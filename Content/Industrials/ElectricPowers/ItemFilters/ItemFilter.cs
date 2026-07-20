using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 物品过滤板：过滤名单的手持载体，可随身编辑并把名单安装到收集器、物流管道等机器上<br/>
    /// 名单数据直接存放在 ModItem 实例上(存档/网络走 <see cref="SaveData"/>/<see cref="NetSend"/> 一族钩子)，
    /// 多人模式下本地编辑通过 <see cref="MessageID.SyncEquipment"/> 同步背包物品
    /// </summary>
    internal class ItemFilter : ModItem, IItemFilterHost
    {
        public override string Texture => CWRConstant.ElectricPowers + "ItemFilter";

        public static LocalizedText StateFormat { get; private set; }
        public static LocalizedText CopiedFromChest { get; private set; }
        public static LocalizedText CopiedFromCollector { get; private set; }

        /// <summary>过滤名单数据本体</summary>
        internal ItemFilterSet Filter { get; private set; } = new();

        #region IItemFilterHost

        ItemFilterSet IItemFilterHost.Filter => Filter;

        public string FilterHostName => Item.Name;

        /// <summary>卡片必须仍在本地玩家的背包(或光标)中，编辑器才允许继续编辑</summary>
        public bool FilterHostAlive {
            get {
                if (Item == null || Item.IsAir || Item.type != Type) {
                    return false;
                }
                if (ReferenceEquals(Main.mouseItem, Item)) {
                    return true;
                }
                Item[] inventory = Main.LocalPlayer.inventory;
                for (int i = 0; i < inventory.Length; i++) {
                    if (ReferenceEquals(inventory[i], Item)) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 名单被本地修改后同步背包物品到服务器(服务器会自动转发给其他客户端)，
        /// 这样机器在服务端安装名单时读到的即是最新数据
        /// </summary>
        public void OnFilterChanged() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            int slot = FindLocalInventorySlot();
            if (slot >= 0) {
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, Main.myPlayer, slot);
            }
        }

        private int FindLocalInventorySlot() {
            if (ReferenceEquals(Main.mouseItem, Item)) {
                return PlayerItemSlotID.InventoryMouseItem;
            }
            Item[] inventory = Main.LocalPlayer.inventory;
            for (int i = 0; i < inventory.Length; i++) {
                if (ReferenceEquals(inventory[i], Item)) {
                    return PlayerItemSlotID.Inventory0 + i;
                }
            }
            return -1;
        }

        #endregion

        public override void SetStaticDefaults() {
            StateFormat = this.GetLocalization(nameof(StateFormat), () => "{0} · 已收录 {1} 项");
            CopiedFromChest = this.GetLocalization(nameof(CopiedFromChest), () => "已复制箱内物品");
            CopiedFromCollector = this.GetLocalization(nameof(CopiedFromCollector), () => "已复制收集器名单");
        }

        public override void SetDefaults() {
            Item.width = Item.height = 64;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.maxStack = 1; //防止堆叠导致数据混乱
        }

        //ModItem 默认克隆是浅拷贝，必须深拷贝名单避免引用粘连
        public override ModItem Clone(Item newEntity) {
            ItemFilter clone = (ItemFilter)base.Clone(newEntity);
            clone.Filter = new ItemFilterSet();
            clone.Filter.CopyFrom(Filter);
            return clone;
        }

        #region 使用交互

        /// <summary>对准收集器使用：把机器名单复制到卡上</summary>
        private bool TryCopyFromCollector(Player player) {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!TileProcessorLoader.AutoPositionGetTP<CollectorTP>(point16, out var collectorTP)) {
                return false;
            }

            Filter.CopyFrom(collectorTP.Filter);
            OnFilterChanged();
            SoundEngine.PlaySound(CWRSound.Select);
            CombatText.NewText(player.Hitbox, ItemFilterTheme.AccentWhitelist, CopiedFromCollector.Value);
            return true;
        }

        /// <summary>对准箱子使用：把箱内物品类型复制为名单内容(保留当前黑白名单模式)</summary>
        private bool TryCopyFromChest(Player player) {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!VaultUtils.SafeGetTopLeft(point16, out var newPoint)) {
                return false;
            }

            int chestIndex = Chest.FindChest(newPoint.X, newPoint.Y);
            if (chestIndex == -1) {
                //此处必须放行:任何实心物块都能取到左上角坐标，
                //吞掉点击会导致对着地形使用时编辑器永远无法打开(旧版顽疾)
                return false;
            }

            Chest chest = Main.chest[chestIndex];
            HashSet<int> chestItemTypes = [];
            foreach (var item in chest.item) {
                if (item.type > ItemID.None && item.stack > 0) {
                    chestItemTypes.Add(item.type);
                }
            }

            Filter.CopyFrom(chestItemTypes, Filter.Mode);
            OnFilterChanged();
            SoundEngine.PlaySound(CWRSound.Select);
            CombatText.NewText(player.Hitbox, ItemFilterTheme.AccentWhitelist, CopiedFromChest.Value);
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }

            ItemFilterEditorUI editor = ItemFilterEditorUI.Instance;
            //点击落在编辑器面板上时不响应挥动
            if (editor == null || editor.hoverInMainPage) {
                return true;
            }

            if (TryCopyFromCollector(player)) {
                return true;
            }

            if (TryCopyFromChest(player)) {
                return true;
            }

            editor.ToggleFor(this);
            return true;
        }

        public override bool ConsumeItem(Player player) => false;

        #endregion

        #region 存档与网络

        public override void SaveData(TagCompound tag) => Filter.Save(tag, "Filter");

        public override void LoadData(TagCompound tag) => Filter.TryLoad(tag, "Filter");

        public override void NetSend(BinaryWriter writer) => Filter.Write(writer);

        public override void NetReceive(BinaryReader reader) => Filter.Read(reader);

        #endregion

        #region 展示

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            string modeName = Filter.Mode == ItemFilterMode.Whitelist
                ? ItemFilterEditorUI.ModeWhitelistText?.Value ?? "白名单"
                : ItemFilterEditorUI.ModeBlacklistText?.Value ?? "黑名单";

            tooltips.Add(new TooltipLine(Mod, "CWRFilterState", StateFormat.Format(modeName, Filter.Count)) {
                OverrideColor = Filter.Mode == ItemFilterMode.Whitelist
                    ? ItemFilterTheme.AccentWhitelist
                    : ItemFilterTheme.AccentBlacklist
            });
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            int count = Filter.Count;
            if (count <= 0) {
                return;
            }

            //右下角计数角标，替代旧版会溢出到相邻物品格的环形展示
            string text = count > 99 ? "99+" : count.ToString();
            Color badgeColor = Filter.Mode == ItemFilterMode.Whitelist
                ? ItemFilterTheme.AccentWhitelist
                : ItemFilterTheme.AccentBlacklist;
            Vector2 textSize = FontAssets.ItemStack.Value.MeasureString(text) * 0.75f * scale;
            Vector2 textPos = position + new Vector2(14f, 16f) * scale - textSize * 0.5f;
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, text
                , textPos.X, textPos.Y, badgeColor, Color.Black * 0.9f, Vector2.Zero, 0.75f * scale);
        }

        #endregion

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
                AddIngredient(ItemID.Chest, 4).
                AddIngredient(CWRID.Item_DubiousPlating, 2).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 2).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                    AddRecipeGroup(RecipeGroupID.IronBar, 5).
                    AddIngredient(ItemID.Chest, 4).
                    AddTile(TileID.Anvils).
                    Register();
            }
        }
    }

    /// <summary>
    /// 旧存档迁移垫片：历史版本的名单数据由同名 GlobalItem 以 <c>_Items</c> 键保存，
    /// 类名必须保持 ItemFilterData 才能命中旧存档中的全局数据条目<br/>
    /// 本类不再保存任何数据(数据已迁入 <see cref="ItemFilter"/> 本体)，旧物品再次存档后即自然退役
    /// </summary>
    internal class ItemFilterData : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => entity.type == ModContent.ItemType<ItemFilter>();

        public override void LoadData(Item item, TagCompound tag) {
            if (item.ModItem is not ItemFilter card) {
                return;
            }
            //仅在新格式数据缺席时回填，避免覆盖已迁移的名单
            if (card.Filter.IsEmpty && tag.TryGet("_Items", out int[] legacyItems)) {
                card.Filter.CopyFrom(legacyItems, ItemFilterMode.Whitelist);
            }
        }
    }
}
