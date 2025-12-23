using CalamityOverhaul.OtherMods.MagicStorage;
using InnoVault.Actors;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

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
        public Point16 targetChestPos = Point16.NegativeOne;
        [SyncVar]
        public Point16 targetMagicStoragePos = Point16.NegativeOne;
        [SyncVar]
        public bool isMagicStorageTarget = false;
        [SyncVar]
        public int armSlot = 0;
        [SyncVar]
        public float rotation = 0f;

        //本地字段(不同步)
        private Item graspItem;
        private bool initialized;

        //存储目标坐标
        [SyncVar]
        private Point16 collectorPos = Point16.NegativeOne;

        //物理模拟参数
        private const float SpringStiffness = 0.15f;
        private const float Damping = 0.85f;
        private const float MaxSpeed = 16f;
        private const float ArrivalThreshold = 32f;

        //视觉效果参数(仅客户端)
        private float clampOpenness = 0f;
        private float shakeIntensity = 0f;
        private int particleTimer = 0;
        private float rotationVelocity = 0f;

        //状态机
        private ArmState currentState {
            get => (ArmState)stateValue;
            set => stateValue = (byte)value;
        }
        private int stateTimer = 0;

        //搜索冷却(避免频繁搜索)
        private int searchCooldown = 0;

        //钱币吸附参数
        private const float CoinMagnetRange = 200f;
        private bool isCollectingCoins = false;
        private List<int> magnetizedCoins = new List<int>();

        //不重要物品列表
        private readonly static HashSet<int> unimportances = [
            ItemID.Heart, ItemID.CandyCane, ItemID.CandyApple,
            ItemID.Star, ItemID.SoulCake
        ];

        public override void OnSpawn(object obj) {
            Width = 32;
            Height = 32;
            DrawLayer = ActorDrawLayer.BeforeTiles;

            if (obj is object[] args && args.Length >= 2) {
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

        private static bool IsCoin(Item item) {
            return item.type == ItemID.CopperCoin ||
                   item.type == ItemID.SilverCoin ||
                   item.type == ItemID.GoldCoin ||
                   item.type == ItemID.PlatinumCoin;
        }

        private Item FindNearestItem() {
            if (VaultUtils.isClient) return null;

            Item bestItem = null;
            float minDistSQ = 4000000f;
            int itemFilterType = ModContent.ItemType<ItemFilter>();

            foreach (var item in Main.ActiveItems) {
                if (!IsValidTarget(item)) continue;

                //检查过滤器
                if (collectorTP.TagItemSign == itemFilterType) {
                    var filterData = collectorTP.ItemFilter.GetGlobalItem<ItemFilterData>();
                    if (!filterData.Items.Contains(item.type)) {
                        continue;
                    }
                }
                else if (collectorTP.TagItemSign > ItemID.None && item.type != collectorTP.TagItemSign) {
                    continue;
                }

                //提前检查存储目标(避免抓取后无处存放)
                if (collectorTP.FindStorageTarget(item) == null) {
                    continue;
                }

                float distSQ = item.Center.DistanceSQ(Center);
                if (distSQ < minDistSQ) {
                    bestItem = item;
                    minDistSQ = distSQ;
                }
            }

            return bestItem;
        }

        private bool IsValidTarget(Item item) {
            if (item.IsAir || !item.active) return false;
            if (unimportances.Contains(item.type)) return false;

            int targetCollector = item.CWR().TargetByCollector;
            //只接受未被锁定或被自己锁定的物品
            if (targetCollector >= 0 && targetCollector != WhoAmI) return false;

            return true;
        }

        private void MagnetizeNearbyCoins(Vector2 targetCenter) {
            if (VaultUtils.isClient) return;

            magnetizedCoins.Clear();

            //查找周围的所有钱币
            foreach (var coin in Main.ActiveItems) {
                if (!coin.active || coin.IsAir) continue;
                if (!IsCoin(coin)) continue;

                //检查距离
                float distance = Vector2.Distance(coin.Center, targetCenter);
                if (distance > CoinMagnetRange) continue;

                //检查是否已被其他收集器锁定
                int targetCollector = coin.CWR().TargetByCollector;
                if (targetCollector >= 0 && targetCollector != WhoAmI) continue;

                //锁定这个钱币
                coin.CWR().TargetByCollector = WhoAmI;
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
            if (VaultUtils.isClient) return;
            if (magnetizedCoins.Count == 0) return;

            long totalValue = graspItem.IsACoin ? GetCoinValue(graspItem) * graspItem.stack : 0;

            //收集所有钱币的总价值
            foreach (int coinWhoAmI in magnetizedCoins) {
                if (coinWhoAmI < 0 || coinWhoAmI >= Main.maxItems) continue;

                Item coin = Main.item[coinWhoAmI];
                if (!coin.active || coin.IsAir) continue;

                totalValue += GetCoinValue(coin) * coin.stack;
                coin.TurnToAir();
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, coinWhoAmI);
            }

            //将总价值转换回最优钱币组合
            if (totalValue > 0) {
                graspItem = ConvertValueToCoin(totalValue);
                graspItem.CWR().TargetByCollector = WhoAmI;
            }

            magnetizedCoins.Clear();
        }

        private static int GetCoinValue(Item coin) {
            return coin.type switch {
                ItemID.CopperCoin => 1,
                ItemID.SilverCoin => 100,
                ItemID.GoldCoin => 10000,
                ItemID.PlatinumCoin => 1000000,
                _ => 0
            };
        }

        private static Item ConvertValueToCoin(long value) {
            //优先使用大面值钱币
            if (value >= 1000000) {
                return new Item(ItemID.PlatinumCoin, (int)(value / 1000000));
            }
            else if (value >= 10000) {
                return new Item(ItemID.GoldCoin, (int)(value / 10000));
            }
            else if (value >= 100) {
                return new Item(ItemID.SilverCoin, (int)(value / 100));
            }
            else {
                return new Item(ItemID.CopperCoin, (int)value);
            }
        }

        private Chest GetTargetChest() {
            if (targetChestPos == Point16.NegativeOne) return null;
            int index = Chest.FindChest(targetChestPos.X, targetChestPos.Y);
            return index >= 0 ? Main.chest[index] : null;
        }

        private object GetTargetMagicStorage() {
            if (targetMagicStoragePos == Point16.NegativeOne) return null;
            if (!ModLoader.HasMod("MagicStorage")) return null;

            try {
                return MSRef.FindMagicStorage(graspItem, targetMagicStoragePos, CollectorTP.maxFindChestMode);
            } catch {
                return null;
            }
        }

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
            if (Main.netMode == NetmodeID.Server) return;

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

        private void State_Idle() {
            stateTimer++;
            searchCooldown = Math.Max(0, searchCooldown - 1);

            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.1f);
            shakeIntensity *= 0.9f;

            //每30帧且冷却结束后搜索
            if (stateTimer >= 30 && searchCooldown == 0 && collectorTP.MachineData.UEvalue >= collectorTP.consumeUE) {
                if (!VaultUtils.isClient) {
                    TransitionToState(ArmState.Searching);
                }
            }

            Vector2 idleOffset = GetIdleOffset();
            SpringPhysicsMove(startPos + idleOffset, 0.8f);
        }

        private void State_Searching() {
            //只在服务器端搜索
            if (!VaultUtils.isClient) {
                Item foundItem = FindNearestItem();

                if (foundItem != null) {
                    targetItemWhoAmI = foundItem.whoAmI;
                    foundItem.CWR().TargetByCollector = WhoAmI;

                    //检查是否为钱币,如果是则标记为钱币收集模式
                    isCollectingCoins = IsCoin(foundItem);

                    //如果是钱币,立即吸附周围的钱币
                    if (isCollectingCoins) {
                        MagnetizeNearbyCoins(foundItem.Center);
                    }

                    //消耗能量
                    collectorTP.MachineData.UEvalue -= collectorTP.consumeUE;
                    collectorTP.SendData();

                    TransitionToState(ArmState.MovingToItem);
                }
                else {
                    searchCooldown = 60; //设置搜索冷却
                    TransitionToState(ArmState.Idle);
                }
            }

            //播放音效(所有客户端)
            if (stateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f, Pitch = 0.3f }, Center);
            }
        }

        private void State_MovingToItem() {
            if (targetItemWhoAmI < 0 || targetItemWhoAmI >= Main.maxItems) {
                TransitionToState(ArmState.Idle);
                return;
            }

            Item targetItem = Main.item[targetItemWhoAmI];
            if (!IsValidTarget(targetItem) || targetItem.CWR().TargetByCollector != WhoAmI && targetItem.CWR().TargetByCollector != -1) {
                TransitionToState(ArmState.Idle);
                return;
            }

            targetPosition = targetItem.Center;

            //计算到目标的距离
            float distanceToTarget = Vector2.Distance(Center, targetPosition);

            //根据距离调整速度倍率,距离越近速度越慢,防止越过
            float speedMultiplier = MathHelper.Clamp(distanceToTarget / ArrivalThreshold, 0.3f, 1.2f);

            SpringPhysicsMove(targetPosition, speedMultiplier);
            SpawnMechanicalParticles();

            clampOpenness = MathHelper.Lerp(clampOpenness, 0.8f, 0.15f);

            if (distanceToTarget < ArrivalThreshold) {
                TransitionToState(ArmState.Grasping);
            }
        }

        private void State_Grasping() {
            stateTimer++;

            if (targetItemWhoAmI < 0 || targetItemWhoAmI >= Main.maxItems) {
                TransitionToState(ArmState.Idle);
                return;
            }

            Item targetItem = Main.item[targetItemWhoAmI];

            clampOpenness = MathHelper.Lerp(clampOpenness, 0f, 0.25f);
            shakeIntensity = 1.5f;

            targetPosition = targetItem.Center;
            //抓取时保持在目标位置,速度倍率降低
            SpringPhysicsMove(targetPosition, 0.3f);

            SpawnMechanicalParticles(intensive: true);

            //抓取完成(仅服务器端处理物品)
            if (stateTimer > 12) {
                if (!VaultUtils.isClient) {
                    graspItem = targetItem.Clone();
                    targetItem.TurnToAir();
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, targetItem.whoAmI);

                    //如果是钱币收集模式,合并所有吸附的钱币
                    if (isCollectingCoins) {
                        MergeMagnetizedCoins();
                    }

                    //查找存储目标(箱子或Magic Storage)
                    object storageTarget = collectorTP.FindStorageTarget(graspItem);

                    if (storageTarget is Chest chest) {
                        targetChestPos = new Point16(chest.x, chest.y);
                        targetMagicStoragePos = Point16.NegativeOne;
                        isMagicStorageTarget = false;
                        graspItem.CWR().TargetByCollector = WhoAmI;
                        TransitionToState(ArmState.MovingToChest);
                    }
                    else if (storageTarget != null && ModLoader.HasMod("MagicStorage")) {
                        //Magic Storage目标
                        try {
                            var heartType = CWRMod.Instance.magicStorage.Find<ModTileEntity>("TEStorageHeart").Type;
                            foreach (var te in TileEntity.ByID.Values) {
                                if (te.type == heartType && te == storageTarget) {
                                    targetMagicStoragePos = te.Position;
                                    targetChestPos = Point16.NegativeOne;
                                    isMagicStorageTarget = true;
                                    graspItem.CWR().TargetByCollector = WhoAmI;
                                    TransitionToState(ArmState.MovingToChest);
                                    break;
                                }
                            }
                        } catch {
                            //找不到Magic Storage,丢弃物品
                            VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, graspItem);
                            graspItem.TurnToAir();
                            TransitionToState(ArmState.Idle);
                        }
                    }
                    else {
                        //找不到存储位置,丢弃物品
                        VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, graspItem);
                        graspItem.TurnToAir();
                        TransitionToState(ArmState.Idle);
                    }
                }

                //音效和特效(所有客户端)
                if (stateTimer == 13) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = -0.2f }, Center);

                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 15; i++) {
                            Vector2 particleVel = Main.rand.NextVector2Circular(4, 4);
                            Dust dust = Dust.NewDustDirect(Center - Vector2.One * 16, 32, 32,
                                DustID.Electric, particleVel.X, particleVel.Y, 100, Color.Cyan, 1.5f);
                            dust.noGravity = true;
                        }
                    }
                }
            }
        }

        private void State_MovingToChest() {
            if (graspItem == null || graspItem.type == ItemID.None) {
                TransitionToState(ArmState.Idle);
                return;
            }

            //确定目标位置
            Vector2 targetPos;
            if (isMagicStorageTarget && targetMagicStoragePos != Point16.NegativeOne) {
                targetPos = targetMagicStoragePos.ToWorldCoordinates() + new Vector2(8, 8);
            }
            else if (!isMagicStorageTarget && targetChestPos != Point16.NegativeOne) {
                targetPos = targetChestPos.ToWorldCoordinates() + new Vector2(8, 8);
            }
            else {
                //目标失效
                if (!VaultUtils.isClient) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, graspItem);
                    graspItem.TurnToAir();
                }
                TransitionToState(ArmState.Idle);
                return;
            }

            targetPosition = targetPos;
            SpringPhysicsMove(targetPosition, 1.0f);

            graspItem.Center = Center;
            SpawnMechanicalParticles();

            //到达目标
            Rectangle targetRect;
            if (isMagicStorageTarget) {
                targetRect = targetMagicStoragePos.ToWorldCoordinates().GetRectangle(48, 48);
            }
            else {
                targetRect = targetChestPos.ToWorldCoordinates().GetRectangle(32);
            }

            if (HitBox.Intersects(targetRect)) {
                TransitionToState(ArmState.Depositing);
            }
        }

        private void State_Depositing() {
            stateTimer++;

            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.2f);
            shakeIntensity = 0.8f;

            SpawnMechanicalParticles(intensive: true);

            if (stateTimer > 10) {
                //只在服务器端处理物品存储
                if (!VaultUtils.isClient) {
                    if (isMagicStorageTarget && ModLoader.HasMod("MagicStorage")) {
                        //存储到Magic Storage
                        try {
                            object magicStorage = GetTargetMagicStorage();
                            if (magicStorage != null) {
                                MSRef.DepositItemMethod?.Invoke(magicStorage, [graspItem]);
                            }
                        } catch {
                            //失败则掉落物品
                            VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, graspItem);
                        }
                    }
                    else {
                        //存储到箱子
                        Chest targetChest = GetTargetChest();
                        if (targetChest != null) {
                            targetChest.eatingAnimationTime = 20;
                            targetChest.AddItem(graspItem, true);
                            CheckCoins(targetChest);
                        }
                    }

                    graspItem.TurnToAir();
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, graspItem.whoAmI);
                    }
                }

                //音效(所有客户端)
                if (stateTimer == 11) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = 0.3f }, Center);
                }

                if (stateTimer > 15) {
                    TransitionToState(ArmState.Idle);
                }
            }
        }

        private void TransitionToState(ArmState newState) {
            bool was = currentState != newState;
            currentState = newState;
            stateTimer = 0;

            if (newState == ArmState.Idle) {
                targetItemWhoAmI = -1;
                targetChestPos = Point16.NegativeOne;
                targetMagicStoragePos = Point16.NegativeOne;
                isMagicStorageTarget = false;
                isCollectingCoins = false;
                magnetizedCoins.Clear();
            }

            if (was) {
                //只在服务器端触发网络更新
                if (!VaultUtils.isClient) {
                    NetUpdate = true;
                }
            }
        }

        private Vector2 GetIdleOffset() {
            return armSlot switch {
                1 => new Vector2(120, -20),
                2 => new Vector2(-120, -20),
                _ => new Vector2(0, -120)
            };
        }

        private static void CheckCoins(Chest chest) {
            long totalValue = 0;

            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && !item.IsAir && item.IsACoin) {
                    int value = item.type switch {
                        ItemID.SilverCoin => 100,
                        ItemID.GoldCoin => 10000,
                        ItemID.PlatinumCoin => 1000000,
                        _ => 1
                    };
                    totalValue += (long)value * item.stack;
                    item.TurnToAir();
                }
            }

            if (totalValue <= 0) return;

            if (totalValue >= 1000000) {
                chest.AddItem(new Item(ItemID.PlatinumCoin, (int)(totalValue / 1000000)));
                totalValue %= 1000000;
            }
            if (totalValue >= 10000) {
                chest.AddItem(new Item(ItemID.GoldCoin, (int)(totalValue / 10000)));
                totalValue %= 10000;
            }
            if (totalValue >= 100) {
                chest.AddItem(new Item(ItemID.SilverCoin, (int)(totalValue / 100)));
                totalValue %= 100;
            }
            if (totalValue > 0) {
                chest.AddItem(new Item(ItemID.CopperCoin, (int)totalValue));
            }
        }

        public override void AI() {
            if (!initialized) {
                if (!VaultUtils.isClient) {
                    startPos = Center;
                    velocity = Vector2.Zero;
                    NetUpdate = true;
                }
                initialized = true;
            }

            if (collectorPos == Point16.NegativeOne) {
                return;
            }

            if (!TileProcessorLoader.AutoPositionGetTP(collectorPos, out collectorTP)) {
                if (!VaultUtils.isClient) {
                    ActorLoader.KillActor(WhoAmI);
                }
                return;
            }

            startPos = collectorTP.ArmPos;

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

            //绘制抓取的物品
            if (graspItem != null && !graspItem.IsAir) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, graspItem.type
                    , Center - Main.screenPosition, 1f
                    , clampRot + MathHelper.PiOver2, drawColor);
            }

            return false;
        }
    }
}
