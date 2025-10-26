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
        // 使用 List 代替 HashSet 以保证顺序一致性
        internal List<int> Items = [];
        
        // 版本号用于追踪数据变更
        internal int DataVersion = 0;
        
        public override bool InstancePerEntity => true;
        
        public override GlobalItem Clone(Item from, Item to) {
            ItemFilterData itemFilterData = (ItemFilterData)base.Clone(from, to);
            // 深拷贝列表，避免引用粘连
            itemFilterData.Items = new List<int>(Items);
            itemFilterData.DataVersion = DataVersion;
            return itemFilterData;
        }
        
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) {
            return entity.type == ModContent.ItemType<ItemFilter>();
        }
        
        /// <summary>
        /// 线程安全地添加物品ID
        /// </summary>
        public bool TryAddItem(int itemID) {
            if (itemID <= ItemID.None) return false;
            if (Items.Contains(itemID)) return false;
            
            Items.Add(itemID);
            DataVersion++;
            return true;
        }
        
        /// <summary>
        /// 线程安全地移除物品ID
        /// </summary>
        public bool TryRemoveItem(int itemID) {
            bool removed = Items.Remove(itemID);
            if (removed) {
                DataVersion++;
            }
            return removed;
        }
        
        /// <summary>
        /// 批量设置物品（原子操作）
        /// </summary>
        public void SetItems(IEnumerable<int> newItems) {
            Items.Clear();
            Items.AddRange(newItems.Where(id => id > ItemID.None).Distinct());
            DataVersion++;
        }
        
        public override void NetSend(Item item, BinaryWriter writer) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            writer.Write(DataVersion);
            writer.Write(Items.Count);
            foreach (int itemID in Items) {
                writer.Write(itemID);
            }
        }
        
        public override void NetReceive(Item item, BinaryReader reader) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            int receivedVersion = reader.ReadInt32();
            int count = reader.ReadInt32();
            List<int> receivedItems = new(count);
            
            for (int i = 0; i < count; i++) {
                receivedItems.Add(reader.ReadInt32());
            }
            
            // 只有当接收到的版本更新时才更新数据
            if (receivedVersion > DataVersion) {
                Items = receivedItems;
                DataVersion = receivedVersion;
            }
        }
        
        public override void SaveData(Item item, TagCompound tag) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            Items ??= [];
            tag["_Items"] = Items.ToArray();
            tag["_DataVersion"] = DataVersion;
        }
        
        public override void LoadData(Item item, TagCompound tag) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            if (tag.TryGet<int[]>("_Items", out var value)) {
                Items = new List<int>(value);
            }
            else {
                Items = [];
            }
            
            if (tag.TryGet<int>("_DataVersion", out int version)) {
                DataVersion = version;
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
            Item.maxStack = 1; // 防止堆叠导致数据混乱
        }
        
        public static ItemFilterData GetData(Item item) => item.GetGlobalItem<ItemFilterData>();
        
        /// <summary>
        /// 从收集器复制过滤列表
        /// </summary>
        private bool HandleCollectorBehavior() {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!TileProcessorLoader.AutoPositionGetTP<CollectorTP>(point16, out var collectorTP)) {
                return false;
            }

            SoundEngine.PlaySound(CWRSound.Select);

            var data = GetData(Item);
            var sourceData = GetData(collectorTP.ItemFilter);
            
            // 原子操作：一次性设置所有物品
            data.SetItems(sourceData.Items);
            
            // 网络同步
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, Item.whoAmI);
            }

            return true;
        }
        
        /// <summary>
        /// 从箱子复制物品列表
        /// </summary>
        private bool HandleChestBehavior() {
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
            
            // 提取箱子中的所有物品类型
            HashSet<int> chestItemTypes = [];
            foreach (var item in chest.item) {
                if (item.type > ItemID.None) {
                    chestItemTypes.Add(item.type);
                }
            }
            
            // 原子操作
            data.SetItems(chestItemTypes);

            return true;
        }
        
        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }
            
            if (ItemFilterUI.Instance.hoverInMainPage) {
                return true;
            }
            
            if (HandleCollectorBehavior()) {
                ItemFilterUI.Instance.Initialize();
                return true;
            }
            
            if (HandleChestBehavior()) {
                ItemFilterUI.Instance.Initialize();
                return true;
            }
            
            ItemFilterUI.Instance.Active = !ItemFilterUI.Instance.Active;
            ItemFilterUI.Instance.ItemFilter = Item;//这里不要赋值克隆版本的物品，否则数据不同步
            SoundEngine.PlaySound(SoundID.MenuOpen);
            return true;
        }
        
        public override bool ConsumeItem(Player player) => false;
        
        public override bool CanRightClick() => Main.mouseItem.type > ItemID.None;
        
        public override void RightClick(Player player) {
            if (Main.mouseItem.type <= ItemID.None) return;
            
            var data = GetData(Item);
            if (data.TryAddItem(Main.mouseItem.type)) {
                // 同步到网络
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, Item.whoAmI);
                }
            }
        }
        
        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            
            var filterItems = GetData(Item).Items;
            if (filterItems.Count == 0) {
                return;
            }

            const float displayRadius = 35f;
            float angleIncrement = MathHelper.TwoPi / filterItems.Count;
            Vector2 drawCenter = position;

            for (int i = 0; i < filterItems.Count; i++) {
                int itemType = filterItems[i];
                if (itemType <= ItemID.None) continue;

                float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * displayRadius;
                Vector2 itemPos = drawCenter + offset;

                VaultUtils.SafeLoadItem(itemType);
                VaultUtils.SimpleDrawItem(spriteBatch, itemType, itemPos, itemWidth: 26, scale * 0.75f, 0, Color.White);
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
            sengs = Math.Min(sengs + 0.1f, 1f);
            
            UIHitBox = DrawPosition.GetRectangle((int)(60 * sengs));
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;
            
            if (hoverInMainPage) {
                ItemFilterUI.Instance.hoverSlotIndex = slotIndex;
                hoverSengs = Math.Min(hoverSengs + 0.1f, 1f);

                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    ItemFilterUI.Instance.RemoveSlot(this);
                }
            }
            else {
                hoverSengs = Math.Max(hoverSengs - 0.1f, 0f);
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch) {
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, 
                    Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.1f, Vector2.Zero);
            }
            
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, 
                Color.Azure, Color.Aqua, 1, Vector2.Zero);

            VaultUtils.SafeLoadItem(Item);
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;
            VaultUtils.SimpleDrawItem(spriteBatch, Item, DrawPosition + UIHitBox.Size() / 2, 
                40, 1.2f + hoverSengs * 0.2f, 0f, itemColor);
        }
    }

    internal class ItemFilterUI : UIHandle
    {
        [VaultLoaden(CWRConstant.UI + "ItemFilterUI")]
        internal static Texture2D UITex = null;
        
        private bool CanOpen;
        internal float sengs;
        internal float hoverSengs;
        internal Item ItemFilter;
        internal float hoverSlotIndex;
        internal const int RowNum = 6;
        internal const int MaxSlots = RowNum * RowNum;
        
        internal static ItemFilterUI Instance => UIHandleLoader.GetUIHandleOfType<ItemFilterUI>();
        internal List<ItemFilterSlot> Slots = [];
        
        // 缓存上次的数据版本，避免重复初始化
        private int lastDataVersion = -1;
        
        public override bool Active {
            get => CanOpen || sengs > 0;
            set => CanOpen = value;
        }
        
        /// <summary>
        /// 添加槽位（带验证）
        /// </summary>
        internal ItemFilterSlot AddSlot(int itemID) {
            if (itemID <= ItemID.None) return null;
            if (Slots.Any(s => s.Item == itemID)) return null;
            if (Slots.Count >= MaxSlots) return null;
            
            ItemFilterSlot slot = new ItemFilterSlot {
                Item = itemID
            };
            Slots.Add(slot);
            return slot;
        }
        
        /// <summary>
        /// 移除槽位并同步数据
        /// </summary>
        internal void RemoveSlot(ItemFilterSlot slot) {
            if (Slots.Remove(slot)) {
                var data = ItemFilter.GetGlobalItem<ItemFilterData>();
                data.TryRemoveItem(slot.Item);
                
                // 网络同步
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, ItemFilter.whoAmI);
                }
            }
        }
        
        /// <summary>
        /// 初始化UI槽位
        /// </summary>
        public void Initialize() {
            if (ItemFilter == null || ItemFilter.IsAir) return;
            
            var data = ItemFilter.GetGlobalItem<ItemFilterData>();
            // 只有数据版本变化时才重新初始化
            if (data.DataVersion == lastDataVersion && Slots.Count == data.Items.Count) {
                return;
            }

            Slots.Clear();
            foreach (var itemID in data.Items) {
                AddSlot(itemID);
            }
            
            lastDataVersion = data.DataVersion;
        }
        
        public override void Update() {
            if (CanOpen) {
                if (sengs == 0f) {
                    Initialize();
                }

                if (sengs < 0.2f) {
                    DrawPosition = MousePosition - UITex.Size() / 2f;
                }

                sengs = Math.Min(sengs + 0.1f, 1f);
            }
            else {
                sengs = Math.Max(sengs - 0.1f, 0f);
            }

            hoverSlotIndex = -1;

            UIHitBox = DrawPosition.GetRectangle((int)(420 * sengs));
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - UIHitBox.Width);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - UIHitBox.Height);

            // 更新所有槽位
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
                hoverSengs = Math.Min(hoverSengs + 0.1f, 1f);

                // 添加物品到过滤器
                if (keyLeftPressState == KeyPressState.Pressed && Main.mouseItem.type > ItemID.None && hoverSlotIndex == -1) {
                    if (Slots.Count < MaxSlots) {
                        var data = ItemFilter.GetGlobalItem<ItemFilterData>();
                        if (data.TryAddItem(Main.mouseItem.type)) {
                            SoundEngine.PlaySound(SoundID.Grab);
                            AddSlot(Main.mouseItem.type);
                        }
                    }
                }
            }
            else {
                hoverSengs = Math.Max(hoverSengs - 0.1f, 0f);
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch) {
            if (hoverSengs > 0) {
                spriteBatch.Draw(UITex, DrawPosition + UITex.Size() / 2f, null, 
                    Color.BlueViolet with { A = 0 } * hoverSengs, 0, UITex.Size() / 2f, 
                    1f + 0.02f * hoverSengs, SpriteEffects.None, 0);
            }
            
            spriteBatch.Draw(UITex, DrawPosition + UITex.Size() / 2f, null, Color.White * sengs, 
                0, UITex.Size() / 2f, sengs, SpriteEffects.None, 0);
            
            foreach (var slot in Slots) {
                slot.Draw(spriteBatch);
            }
        }
    }
}
