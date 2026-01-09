using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests.OldDuchestUIs;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests
{
    /// <summary>
    /// 老箱子的TileProcessor
    /// 管理大型存储空间
    /// </summary>
    public class OldDuchestTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<OldDuchestTile>();

        //存储常量
        private const int MAX_INTERACTION_DISTANCE = 9000;

        //存储物品列表
        public List<Item> storedItems = new();

        //动画相关
        private int glowTimer = 0;
        private float glowIntensity = 0f;
        internal bool isOpen = false;
        private int closeTimer = 0;

        //每日刷新相关
        private bool isInCampsite = false;
        private int lastRefreshCycle = -1;
        private bool hasBeenOpened = false;

        //水下状态
        public bool isUnderwater = false;

        public override void SetProperty() {
            storedItems = new List<Item>();
        }

        public override void SendData(ModPacket data) {
            //发送存储物品数据
            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                if (item == null) {
                    ItemIO.Send(new Item(), data, true, true);
                }
                else {
                    ItemIO.Send(item, data, true, true);
                }
            }

            //发送刷新相关数据
            data.Write(isInCampsite);
            data.Write(lastRefreshCycle);
            data.Write(hasBeenOpened);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            //接收存储物品数据
            int count = reader.ReadInt32();
            storedItems.Clear();
            for (int i = 0; i < count; i++) {
                storedItems.Add(ItemIO.Receive(reader, true, true));
            }

            //接收刷新相关数据
            isInCampsite = reader.ReadBoolean();
            lastRefreshCycle = reader.ReadInt32();
            hasBeenOpened = reader.ReadBoolean();

            //同步到UI
            SyncItemsToUI();
        }

        public override void SaveData(TagCompound tag) {
            try {
                //保存存储的物品
                List<TagCompound> itemTags = [];
                foreach (var item in storedItems) {
                    if (item == null) {
                        itemTags.Add(ItemIO.Save(new Item()));
                    }
                    else {
                        itemTags.Add(ItemIO.Save(item));
                    }
                }
                tag["itemTags"] = itemTags;

                //保存刷新数据
                tag["isInCampsite"] = isInCampsite;
                tag["lastRefreshCycle"] = lastRefreshCycle;
                tag["hasBeenOpened"] = hasBeenOpened;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OldDuchestTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                //加载存储的物品
                if (!tag.TryGet("itemTags", out List<TagCompound> itemTags)) {
                    return;
                }

                storedItems.Clear();
                foreach (var itemTag in itemTags) {
                    storedItems.Add(ItemIO.Load(itemTag));
                }

                //加载刷新数据
                if (tag.ContainsKey("isInCampsite")) {
                    isInCampsite = tag.GetBool("isInCampsite");
                }
                if (tag.ContainsKey("lastRefreshCycle")) {
                    lastRefreshCycle = tag.GetInt("lastRefreshCycle");
                }
                if (tag.ContainsKey("hasBeenOpened")) {
                    hasBeenOpened = tag.GetBool("hasBeenOpened");
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OldDuchestTP.LoadData Error: {ex.Message}");
            }
        }

        public override void Initialize() {
            CheckIfInCampsite();
            if (TrackItem == null) {
                hasBeenOpened = false;
                InitializeCampsiteChest();
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (player.whoAmI == Main.myPlayer) {
                OldDuchestUI.Instance.Interactive(this);
            }
            return null;
        }

        public override void Update() {
            //更新发光效果
            if (isOpen) {
                glowIntensity = Math.Min(1f, glowIntensity + 0.1f);
                glowTimer++;
            }
            else {
                glowIntensity = Math.Max(0f, glowIntensity - 0.05f);
            }

            //检测箱子是否在水下
            isUnderwater = CheckChestUnderwater();

            //检查距离自动关闭
            if (isOpen && (Main.LocalPlayer.DistanceSQ(CenterInWorld) > MAX_INTERACTION_DISTANCE || OldDuchestUI.Instance.CurrentChest != this)) {
                CloseUI(Main.LocalPlayer);
                if (OldDuchestUI.Instance.CurrentChest == this) {
                    OldDuchestUI.Instance.Close();
                }
                SoundEngine.PlaySound(CWRSound.OldDuchestClose with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 });
            }

            //营地箱子定期刷新检查
            if (isInCampsite && !isOpen) {
                int currentCycle = OldDuchestLootGenerator.GetGameTimeSeed();
                if (lastRefreshCycle != currentCycle && hasBeenOpened) {
                    RefreshLoot(currentCycle);
                }

                bool updateAdd = false;
                //移除营地附近的掉落物品
                foreach (var i in Main.ActiveItems) {
                    float distance = i.DistanceSQ(CenterInWorld);
                    if (distance > 90000) {
                        continue;//只移除营地附近的掉落物品
                    }
                    i.position += i.To(CenterInWorld).UnitVector() * 6;
                    if (distance < 16) {
                        if (StackAddItem(i)) {
                            updateAdd = true;
                        }
                        if (i.stack <= 0) {
                            i.TurnToAir();
                        }
                    }
                }
                if (updateAdd) {
                    //同步到UI
                    SyncItemsToUI();
                    SendData();
                    if (closeTimer <= 0) {
                        closeTimer = 60;
                        //更新图格帧为打开状态
                        UpdateTileFrame(true);
                        SoundEngine.PlaySound(CWRSound.OldDuchestOpen with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 }, CenterInWorld);
                    }
                }
            }

            if (closeTimer > 0) {
                if (--closeTimer == 0) {
                    //更新图格帧为关闭状态
                    UpdateTileFrame(false);
                    SoundEngine.PlaySound(CWRSound.OldDuchestClose with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 }, CenterInWorld);
                }
            }

            //更新光照
            if (glowIntensity > 0.01f) {
                float pulsePulse = MathF.Sin(glowTimer * 0.05f) * 0.3f + 0.7f;
                Lighting.AddLight(CenterInWorld,
                    new Color(139, 87, 42).ToVector3() * glowIntensity * pulsePulse);
            }
        }

        public bool StackAddItem(Item item) {
            if (!item.Alives()) {
                return false;
            }

            // 处理钱币合并
            if (item.IsACoin) {
                return MergeCoins(item);
            }

            bool changed = false;

            // 尝试堆叠到现有物品
            for (int i = 0; i < storedItems.Count && item.stack > 0; i++) {
                Item slot = storedItems[i];
                if (slot == null || slot.IsAir) {
                    continue;
                }

                if (slot.type == item.type && slot.stack < slot.maxStack) {
                    int transferable = Math.Min(item.stack, slot.maxStack - slot.stack);
                    slot.stack += transferable;
                    item.stack -= transferable;
                    changed = true;
                }
            }

            // 如果还有剩余且有空间，添加新槽位
            if (item.stack > 0) {
                if (storedItems.Count < 240) {
                    storedItems.Add(item.Clone());
                    item.stack = 0;
                    changed = true;
                }
            }

            return changed;
        }

        private bool MergeCoins(Item item) {
            long totalValue = GetCoinValue(item.type) * item.stack;
            List<Item> coinsInChest = new();

            // 收集箱子里的所有钱币
            for (int i = storedItems.Count - 1; i >= 0; i--) {
                if (storedItems[i].IsACoin) {
                    totalValue += GetCoinValue(storedItems[i].type) * storedItems[i].stack;
                    coinsInChest.Add(storedItems[i]);
                    storedItems.RemoveAt(i);
                }
            }

            // 将总价值转换回钱币物品
            List<Item> newCoins = CoinsFromValue(totalValue);

            // 检查是否有足够空间存放整理后的钱币
            // 如果整理后的钱币占用的槽位比原来少或相等，或者箱子还有空位
            if (storedItems.Count + newCoins.Count <= 240) {
                storedItems.AddRange(newCoins);
                item.stack = 0; // 钱币被完全吸收
                return true;
            }
            else {
                // 空间不足，回滚操作
                // 注意：这里回滚可能顺序会变，但对于钱币来说没关系
                storedItems.AddRange(coinsInChest);
                return false;
            }
        }

        private long GetCoinValue(int type) {
            return type switch {
                ItemID.CopperCoin => 1,
                ItemID.SilverCoin => 100,
                ItemID.GoldCoin => 10000,
                ItemID.PlatinumCoin => 1000000,
                _ => 0
            };
        }

        private List<Item> CoinsFromValue(long value) {
            List<Item> list = new();
            long platinum = value / 1000000;
            value %= 1000000;
            long gold = value / 10000;
            value %= 10000;
            long silver = value / 100;
            value %= 100;
            long copper = value;

            AddCoins(list, ItemID.PlatinumCoin, platinum);
            AddCoins(list, ItemID.GoldCoin, gold);
            AddCoins(list, ItemID.SilverCoin, silver);
            AddCoins(list, ItemID.CopperCoin, copper);

            return list;
        }

        private void AddCoins(List<Item> list, int type, long count) {
            while (count > 0) {
                Item item = new Item();
                item.SetDefaults(type);
                int stack = (int)Math.Min(count, item.maxStack);
                item.stack = stack;
                list.Add(item);
                count -= stack;
            }
        }

        /// <summary>
        /// 检测箱子是否在水下
        /// </summary>
        private bool CheckChestUnderwater() {
            Point tileCoord = (CenterInWorld / 16).ToPoint();

            //检查箱子上方是否有水
            for (int y = -3; y <= 0; y++) {
                for (int x = -2; x <= 2; x++) {
                    Tile tile = Framing.GetTileSafely(tileCoord.X + x, tileCoord.Y + y);
                    if (tile.LiquidAmount > 128 && tile.LiquidType == LiquidID.Water) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 打开UI
        /// </summary>
        public void OpenUI(Player player) {
            if (player == null || !player.active) return;

            isOpen = true;

            //标记营地箱子已被打开
            if (isInCampsite) {
                hasBeenOpened = true;
                SendData();
            }

            //如果在水下，播放水泡音效并生成泡泡
            if (isUnderwater) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Pitch = -0.1f,
                    Volume = 0.7f
                }, CenterInWorld);

                //生成一波泡泡效果
                SpawnOpenBubbles();
            }

            //更新图格帧为打开状态
            UpdateTileFrame(true);
        }

        /// <summary>
        /// 生成打开箱子时的泡泡
        /// </summary>
        private void SpawnOpenBubbles() {
            if (VaultUtils.isServer) {
                return;
            }

            //生成15到25个泡泡
            int bubbleCount = Main.rand.Next(15, 26);

            for (int i = 0; i < bubbleCount; i++) {
                Vector2 spawnPos = CenterInWorld + new Vector2(
                    Main.rand.NextFloat(-40f, 40f),
                    Main.rand.NextFloat(-20f, 20f)
                );

                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-1.5f, 1.5f),
                    Main.rand.NextFloat(-3f, -1.5f)
                );

                float scale = Main.rand.NextFloat(0.6f, 1.2f);

                PRTLoader.NewParticle<Industrials.Generator.Hydroelectrics.PRT_WaterBubble>(
                    spawnPos, velocity, Color.White, scale);
            }

            //额外生成一些水粒子
            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = new Vector2(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-3f, -1f)
                );
                Dust.NewDust(CenterInWorld - new Vector2(32, 16), 64, 32,
                    DustID.Water, dustVel.X, dustVel.Y, 100, default, 1.5f);
            }
        }

        /// <summary>
        /// 关闭UI
        /// </summary>
        public void CloseUI(Player player) {
            if (player == null) return;

            isOpen = false;

            //保存UI数据
            if (OldDuchestUI.Instance.CurrentChest == this) {
                SaveItemsFromUI();
            }

            //更新图格帧为关闭状态
            UpdateTileFrame(false);
        }

        /// <summary>
        /// 将物品数据同步到UI
        /// </summary>
        public void SyncItemsToUI() {
            if (OldDuchestUI.Instance == null) return;

            OldDuchestUI.Instance.LoadItems(storedItems);
        }

        /// <summary>
        /// 从UI保存物品数据
        /// </summary>
        public void SaveItemsFromUI() {
            if (OldDuchestUI.Instance == null) return;

            storedItems = OldDuchestUI.Instance.GetStoredItems();
            SendData();
        }

        /// <summary>
        /// 更新图格动画帧
        /// </summary>
        private void UpdateTileFrame(bool open) {
            if (!VaultUtils.SafeGetTopLeft(Position.X, Position.Y, out var topLeft)) {
                return;
            }

            int frameOffset = open ? 1 : 0;
            int frameHeight = 4 * 18; //4格高度

            for (int i = 0; i < 6; i++) {
                for (int j = 0; j < 4; j++) {
                    Tile tile = Framing.GetTileSafely(topLeft.X + i, topLeft.Y + j);
                    if (tile.HasTile && tile.TileType == TargetTileID) {
                        tile.TileFrameY = (short)(j * 18 + frameOffset * frameHeight);
                    }
                }
            }

            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(Main.myPlayer, topLeft.X, topLeft.Y, 6, 4);
            }
        }

        public override void OnKill() {
            //关闭UI
            if (isOpen && OldDuchestUI.Instance.CurrentChest == this) {
                OldDuchestUI.Instance.Close();
                SoundEngine.PlaySound(CWRSound.OldDuchestClose with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 });
            }

            //掉落物品
            if (!VaultUtils.isClient) {
                DropItems();
            }

            storedItems.Clear();
        }

        /// <summary>
        /// 掉落所有物品
        /// </summary>
        private void DropItems() {
            VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, new Item(ModContent.ItemType<OldDuchest>()));
            foreach (var item in storedItems) {
                if (!item.Alives()) {
                    continue;
                }

                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, item.Clone());
            }
        }

        /// <summary>
        /// 检查箱子是否在营地内
        /// </summary>
        private void CheckIfInCampsite() {
            if (!OldDukeCampsite.IsGenerated) {
                isInCampsite = false;
                return;
            }

            Vector2 campsitePos = OldDukeCampsite.CampsitePosition;
            float distance = Vector2.Distance(CenterInWorld, campsitePos);
            isInCampsite = distance < 600f;
        }

        /// <summary>
        /// 初始化营地箱子内容
        /// </summary>
        private void InitializeCampsiteChest() {
            if (!isInCampsite) {
                return;
            }

            int currentCycle = OldDuchestLootGenerator.GetGameTimeSeed();
            if (lastRefreshCycle != currentCycle) {
                RefreshLoot(currentCycle);
            }
        }

        /// <summary>
        /// 刷新战利品
        /// </summary>
        private void RefreshLoot(int refreshCycle) {
            if (VaultUtils.isClient) {
                return;
            }
            storedItems.Clear();
            storedItems = Campsites.OldDuchestLootGenerator.GenerateDailyLoot();
            lastRefreshCycle = refreshCycle;
            hasBeenOpened = false;
            SendData();
        }

        /// <summary>
        /// 触发存入物品的动画效果
        /// 短暂打开箱子然后自动关闭
        /// </summary>
        public void TriggerDepositAnimation() {
            if (isOpen) {
                return;
            }

            //设置关闭计时器，触发开关动画
            if (closeTimer <= 0) {
                closeTimer = 45;//约0.75秒后自动关闭
                UpdateTileFrame(true);
                SoundEngine.PlaySound(CWRSound.OldDuchestOpen with { Volume = 0.5f, Pitch = isUnderwater ? -0.4f : 0.1f }, CenterInWorld);
            }
        }
    }
}
