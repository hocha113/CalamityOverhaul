using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class ItemFilterData : GlobalItem
    {
        internal HashSet<int> Items = [];
        public override bool InstancePerEntity => true;
        public override GlobalItem Clone(Item from, Item to) {
            ItemFilterData itemFilterData = (ItemFilterData)base.Clone(from, to);
            itemFilterData.Items = [.. Items];
            return base.Clone(from, to);
        }
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) {
            return entity.type == ModContent.ItemType<ItemFilter>();
        }
        public override void NetSend(Item item, BinaryWriter writer) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            var data = Items.ToArray();
            writer.Write(data.Length);
            for (int i = 0; i < data.Length; i++) {
                writer.Write(data[i]);
            }
        }
        public override void NetReceive(Item item, BinaryReader reader) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            int count = reader.ReadInt32();
            int[] data = new int[count];
            for (int i = 0; i < count; i++) {
                data[i] = reader.ReadInt32();
            }
            Items = [.. data];
        }
        public override void SaveData(Item item, TagCompound tag) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            Items ??= [];
            tag["_Items"] = Items.ToArray();
        }
        public override void LoadData(Item item, TagCompound tag) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            if (tag.TryGet<int[]>("_Items", out var value)) {
                Items = [.. value];
            }
            else {
                Items = [];
            }
        }
    }

    internal class ItemFilter : ModItem
    {
        public override string Texture => CWRConstant.ElectricPowers + "ItemFilter";
        public static int ID { get; private set; }
        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() {
            Item.width = Item.height = 64;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
        }
        public static HashSet<int> GetData(Item item) => item.GetGlobalItem<ItemFilterData>().Items;
        private bool HandleCollectorBever() {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!TileProcessorLoader.AutoPositionGetTP<CollectorTP>(point16, out var collectorTP)) {
                return false;
            }

            SoundEngine.PlaySound(CWRSound.Select);

            var data = GetData(Item);
            data.Clear();

            foreach (var itemID in GetData(collectorTP.ItemFilter)) {
                if (itemID == ItemID.None) {
                    continue;
                }
                data.Add(itemID);
            }

            return true;
        }
        private bool HandleChestBever() {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!VaultUtils.SafeGetTopLeft(point16, out var newPoint)) {
                return false;
            }

            int chestIndex = Chest.FindChest(newPoint.X, newPoint.Y);
            if (chestIndex == -1) {
                return true;
            }

            SoundEngine.PlaySound(CWRSound.Select);

            Chest chest = Main.chest[chestIndex];
            var data = GetData(Item);
            data.Clear();

            foreach (var item in chest.item) {
                if (item.type == ItemID.None) {
                    continue;
                }
                data.Add(item.type);
            }

            return true;
        }
        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }
            if (ItemFilterUI.Instance.hoverInMainPage) {
                return true;
            }
            if (HandleCollectorBever()) {
                ItemFilterUI.Instance.Initialize();
                return true;
            }
            if (HandleChestBever()) {
                ItemFilterUI.Instance.Initialize();
                return true;
            }
            ItemFilterUI.Instance.Active = !ItemFilterUI.Instance.Active;
            ItemFilterUI.Instance.ItemFilter = Item.Clone();
            SoundEngine.PlaySound(SoundID.MenuOpen);
            return true;
        }
        public override bool ConsumeItem(Player player) {
            return false;
        }
        public override bool CanRightClick() {
            return Main.mouseItem.type > ItemID.None;
        }
        public override void RightClick(Player player) {
            GetData(Item).Add(Main.mouseItem.type);
        }
        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            //获取过滤器中存储的物品列表
            HashSet<int> filterItems = GetData(Item);
            if (filterItems.Count == 0) {
                return;//如果过滤器为空，也无需绘制
            }

            //--- 开始绘制环绕的过滤物品图标 ---
            const float displayRadius = 35f;//定义一个固定的显示半径
            float angleIncrement = MathHelper.TwoPi / filterItems.Count;//计算每个物品之间的角度间隔
            Vector2 drawCenter = position;//以物品自身为中心点

            int i = 0;
            foreach (int itemType in filterItems) {
                if (itemType <= ItemID.None) {
                    i++;
                    continue;//跳过无效的物品ID
                }

                //计算每个物品环绕显示的位置
                float currentAngle = angleIncrement * i - MathHelper.PiOver2;//-Pi/2是为了让第一个物品在正上方
                Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * displayRadius;
                Vector2 itemPos = drawCenter + offset;

                VaultUtils.SafeLoadItem(itemType);
                //因为是在UI上绘制，不需要考虑光照，直接用白色作为绘制颜色
                //并使用一个比主物品稍小的缩放比例
                VaultUtils.SimpleDrawItem(spriteBatch, itemType, itemPos, itemWidth: 26, scale * 0.75f, 0, Color.White);

                i++;
            }
        }
    }

    internal class ItemFilterSlot : UIHandle
    {
        internal int Item;
        internal float sengs;
        internal float hoverSengs;
        internal float slotIndex;
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public override bool Active => true;
        public override void Update() {
            if (sengs < 1f) {
                sengs += 0.1f;
            }
            UIHitBox = DrawPosition.GetRectangle((int)(60 * sengs));
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;
            if (hoverInMainPage) {
                ItemFilterUI.Instance.hoverSlotIndex = slotIndex;
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    ItemFilterUI.Instance.Slots.Remove(this);
                    ItemFilterUI.Instance.Items.Remove(Item);
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.1f, Vector2.Zero);
            }
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Azure, Color.Aqua, 1, Vector2.Zero);

            VaultUtils.SafeLoadItem(Item);
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;
            VaultUtils.SimpleDrawItem(spriteBatch, Item, DrawPosition + UIHitBox.Size() / 2, 1.2f + hoverSengs * 0.2f, 0f, itemColor);
        }
    }

    internal class ItemFilterUI : UIHandle
    {
        private bool CanOpen;
        internal float sengs;
        internal float hoverSengs;
        internal Item ItemFilter;
        internal float hoverSlotIndex;
        internal const int RowNum = 6;
        internal static ItemFilterUI Instance => UIHandleLoader.GetUIHandleOfType<ItemFilterUI>();
        internal ref HashSet<int> Items => ref ItemFilter.GetGlobalItem<ItemFilterData>().Items;
        internal List<ItemFilterSlot> Slots = [];
        public override bool Active {
            get => CanOpen || sengs > 0;
            set {
                CanOpen = value;
            }
        }
        internal ItemFilterSlot AddSlot(int itemID) {
            if (itemID == ItemID.None) {
                return null;
            }
            if (Items.Contains(itemID)) {
                return null;
            }
            if (Slots.Count >= RowNum * RowNum) {
                return null;
            }
            ItemFilterSlot itemFilterSlot = new() {
                Item = itemID
            };
            Slots.Add(itemFilterSlot);
            Items.Add(itemID);
            return itemFilterSlot;
        }
        public void Initialize() {
            Slots.Clear();
            var oldArray = Items.ToArray();
            Items.Clear();
            foreach (var item in oldArray) {
                AddSlot(item);
            }
        }
        public override void Update() {
            if (CanOpen) {
                if (sengs == 0f) {
                    Initialize();
                }

                if (sengs < 0.2f) {
                    DrawPosition = MousePosition;
                }
                
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }
            else {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
            }

            hoverSlotIndex = -1;

            sengs = MathHelper.Clamp(sengs, 0, 1f);

            UIHitBox = DrawPosition.GetRectangle((int)(420 * sengs));
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;


            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - UIHitBox.Width);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - UIHitBox.Height);

            for (int i = 0; i < Slots.Count; i++) {
                ItemFilterSlot slot = Slots[i];
                slot.slotIndex = i;
                slot.DrawPosition = DrawPosition + new Vector2(40, 40) * sengs;
                slot.DrawPosition.X += i % RowNum * slot.UIHitBox.Width * sengs;
                slot.DrawPosition.Y += i / RowNum * slot.UIHitBox.Height * sengs;
                slot.Update();
            }

            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }

                if (keyLeftPressState == KeyPressState.Pressed && Main.mouseItem.type > ItemID.None && hoverSlotIndex == -1) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    AddSlot(Main.mouseItem.type);
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.DraedonContactPanel.Value, 16, UIHitBox, Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.01f, Vector2.Zero);
            }
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.DraedonContactPanel.Value, 16, UIHitBox, Color.Azure, Color.Aqua, 1, Vector2.Zero);
            foreach (var slot in Slots) {
                slot.Draw(spriteBatch);
            }
        }
    }
}
