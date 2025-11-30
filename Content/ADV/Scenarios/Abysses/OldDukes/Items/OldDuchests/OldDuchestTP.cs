using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests.OldDuchestUIs;
using InnoVault.TileProcessors;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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
        private const int STORAGE_SLOTS = 240; //20x12格大型存储空间
        private const int MAX_INTERACTION_DISTANCE = 9000;

        //存储物品列表
        public List<Item> storedItems = new();

        //动画相关
        private int glowTimer = 0;
        private float glowIntensity = 0f;
        internal bool isOpen = false;

        public override void SetProperty() {
            storedItems = new List<Item>();
        }

        public override void SendData(ModPacket data) {
            //发送存储物品数据
            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                ItemIO.Send(item, data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            //接收存储物品数据
            int count = reader.ReadInt32();
            storedItems.Clear();
            for (int i = 0; i < count; i++) {
                Item item = ItemIO.Receive(reader, true);
                storedItems.Add(item);
            }
        }

        public override void SaveData(TagCompound tag) {
            //保存存储的物品
            tag["itemCount"] = storedItems.Count;
            for (int i = 0; i < storedItems.Count; i++) {
                tag[$"item{i}_type"] = storedItems[i].type;
                tag[$"item{i}_stack"] = storedItems[i].stack;
                if (storedItems[i].prefix != 0) {
                    tag[$"item{i}_prefix"] = storedItems[i].prefix;
                }
            }
        }

        public override void LoadData(TagCompound tag) {
            //加载存储的物品
            storedItems.Clear();
            int count = tag.GetInt("itemCount");
            for (int i = 0; i < count; i++) {
                if (tag.ContainsKey($"item{i}_type")) {
                    Item item = new Item();
                    item.SetDefaults(tag.GetInt($"item{i}_type"));
                    item.stack = tag.GetInt($"item{i}_stack");
                    if (tag.ContainsKey($"item{i}_prefix")) {
                        item.prefix = tag.GetByte($"item{i}_prefix");
                    }
                    storedItems.Add(item);
                }
            }
        }

        public override void Update() {
            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }

            //更新发光效果
            if (isOpen) {
                glowIntensity = Math.Min(1f, glowIntensity + 0.1f);
                glowTimer++;
            }
            else {
                glowIntensity = Math.Max(0f, glowIntensity - 0.05f);
            }

            //检查距离自动关闭
            if (isOpen && player.DistanceSQ(CenterInWorld) > MAX_INTERACTION_DISTANCE) {
                CloseUI(player);
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
            foreach (var item in storedItems) {
                if (item == null || item.IsAir) {
                    continue;
                }

                int itemIndex = Item.NewItem(
                    new EntitySource_TileBreak(Position.X, Position.Y),
                    CenterInWorld,
                    item.type,
                    item.stack,
                    false,
                    item.prefix
                );

                if (VaultUtils.isServer && itemIndex >= 0) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
                }
            }
        }
    }
}
