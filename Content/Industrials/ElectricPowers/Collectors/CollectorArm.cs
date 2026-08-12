using InnoVault.Actors;
using InnoVault.Storages;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors
{
    internal class CollectorArm : Actor
    {
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalArm")]
        private static Asset<Texture2D> arm = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClamp")]
        private static Asset<Texture2D> clamp = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClampGlow")]
        private static Asset<Texture2D> clampGlow = null;

        //核心引用
        internal CollectorTP collectorTP;

        //同步字段
        [SyncVar]
        public Vector2 startPos;
        [SyncVar]
        public Vector2 velocity;
        [SyncVar]
        public Vector2 targetPosition;
        [SyncVar]
        public byte stateValue;
        [SyncVar]
        public int targetItemWhoAmI = -1;
        [SyncVar]
        public Point16 targetStoragePos = Point16.NegativeOne;
        [SyncVar]
        public int armSlot = 0;
        [SyncVar]
        public float rotation = 0f;
        //夹爪上物品的展示类型，graspItem实体只存在于服务端，客户端靠这个字段绘制
        [SyncVar]
        public int graspItemType = ItemID.None;
        //所属收集器的坐标，同时也是TP端归属校验的依据
        [SyncVar]
        internal Point16 collectorPos = Point16.NegativeOne;

        //服务端字段(不同步)
        private Item graspItem;
        private readonly List<Item> extraGraspItems = [];
        private IStorageProvider cachedStorageProvider;
        private int searchCooldown;
        private bool isCollectingCoins;
        private readonly List<int> magnetizedCoins = [];

        //本地字段(不同步)
        private bool initialized;

        //物理模拟参数
        private const float SpringStiffness = 0.15f;
        private const float Damping = 0.85f;
        private const float MaxSpeed = 16f;
        private const float ArrivalThreshold = 32f;
        //物品搜索半径(像素)
        private const float ItemSearchRange = 2000f;
        //收集者锁的持续帧数，追踪期间每帧刷新
        private const int LockDuration = 120;

        //视觉效果参数(仅客户端)
        private float clampOpenness = 0f;
        private float shakeIntensity = 0f;
        private int particleTimer = 0;
        private float rotationVelocity = 0f;
        //客户端上次同步状态(检测切换)
        private byte lastClientState;

        //状态机
        private ArmState currentState {
            get => (ArmState)stateValue;
            set => stateValue = (byte)value;
        }
        private int stateTimer = 0;

        //钱币吸附参数
        private const float CoinMagnetRange = 200f;

        //不重要物品列表
        private readonly static HashSet<int> unimportances = [
            ItemID.Heart, ItemID.CandyCane, ItemID.CandyApple,
            ItemID.Star, ItemID.SoulCake
        ];

        public override void OnSpawn(params object[] args) {
            Width = 32;
            Height = 32;
            DrawExtendMode = 1200;
            DrawLayer = ActorDrawLayer.BeforeTiles;

            if (args is not null && args.Length >= 2) {
                collectorPos = (Point16)args[0];
                armSlot = (int)args[1];
            }

            if (!VaultUtils.isClient) {
                startPos = Position;
                velocity = Vector2.Zero;
                NetUpdate = true;
            }
            initialized = true;
            graspItem = new Item();
        }

        #region 目标与锁

        private Item FindNearestItem() {
            if (VaultUtils.isClient) {
                return null;
            }

            //先查有无存储，再扫物品
            var storageCandidates = collectorTP.GetStorageCandidates();
            if (storageCandidates.Count == 0) {
                collectorTP.PromptNoStorage();
                return null;
            }

            Item bestItem = null;
            float minDistSQ = ItemSearchRange * ItemSearchRange;
            bool useFilter = collectorTP.FilterInstalled;

            foreach (var item in Main.ActiveItems) {
                if (!IsValidTarget(item)) {
                    continue;
                }

                //检查过滤器(空名单=不限制)
                if (useFilter) {
                    if (!collectorTP.Filter.Matches(item.type)) {
                        continue;
                    }
                }
                else if (collectorTP.TagItemSign > ItemID.None && item.type != collectorTP.TagItemSign) {
                    continue;
                }

                float distSQ = item.Center.DistanceSQ(Center);
                if (distSQ >= minDistSQ) {
                    continue;
                }

                //存储检查放在最后，它是最昂贵的判断
                if (!AnyStorageAccepts(storageCandidates, item)) {
                    continue;
                }

                bestItem = item;
                minDistSQ = distSQ;
            }

            return bestItem;
        }

        private static bool AnyStorageAccepts(IReadOnlyList<IStorageProvider> candidates, Item item) {
            for (int i = 0; i < candidates.Count; i++) {
                IStorageProvider provider = candidates[i];
                if (provider.IsValid && provider.CanAcceptItem(item)) {
                    return true;
                }
            }
            return false;
        }

        private bool IsValidTarget(Item item) {
            if (item.IsAir || !item.active) {
                return false;
            }
            if (unimportances.Contains(item.type)) {
                return false;
            }

            int targetCollector = item.CWR().TargetByCollector;
            //只接受未被锁定或被自己锁定的物品
            if (targetCollector >= 0 && targetCollector != WhoAmI) {
                return false;
            }

            return true;
        }

        /// <summary>锁物品并刷新时长(追踪每帧)</summary>
        private void LockItem(Item item) {
            var cwr = item.CWR();
            cwr.TargetByCollector = WhoAmI;
            cwr.CollectorLockTime = LockDuration;
        }

        private void RefreshCoinLocks() {
            foreach (int coinWhoAmI in magnetizedCoins) {
                if (coinWhoAmI < 0 || coinWhoAmI >= Main.maxItems) {
                    continue;
                }
                Item coin = Main.item[coinWhoAmI];
                if (coin.active && !coin.IsAir && coin.CWR().TargetByCollector == WhoAmI) {
                    coin.CWR().CollectorLockTime = LockDuration;
                }
            }
        }

        private void UnlockTrackedItems() {
            if (targetItemWhoAmI >= 0 && targetItemWhoAmI < Main.maxItems) {
                Item item = Main.item[targetItemWhoAmI];
                if (item.active && item.CWR().TargetByCollector == WhoAmI) {
                    item.CWR().TargetByCollector = -1;
                }
            }
            foreach (int coinWhoAmI in magnetizedCoins) {
                if (coinWhoAmI < 0 || coinWhoAmI >= Main.maxItems) {
                    continue;
                }
                Item coin = Main.item[coinWhoAmI];
                if (coin.active && coin.CWR().TargetByCollector == WhoAmI) {
                    coin.CWR().TargetByCollector = -1;
                }
            }
        }

        /// <summary>夹爪物品吐回世界(中断/销毁)</summary>
        private void DropCarriedItems() {
            if (VaultUtils.isClient) {
                return;
            }
            if (graspItem.Alives()) {
                graspItem.CWR().TargetByCollector = -1;
                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, graspItem);
                graspItem.TurnToAir();
            }
            foreach (var extra in extraGraspItems) {
                if (extra.Alives()) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, extra);
                }
            }
            extraGraspItems.Clear();
            graspItemType = ItemID.None;
        }

        #endregion

        #region 钱币

        private void MagnetizeNearbyCoins(Vector2 targetCenter) {
            if (VaultUtils.isClient) {
                return;
            }

            magnetizedCoins.Clear();

            //查找周围的所有钱币
            foreach (var coin in Main.ActiveItems) {
                if (!coin.active || coin.IsAir || !coin.IsACoin) {
                    continue;
                }

                //检查距离
                if (Vector2.Distance(coin.Center, targetCenter) > CoinMagnetRange) {
                    continue;
                }

                //已被其他臂锁
                int targetCollector = coin.CWR().TargetByCollector;
                if (targetCollector >= 0 && targetCollector != WhoAmI) {
                    continue;
                }

                //锁定这个钱币
                LockItem(coin);
                magnetizedCoins.Add(coin.whoAmI);
            }

            //播放吸附音效
            if (magnetizedCoins.Count > 0) {
                SoundEngine.PlaySound(SoundID.CoinPickup with {
                    Volume = 0.4f,
                    Pitch = 0.2f
                }, targetCenter);
            }
        }

        private void MergeMagnetizedCoins() {
            if (VaultUtils.isClient || magnetizedCoins.Count == 0) {
                return;
            }

            long totalValue = graspItem.IsACoin ? GetCoinValue(graspItem) * graspItem.stack : 0;

            //收集所有钱币的总价值
            foreach (int coinWhoAmI in magnetizedCoins) {
                if (coinWhoAmI < 0 || coinWhoAmI >= Main.maxItems) {
                    continue;
                }

                Item coin = Main.item[coinWhoAmI];
                if (!coin.active || coin.IsAir) {
                    continue;
                }

                totalValue += GetCoinValue(coin) * coin.stack;
                coin.TurnToAir();
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, coinWhoAmI);
            }

            //将总价值完整分解为多面值钱币，夹爪持有最大面值，其余作为找零一同携带
            if (totalValue > 0) {
                List<Item> coins = ConvertValueToCoins(totalValue);
                graspItem = coins[0];
                graspItem.CWR().TargetByCollector = WhoAmI;
                for (int i = 1; i < coins.Count; i++) {
                    extraGraspItems.Add(coins[i]);
                }
            }

            magnetizedCoins.Clear();
        }

        private static long GetCoinValue(Item coin) {
            return coin.type switch {
                ItemID.CopperCoin => 1,
                ItemID.SilverCoin => 100,
                ItemID.GoldCoin => 10000,
                ItemID.PlatinumCoin => 1000000,
                _ => 0
            };
        }

        private static readonly (int type, long unit)[] CoinDenominations = [
            (ItemID.PlatinumCoin, 1000000),
            (ItemID.GoldCoin, 10000),
            (ItemID.SilverCoin, 100),
            (ItemID.CopperCoin, 1)
        ];

        /// <summary>价值拆成钱币(无余数丢失)</summary>
        private static List<Item> ConvertValueToCoins(long value) {
            List<Item> result = [];
            foreach ((int type, long unit) in CoinDenominations) {
                long count = value / unit;
                value %= unit;
                while (count > 0) {
                    Item coin = new Item(type);
                    coin.stack = (int)Math.Min(count, coin.maxStack);
                    count -= coin.stack;
                    result.Add(coin);
                }
            }
            if (result.Count == 0) {
                result.Add(new Item(ItemID.CopperCoin));
            }
            return result;
        }

        private static void CheckCoins(Chest chest) {
            long totalValue = 0;

            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && !item.IsAir && item.IsACoin) {
                    totalValue += GetCoinValue(item) * item.stack;
                    item.TurnToAir();
                }
            }

            if (totalValue <= 0) {
                return;
            }

            foreach (Item coin in ConvertValueToCoins(totalValue)) {
                chest.AddItem(coin, true);
            }
        }

        #endregion

        #region 存储解析

        /// <summary>目标存储(缓存失效则按坐标重解析)</summary>
        private IStorageProvider GetTargetStorage() {
            if (targetStoragePos == Point16.NegativeOne) {
                return null;
            }

            //检查缓存是否有效
            if (cachedStorageProvider != null && cachedStorageProvider.IsValid
                && cachedStorageProvider.Position == targetStoragePos) {
                return cachedStorageProvider;
            }

            cachedStorageProvider = StorageLoader.GetStorageTargetByPoint(targetStoragePos, graspItem);
            return cachedStorageProvider;
        }

        #endregion

        #region 运动与表现

        private void SpringPhysicsMove(Vector2 target, float speedMultiplier = 1f) {
            Vector2 toTarget = target - Center;

            //弹簧力
            Vector2 springForce = toTarget * SpringStiffness * speedMultiplier;
            velocity += springForce;

            //阻尼
            velocity *= Damping;

            //限速
            if (velocity.LengthSquared() > MaxSpeed * MaxSpeed) {
                velocity = Vector2.Normalize(velocity) * MaxSpeed;
            }

            Position += velocity;

            //平滑旋转
            if (velocity.LengthSquared() > 0.1f) {
                float targetRotation = velocity.ToRotation();
                float rotationDiff = MathHelper.WrapAngle(targetRotation - rotation);
                rotationVelocity = MathHelper.Lerp(rotationVelocity, rotationDiff * 0.2f, 0.3f);
                rotation += rotationVelocity;
            }
        }

        private void SpawnMechanicalParticles(bool intensive = false) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            particleTimer++;
            int spawnRate = intensive ? 8 : 16;

            if (particleTimer % spawnRate == 0) {
                Vector2 particleVel = velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 8, 16, 16,
                    DustID.Electric, particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        private Vector2 GetIdleOffset() {
            return armSlot switch {
                1 => new Vector2(120, -20),
                2 => new Vector2(-120, -20),
                _ => new Vector2(0, -120)
            };
        }

        /// <summary>状态入场表现(SP走Transition；MP客户端跟同步状态)</summary>
        private void OnStateEnteredEffects(ArmState newState) {
            if (VaultUtils.isServer) {
                return;
            }

            switch (newState) {
                case ArmState.Searching:
                    SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f, Pitch = 0.3f }, Center);
                    break;
                case ArmState.MovingToChest:
                    //抓取完成的瞬间
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = -0.2f }, Center);
                    for (int i = 0; i < 15; i++) {
                        Vector2 particleVel = Main.rand.NextVector2Circular(4, 4);
                        Dust dust = Dust.NewDustDirect(Center - Vector2.One * 16, 32, 32,
                            DustID.Electric, particleVel.X, particleVel.Y, 100, Color.Cyan, 1.5f);
                        dust.noGravity = true;
                    }
                    break;
            }
        }

        #endregion

        #region 状态机

        private void State_Idle() {
            stateTimer++;

            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.1f);
            shakeIntensity *= 0.9f;

            if (!VaultUtils.isClient) {
                searchCooldown = Math.Max(0, searchCooldown - 1);

                //每30帧且冷却结束后搜索
                if (stateTimer >= 30 && searchCooldown == 0
                    && collectorTP.MachineData.UEvalue >= CollectorTP.consumeUE) {
                    TransitionToState(ArmState.Searching);
                }
            }

            SpringPhysicsMove(startPos + GetIdleOffset(), 0.8f);
        }

        private void State_Searching() {
            stateTimer++;

            if (VaultUtils.isClient) {
                //客户端保持悬停，等待服务器的搜索结果
                SpringPhysicsMove(startPos + GetIdleOffset(), 0.8f);
                return;
            }

            Item foundItem = FindNearestItem();

            if (foundItem != null) {
                targetItemWhoAmI = foundItem.whoAmI;
                LockItem(foundItem);

                //钱币则进磁吸并吸周围
                isCollectingCoins = foundItem.IsACoin;
                if (isCollectingCoins) {
                    MagnetizeNearbyCoins(foundItem.Center);
                }

                //消耗能量
                collectorTP.MachineData.UEvalue -= CollectorTP.consumeUE;
                collectorTP.SendData();

                TransitionToState(ArmState.MovingToItem);
            }
            else {
                searchCooldown = 60; //设置搜索冷却
                TransitionToState(ArmState.Idle);
            }
        }

        private void State_MovingToItem() {
            if (!VaultUtils.isClient) {
                if (targetItemWhoAmI < 0 || targetItemWhoAmI >= Main.maxItems) {
                    TransitionToState(ArmState.Idle);
                    return;
                }

                Item targetItem = Main.item[targetItemWhoAmI];
                int lockOwner = targetItem.CWR().TargetByCollector;
                if (!IsValidTarget(targetItem) || (lockOwner != WhoAmI && lockOwner != -1)) {
                    TransitionToState(ArmState.Idle);
                    return;
                }

                //续锁
                LockItem(targetItem);
                RefreshCoinLocks();

                targetPosition = targetItem.Center;

                if (Vector2.Distance(Center, targetPosition) < ArrivalThreshold) {
                    TransitionToState(ArmState.Grasping);
                }
            }
            else if (targetItemWhoAmI >= 0 && targetItemWhoAmI < Main.maxItems) {
                //客户端仅表现，跟同步目标
                Item targetItem = Main.item[targetItemWhoAmI];
                if (targetItem.active && !targetItem.IsAir) {
                    targetPosition = targetItem.Center;
                }
            }

            //近距减速，防越过
            float distanceToTarget = Vector2.Distance(Center, targetPosition);
            float speedMultiplier = MathHelper.Clamp(distanceToTarget / ArrivalThreshold, 0.3f, 1.2f);

            SpringPhysicsMove(targetPosition, speedMultiplier);
            SpawnMechanicalParticles();

            clampOpenness = MathHelper.Lerp(clampOpenness, 0.8f, 0.15f);
        }

        private void State_Grasping() {
            stateTimer++;

            clampOpenness = MathHelper.Lerp(clampOpenness, 0f, 0.25f);
            shakeIntensity = 1.5f;

            if (!VaultUtils.isClient) {
                if (targetItemWhoAmI < 0 || targetItemWhoAmI >= Main.maxItems) {
                    TransitionToState(ArmState.Idle);
                    return;
                }

                Item targetItem = Main.item[targetItemWhoAmI];
                if (!targetItem.Alives()) {
                    TransitionToState(ArmState.Idle);
                    return;
                }

                LockItem(targetItem);
                RefreshCoinLocks();
                targetPosition = targetItem.Center;

                //抓取完成
                if (stateTimer > 12) {
                    graspItem = targetItem.Clone();
                    targetItem.TurnToAir();
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, targetItemWhoAmI);

                    //如果是钱币收集模式,合并所有吸附的钱币
                    if (isCollectingCoins) {
                        MergeMagnetizedCoins();
                    }
                    graspItemType = graspItem.type;

                    var storageProvider = collectorTP.FindStorageTarget(graspItem);
                    if (storageProvider != null) {
                        targetStoragePos = storageProvider.Position;
                        cachedStorageProvider = storageProvider;
                        graspItem.CWR().TargetByCollector = WhoAmI;
                        TransitionToState(ArmState.MovingToChest);
                    }
                    else {
                        //找不到存储位置,原地放回物品
                        DropCarriedItems();
                        TransitionToState(ArmState.Idle);
                    }
                    return;
                }
            }

            SpringPhysicsMove(targetPosition, 0.3f);
            SpawnMechanicalParticles(intensive: true);
        }

        private void State_MovingToChest() {
            if (!VaultUtils.isClient) {
                if (!graspItem.Alives() || targetStoragePos == Point16.NegativeOne) {
                    DropCarriedItems();
                    TransitionToState(ArmState.Idle);
                    return;
                }

                var storage = GetTargetStorage();
                if (storage == null || !storage.IsValid) {
                    //存储目标失效
                    DropCarriedItems();
                    TransitionToState(ArmState.Idle);
                    return;
                }

                targetPosition = storage.WorldCenter;

                //到达目标
                if (HitBox.Intersects(storage.HitBox)) {
                    TransitionToState(ArmState.Depositing);
                }
            }

            SpringPhysicsMove(targetPosition, 1.0f);
            SpawnMechanicalParticles();
            clampOpenness = MathHelper.Lerp(clampOpenness, 0f, 0.2f);
        }

        private void State_Depositing() {
            stateTimer++;

            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.2f);
            shakeIntensity = 0.8f;

            SpawnMechanicalParticles(intensive: true);

            //仅服务器存一次
            if (!VaultUtils.isClient && stateTimer == 11) {
                var storage = GetTargetStorage();
                //原版箱子改动后需广播变化槽位，否则开着箱子的玩家看到过期内容
                var chestSnap = ChestNetSync.Capture(storage);
                if (storage != null && storage.IsValid && storage.DepositItem(graspItem)) {
                    storage.PlayDepositAnimation();
                    graspItem.TurnToAir();

                    //钱币找零一并存入，存不下的落地
                    foreach (var extra in extraGraspItems) {
                        if (extra.Alives() && !(storage.IsValid && storage.DepositItem(extra))) {
                            VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, extra);
                        }
                    }
                    extraGraspItems.Clear();
                    graspItemType = ItemID.None;

                    //如果是箱子，合并其中的钱币
                    if (storage is ChestStorageProvider chestProvider && chestProvider.ChestIndex >= 0) {
                        CheckCoins(Main.chest[chestProvider.ChestIndex]);
                    }

                    //Actor在主线程串行更新，可直接发送
                    ChestNetSync.SendChanged(chestSnap.ChestIndex, ChestNetSync.CollectChanged(chestSnap));
                }
                else {
                    //存储失败则掉落物品
                    DropCarriedItems();
                }
            }

            //音效(单人与客户端按本地计时触发)
            if (stateTimer == 11) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = 0.3f }, Center);
            }

            if (!VaultUtils.isClient && stateTimer > 15) {
                TransitionToState(ArmState.Idle);
            }
        }

        /// <summary>状态转换(仅服务器；客户端跟stateValue)</summary>
        private void TransitionToState(ArmState newState) {
            if (VaultUtils.isClient) {
                return;
            }

            bool changed = currentState != newState;
            currentState = newState;
            stateTimer = 0;

            if (newState == ArmState.Idle) {
                //中断路径必须解除锁定，否则臂死亡后物品会被幽灵索引永久锁定
                UnlockTrackedItems();
                targetItemWhoAmI = -1;
                targetStoragePos = Point16.NegativeOne;
                cachedStorageProvider = null;
                isCollectingCoins = false;
                magnetizedCoins.Clear();
                graspItemType = ItemID.None;
            }

            if (changed) {
                NetUpdate = true;
                OnStateEnteredEffects(newState);
            }
        }

        #endregion

        public override void AI() {
            if (!initialized) {
                if (!VaultUtils.isClient) {
                    startPos = Center;
                    velocity = Vector2.Zero;
                    NetUpdate = true;
                }
                initialized = true;
                graspItem ??= new Item();
            }

            if (collectorPos == Point16.NegativeOne) {
                return;
            }

            if (!TileProcessorLoader.AutoPositionGetTP(collectorPos, out collectorTP)) {
                if (!VaultUtils.isClient) {
                    //收集器没了，先吐物再自毁
                    DropCarriedItems();
                    UnlockTrackedItems();
                    ActorLoader.KillActor(WhoAmI);
                }
                return;
            }

            startPos = collectorTP.ArmPos;

            //客户端观察到服务器同步的状态变化时，重置本地计时并触发入场表现
            if (VaultUtils.isClient && stateValue != lastClientState) {
                lastClientState = stateValue;
                stateTimer = 0;
                OnStateEnteredEffects(currentState);
            }

            //状态机驱动
            switch (currentState) {
                case ArmState.Idle:
                    State_Idle();
                    break;
                case ArmState.Searching:
                    State_Searching();
                    break;
                case ArmState.MovingToItem:
                    State_MovingToItem();
                    break;
                case ArmState.Grasping:
                    State_Grasping();
                    break;
                case ArmState.MovingToChest:
                    State_MovingToChest();
                    break;
                case ArmState.Depositing:
                    State_Depositing();
                    break;
            }

            shakeIntensity *= 0.92f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (startPos == Vector2.Zero) {
                return false;
            }

            if (collectorTP?.BatteryPrompt == true) {
                drawColor = new Color(drawColor.R / 2, drawColor.G / 2, drawColor.B / 2, 255);
            }

            Texture2D tex = arm.Value;
            Vector2 start = startPos;
            Vector2 end = Center;

            //添加抖动效果
            if (shakeIntensity > 0.01f) {
                end += Main.rand.NextVector2Circular(shakeIntensity * 2, shakeIntensity * 2);
            }

            //动态贝塞尔曲线控制点
            float dist = Vector2.Distance(start, end);
            float bendHeight = MathHelper.Clamp(dist * 0.5f, 40f, 200f);

            //根据速度添加动态弯曲
            float velocityInfluence = velocity.Length() * 2f;
            bendHeight += velocityInfluence;

            Vector2 midControl = (start + end) / 2 + new Vector2(0, -bendHeight);

            //计算曲线长度
            int sampleCount = 60;
            float curveLength = 0f;
            Vector2 prev = start;
            for (int i = 1; i <= sampleCount; i++) {
                float t = i / (float)sampleCount;
                Vector2 point = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
                curveLength += Vector2.Distance(prev, point);
                prev = point;
            }

            float segmentLength = tex.Height / 2;
            int segmentCount = Math.Max(2, (int)(curveLength / segmentLength));
            if (segmentCount > short.MaxValue) {
                return false;//段数上限
            }

            Vector2[] points = new Vector2[segmentCount + 1];

            for (int i = 0; i <= segmentCount; i++) {
                float t = i / (float)segmentCount;
                points[i] = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
            }

            float clampRot = rotation;

            //绘制机械臂
            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                float rot = direction.ToRotation() + MathHelper.PiOver2;

                if (i == segmentCount - 1) {
                    clampRot = direction.ToRotation();
                }

                //添加轻微的缩放动画
                float scale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i * 0.5f) * 0.02f;

                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rot
                    , new Vector2(tex.Width / 2f, tex.Height), scale, SpriteEffects.None, 0f);
            }

            //绘制夹子
            int clampFrame = clampOpenness > 0.5f ? 0 : 1;

            Main.spriteBatch.Draw(clamp.Value, Center - Main.screenPosition
                , clamp.Value.GetRectangle(clampFrame, 2)
                , drawColor, clampRot + MathHelper.PiOver2
                , clamp.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(clampGlow.Value, Center - Main.screenPosition
                , clampGlow.Value.GetRectangle(clampFrame, 2)
                , Color.White * (0.7f + shakeIntensity * 0.3f), clampRot + MathHelper.PiOver2
                , clampGlow.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            //绘制抓取的物品(类型经同步，客户端也能正确显示)
            if (graspItemType > ItemID.None) {
                VaultUtils.SafeLoadItem(graspItemType);
                VaultUtils.SimpleDrawItem(Main.spriteBatch, graspItemType
                    , Center - Main.screenPosition, 1f
                    , clampRot + MathHelper.PiOver2, drawColor);
            }

            return false;
        }
    }
}
