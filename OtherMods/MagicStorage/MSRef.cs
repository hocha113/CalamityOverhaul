using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using MagicStorage;
using MagicStorage.Common.Systems;
using MagicStorage.Components;
using MagicStorage.UI.States;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.OtherMods.MagicStorage
{
    internal class MSRef
    {
        internal static bool Has => CWRMod.Instance.magicStorage != null && CWRMod.Instance.magicStorage.Version >= new Version(0, 7, 0, 11);
        private static FieldInfo _selectedRecipeField;
        internal static FieldInfo SelectedRecipeField {
            get {
                if (!Has) {
                    return null;
                }

                _selectedRecipeField ??= typeof(CraftingGUI).GetField("selectedRecipe", BindingFlags.Static | BindingFlags.NonPublic);
                return _selectedRecipeField;
            }
        }
        //缓存反射字段，避免每帧查找造成的严重卡顿
        private static FieldInfo _recipePanelField;
        internal static FieldInfo RecipePanelField {
            get {
                if (!Has) {
                    return null;
                }
                _recipePanelField ??= typeof(CraftingUIState).GetField("recipePanel", BindingFlags.Instance | BindingFlags.NonPublic);
                return _recipePanelField;
            }
        }
        private static MethodInfo _depositItemMethod;
        [JITWhenModsEnabled("MagicStorage")]
        internal static MethodInfo DepositItemMethod {
            get {
                if (!Has) {
                    return null;
                }
                _depositItemMethod ??= typeof(TEStorageHeart).GetMethod("DepositItem");
                return _depositItemMethod;
            }
        }
        private static int oldSelectedItemType;
        [JITWhenModsEnabled("MagicStorage")]
        internal static object FindMagicStorage(Item item, Point16 position, int maxFindChestMode) {//所以，对外返回obj，或者是其他不需要引用外部程序集的已有类型，这样才能避免触发编译错误
            if (!Has) {//0.7.0.11
                return null;
            }
            //在一定范围内查找 Magic Storage 的存储核心
            for (int x = position.X - (maxFindChestMode / 16); x <= position.X + (maxFindChestMode / 16); x++) {
                for (int y = position.Y - (maxFindChestMode / 16); y <= position.Y + (maxFindChestMode / 16); y++) {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Point16 checkPos = new Point16(x, y);
                    if (TileEntity.ByPosition.TryGetValue(checkPos, out TileEntity te) && te is TEStorageHeart heart) {
                        //检查安全系统权限
                        if (!SecuritySystem.CanPlayerAccessImmediately(Main.LocalPlayer, -1))
                            continue;

                        //检查存储核心是否还有容量
                        bool hasSpace = false;
                        foreach (var unit in heart.GetStorageUnits()) {
                            if (!unit.Inactive && (unit.HasSpaceInStackFor(item) || !unit.IsFull)) {
                                hasSpace = true;
                                break;
                            }
                        }

                        if (hasSpace)
                            return heart;
                    }
                }
            }

            return null;
        }

        [JITWhenModsEnabled("MagicStorage")]
        public static IEnumerable<Item> GetStoredItems() {
            StoragePlayer storagePlayer = Main.LocalPlayer.GetModPlayer<StoragePlayer>();
            TEStorageHeart heart = storagePlayer.GetStorageHeart();

            if (heart != null) {
                return heart.GetStoredItems();
            }

            return [];
        }

        [JITWhenModsEnabled("MagicStorage")]
        public static long GetItemCount(int itemType) {
            var items = GetStoredItems();
            long count = 0;

            foreach (var item in items) {
                if (item.type == itemType) {
                    count += item.stack;
                }
            }

            return count;
        }

        [JITWhenModsEnabled("MagicStorage")]
        internal static List<Item> GetCraftingAccessItems(Player player) {
            //获取当前玩家的 MagicStorage 玩家实例
            StoragePlayer storagePlayer = player.GetModPlayer<StoragePlayer>();

            //获取当前连接的制作核心实体
            TECraftingAccess craftingAccess = storagePlayer.GetCraftingAccess();

            return craftingAccess.stations;
        }

        [JITWhenModsEnabled("MagicStorage")]
        public static bool TryGetCraftingPagePosition(out Vector2 position, out CalculatedStyle dimensions) {
            position = Vector2.Zero;
            dimensions = default;

            if (!MagicUI.IsCraftingUIOpen() || MagicUI.craftingUI == null)
                return false;

            CraftingUIState craftingUI = (CraftingUIState)MagicUI.craftingUI;
            UIElement recipePanel = (UIElement)RecipePanelField.GetValue(craftingUI);

            if (recipePanel != null) {
                dimensions = recipePanel.GetDimensions();
                position = new Vector2(dimensions.X + dimensions.Width, dimensions.Y);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取 Magic Storage 制作界面当前选中的配方
        /// </summary>
        [JITWhenModsEnabled("MagicStorage")]
        public static Recipe GetSelectedRecipe() {
            if (!MagicUI.IsCraftingUIOpen())
                return null;

            if (SelectedRecipeField == null)
                return null;

            try {
                //因为是静态字段，第一个参数传 null
                return (Recipe)SelectedRecipeField.GetValue(null);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// 获取当前选中配方的结果物品
        /// </summary>
        public static Item GetSelectedRecipeResultItem() {
            if (!Has) {
                return null;
            }
            Recipe recipe = GetSelectedRecipe();
            return recipe?.createItem;
        }

        /// <summary>
        /// 获取当前选中配方的结果物品类型 ID
        /// </summary>
        public static int GetSelectedRecipeResultItemType() {
            if (!Has) {
                return ItemID.None;
            }
            var resultItem = GetSelectedRecipeResultItem();
            return resultItem?.type ?? 0;
        }

        [JITWhenModsEnabled("MagicStorage")]
        private static bool IsCraftingUIOpen() => MagicUI.IsCraftingUIOpen();
        public static bool SafeIsCraftingUIOpen() {
            if (!Has) {
                return false;
            }
            return IsCraftingUIOpen();
        }

        internal static void UpdateUI() {
            if (!Has) {
                return;
            }

            //检查魔法存储的制作界面是否打开
            bool magicStorageOpen = SafeIsCraftingUIOpen();

            if (magicStorageOpen) {
                //获取当前制作站列表
                List<Item> stations = GetCraftingAccessItems(Main.LocalPlayer);
                bool hasSupertable = false;
                int targetType = ModContent.ItemType<TransmutationOfMatterItem>();

                foreach (var item in stations) {
                    if (item.type == targetType) {
                        hasSupertable = true;
                        break;
                    }
                }

                if (hasSupertable) {
                    //如果终焉工作台UI没打开，则打开它
                    if (!SupertableUI.Instance.Active) {
                        SupertableUI.TramTP = null;
                        SupertableUI.Instance.Active = true;
                        if (TryGetCraftingPagePosition(out var pos, out var dimensions)) {
                            SupertableUI.Instance.DrawPosition = pos;
                        }
                    }
                    //如果已经打开，并且来自某个实体，先关闭，防止污染数据
                    else {
                        SupertableUI.TramTP?.CloseUI(Main.LocalPlayer);
                    }

                    //同步配方选择
                    SelectedCrafting();
                }
                else if (SupertableUI.Instance.Active && SupertableUI.TramTP == null) {
                    //如果不包含终焉工作台，且UI是因为联动打开的（TramTP为null），则关闭
                    SupertableUI.Instance.Active = false;
                }
            }
            else if (SupertableUI.Instance.Active && SupertableUI.TramTP == null) {
                //如果魔法存储界面关闭了，且UI是因为联动打开的，则关闭
                SupertableUI.Instance.Active = false;
            }
        }

        private static void SelectedCrafting() {
            //同步配方选择
            if (!SupertableUI.Instance.Active) {
                return;
            }
            Item selectedItem = GetSelectedRecipeResultItem();
            if (selectedItem == null || selectedItem.type <= ItemID.None) {
                return;
            }
            if (oldSelectedItemType == selectedItem.type) {
                return;
            }
            var sidebar = SupertableUI.Instance.SidebarManager;
            if (sidebar == null) {
                return;
            }
            //检查当前选中的配方是否与魔法存储选中的物品一致
            if ((sidebar.SelectedRecipe?.RecipeData.Target) == selectedItem.type) {
                return;
            }
            //查找对应的配方
            for (int i = 0; i < sidebar.RecipeElements.Count; i++) {
                var element = sidebar.RecipeElements[i];
                if (element.RecipeData.Target == selectedItem.type) {
                    //更新选中状态
                    sidebar.SelectedRecipe = element;
                    SupertableUI.Instance.RecipeNavigator?.SetRecipeByData(element.RecipeData);
                    sidebar.ScrollToRecipe(i);
                    break;
                }
            }
            oldSelectedItemType = selectedItem.type;
        }
    }

    internal class MSRefSystem : ModSystem
    {
        public override void UpdateUI(GameTime gameTime) => MSRef.UpdateUI();
    }
}
