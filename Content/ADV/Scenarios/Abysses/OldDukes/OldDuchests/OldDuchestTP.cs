using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests.OldDuchestUIs;
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
    internal class OldDuchestTP : TileProcessor
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

        //每日刷新相关
        private bool isInCampsite = false;
        private int lastRefreshCycle = -1;
        private bool hasBeenOpened = false;

        public override void SetProperty() {
            storedItems = new List<Item>();
        }

        public override void SendData(ModPacket data) {
            //发送存储物品数据
            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                if (item == null) {
                    ItemIO.Send(new Item(), data, true);
                }
                else {
                    ItemIO.Send(item, data, true);
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
                Item item = ItemIO.Receive(reader, true);
                storedItems.Add(item);
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

            //检查距离自动关闭
            if (isOpen && Main.LocalPlayer.DistanceSQ(CenterInWorld) > MAX_INTERACTION_DISTANCE) {
                CloseUI(Main.LocalPlayer);
            }

            //营地箱子定期刷新检查
            if (isInCampsite && !isOpen) {
                int currentCycle = Campsites.OldDuchestLootGenerator.GetGameTimeSeed();
                if (lastRefreshCycle != currentCycle && hasBeenOpened) {
                    RefreshLoot(currentCycle);
                }
            }

            //更新光照
            if (glowIntensity > 0.01f) {
                float pulsePulse = MathF.Sin(glowTimer * 0.05f) * 0.3f + 0.7f;
                Lighting.AddLight(CenterInWorld,
                    new Color(139, 87, 42).ToVector3() * glowIntensity * pulsePulse);
            }
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

            SoundEngine.PlaySound(SoundID.MenuOpen with {
                Pitch = -0.2f,
                Volume = 0.6f
            }, CenterInWorld);

            //更新图格帧为打开状态
            UpdateTileFrame(true);
        }

        /// <summary>
        /// 关闭UI
        /// </summary>
        public void CloseUI(Player player) {
            if (player == null) return;

            isOpen = false;
            SoundEngine.PlaySound(SoundID.MenuClose with {
                Pitch = -0.3f,
                Volume = 0.5f
            }, CenterInWorld);

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
            if (!Campsites.OldDukeCampsite.IsGenerated) {
                isInCampsite = false;
                return;
            }

            Vector2 campsitePos = Campsites.OldDukeCampsite.CampsitePosition;
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

            int currentCycle = Campsites.OldDuchestLootGenerator.GetGameTimeSeed();
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
    }
}
