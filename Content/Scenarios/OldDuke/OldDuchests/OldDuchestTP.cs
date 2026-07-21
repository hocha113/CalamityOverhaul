using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests.OldDuchestUIs;
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

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests
{
    /// <summary>老箱子TP</summary>
    public class OldDuchestTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<OldDuchestTile>();

        private const int MAX_INTERACTION_DISTANCE = 9000;

        public List<Item> storedItems = new();

        private int glowTimer = 0;
        private float glowIntensity = 0f;
        internal bool isOpen = false;
        private int closeTimer = 0;

        private bool isInCampsite = false;
        private int lastRefreshCycle = -1;
        private bool hasBeenOpened = false;

        public bool isUnderwater = false;

        public override void SetProperty() {
            storedItems = new List<Item>();
        }

        public override void SendData(ModPacket data) {
            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                if (item == null) {
                    ItemIO.Send(new Item(), data, true, true);
                }
                else {
                    ItemIO.Send(item, data, true, true);
                }
            }

            data.Write(isInCampsite);
            data.Write(lastRefreshCycle);
            data.Write(hasBeenOpened);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            int count = reader.ReadInt32();
            storedItems.Clear();
            for (int i = 0; i < count; i++) {
                storedItems.Add(ItemIO.Receive(reader, true, true));
            }

            isInCampsite = reader.ReadBoolean();
            lastRefreshCycle = reader.ReadInt32();
            hasBeenOpened = reader.ReadBoolean();

            SyncItemsToUI();
        }

        public override void SaveData(TagCompound tag) {
            try {
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

                tag["isInCampsite"] = isInCampsite;
                tag["lastRefreshCycle"] = lastRefreshCycle;
                tag["hasBeenOpened"] = hasBeenOpened;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OldDuchestTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                if (!tag.TryGet("itemTags", out List<TagCompound> itemTags)) {
                    return;
                }

                storedItems.Clear();
                foreach (var itemTag in itemTags) {
                    storedItems.Add(CWRSaveData.LoadItemTag(itemTag, $"{nameof(OldDuchestTP)}:itemTags"));
                }

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
            if (isOpen) {
                glowIntensity = Math.Min(1f, glowIntensity + 0.1f);
                glowTimer++;
            }
            else {
                glowIntensity = Math.Max(0f, glowIntensity - 0.05f);
            }

            isUnderwater = CheckChestUnderwater();

            //距离自动关
            if (isOpen && (Main.LocalPlayer.DistanceSQ(CenterInWorld) > MAX_INTERACTION_DISTANCE || OldDuchestUI.Instance.CurrentChest != this)) {
                CloseUI(Main.LocalPlayer);
                if (OldDuchestUI.Instance.CurrentChest == this) {
                    OldDuchestUI.Instance.Close();
                }
                SoundEngine.PlaySound(CWRSound.OldDuchestClose with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 });
            }

            //营地定期刷新
            if (isInCampsite && !isOpen) {
                int currentCycle = OldDuchestLootGenerator.GetGameTimeSeed();
                if (lastRefreshCycle != currentCycle && hasBeenOpened) {
                    RefreshLoot(currentCycle);
                }

                bool updateAdd = false;
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
                            SyncItemsToUI();
                    SendData();
                    if (closeTimer <= 0) {
                        closeTimer = 60;
                        UpdateTileFrame(true);
                        SoundEngine.PlaySound(CWRSound.OldDuchestOpen with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 }, CenterInWorld);
                    }
                }
            }

            if (closeTimer > 0) {
                if (--closeTimer == 0) {
                    UpdateTileFrame(false);
                    SoundEngine.PlaySound(CWRSound.OldDuchestClose with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 }, CenterInWorld);
                }
            }

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

            if (item.IsACoin) {
                return MergeCoins(item);
            }

            bool changed = false;

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

            for (int i = storedItems.Count - 1; i >= 0; i--) {
                if (storedItems[i].IsACoin) {
                    totalValue += GetCoinValue(storedItems[i].type) * storedItems[i].stack;
                    coinsInChest.Add(storedItems[i]);
                    storedItems.RemoveAt(i);
                }
            }

            List<Item> newCoins = CoinsFromValue(totalValue);

            if (storedItems.Count + newCoins.Count <= 240) {
                storedItems.AddRange(newCoins);
                item.stack = 0;
                return true;
            }
            else {
                //空间不足回滚；钱币顺序无所谓
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

        private bool CheckChestUnderwater() {
            Point tileCoord = (CenterInWorld / 16).ToPoint();

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

        public void OpenUI(Player player) {
            if (player == null || !player.active) return;

            isOpen = true;

            if (isInCampsite) {
                hasBeenOpened = true;
                SendData();
            }

            //水下开箱泡泡
            if (isUnderwater) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Pitch = -0.1f,
                    Volume = 0.7f
                }, CenterInWorld);

                SpawnOpenBubbles();
            }

            UpdateTileFrame(true);
        }

        private void SpawnOpenBubbles() {
            if (VaultUtils.isServer) {
                return;
            }

            //15-25泡泡
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

            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = new Vector2(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-3f, -1f)
                );
                Dust.NewDust(CenterInWorld - new Vector2(32, 16), 64, 32,
                    DustID.Water, dustVel.X, dustVel.Y, 100, default, 1.5f);
            }
        }

        public void CloseUI(Player player) {
            if (player == null) return;

            isOpen = false;

            if (OldDuchestUI.Instance.CurrentChest == this) {
                SaveItemsFromUI();
            }

            UpdateTileFrame(false);
        }

        public void SyncItemsToUI() {
            if (OldDuchestUI.Instance == null) return;

            OldDuchestUI.Instance.LoadItems(storedItems);
        }

        public void SaveItemsFromUI() {
            if (OldDuchestUI.Instance == null) return;

            storedItems = OldDuchestUI.Instance.GetStoredItems();
            SendData();
        }

        private void UpdateTileFrame(bool open) {
            if (!VaultUtils.SafeGetTopLeft(Position.X, Position.Y, out var topLeft)) {
                return;
            }

            int frameOffset = open ? 1 : 0;
            int frameHeight = 4 * 18;//4格高

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
            if (isOpen && OldDuchestUI.Instance.CurrentChest == this) {
                OldDuchestUI.Instance.Close();
                SoundEngine.PlaySound(CWRSound.OldDuchestClose with { Volume = 0.6f, Pitch = isUnderwater ? -0.4f : 0 });
            }

            if (!VaultUtils.isClient) {
                DropItems();
            }

            storedItems.Clear();
        }

        private void DropItems() {
            VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, new Item(ModContent.ItemType<OldDuchest>()));
            foreach (var item in storedItems) {
                if (!item.Alives()) {
                    continue;
                }

                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, item.Clone());
            }
        }

        private void CheckIfInCampsite() {
            if (!OldDukeCampsite.IsGenerated) {
                isInCampsite = false;
                return;
            }

            Vector2 campsitePos = OldDukeCampsite.CampsitePosition;
            float distance = Vector2.Distance(CenterInWorld, campsitePos);
            isInCampsite = distance < 600f;
        }

        private void InitializeCampsiteChest() {
            if (!isInCampsite) {
                return;
            }

            int currentCycle = OldDuchestLootGenerator.GetGameTimeSeed();
            if (lastRefreshCycle != currentCycle) {
                RefreshLoot(currentCycle);
            }
        }

        private void RefreshLoot(int refreshCycle) {
            if (VaultUtils.isClient) {
                return;
            }
            storedItems.Clear();
            storedItems = OldDuchestLootGenerator.GenerateDailyLoot();
            lastRefreshCycle = refreshCycle;
            hasBeenOpened = false;
            SendData();
        }

        /// <summary>存入时短暂开关动画</summary>
        public void TriggerDepositAnimation() {
            if (isOpen) {
                return;
            }

            if (closeTimer <= 0) {
                closeTimer = 45;//约0.75秒后自动关闭
                UpdateTileFrame(true);
                SoundEngine.PlaySound(CWRSound.OldDuchestOpen with { Volume = 0.5f, Pitch = isUnderwater ? -0.4f : 0.1f }, CenterInWorld);
            }
        }
    }
}
