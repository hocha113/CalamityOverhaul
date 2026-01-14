using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.TileProcessors;
using CalamityOverhaul.Content.UIs.SupertableUIs.Inventory;
using CalamityOverhaul.Content.UIs.SupertableUIs.UIContent;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    /// <summary>
    /// 欧米茄物质聚合仪UI
    /// </summary>
    public class SupertableUI : UIHandle, ICWRLoader
    {
        #region 静态数据和实例

        public static SupertableUI Instance => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        public static string[][] RpsDataStringArrays { get; set; } = [];
        public static List<string[]> OtherRpsData_ZenithWorld_StringList { get; set; } = [];
        public static List<string[]> ModCall_OtherRpsData_StringList { get; set; } = [];
        public static List<RecipeData> AllRecipes { get; set; } = [];
        public static int AllRecipesVanillaContentCount { get; private set; }
        public static TramModuleTP TramTP { get; set; }
        public static Item[] GlobalItems { get; set; }
        #endregion

        #region 核心组件

        internal SupertableController _controller;
        internal RecipeSidebarManager _sidebarManager;
        internal RecipeNavigator _recipeNavigator;
        internal DragController _dragController;
        internal QuickActionsManager _quickActionsManager;

        /// <summary>
        /// 获取配方导航器（供侧边栏等组件使用）
        /// </summary>
        internal RecipeNavigator RecipeNavigator => _recipeNavigator;

        /// <summary>
        /// 获取侧边栏管理器（供配方导航器等组件使用）
        /// </summary>
        internal RecipeSidebarManager SidebarManager => _sidebarManager;

        #endregion

        #region UI状态字段

        /// <summary>
        /// 获取当前UI中的物品数组
        /// </summary>
        public Item[] Items {
            get => _controller.SlotManager.Slots;
            set => _controller.SlotManager.Slots = value;
        }

        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MainValue");

        private GridCoordinate _hoveredCell;
        private Rectangle _gridRectangle;
        private Rectangle _resultRectangle;

        public bool HoverInPutItemCellPage { get; private set; }
        public bool OnInputSlot { get; private set; }
        public bool OnCloseButton { get; private set; }

        public Vector2 TopLeft => DrawPosition + SupertableConstants.MAIN_UI_OFFSET;

        public override bool Active {
            get => player.CWR().SupertableUIStartBool || _controller.AnimationController.OpenProgress > 0;
            set {
                bool lastValue = player.CWR().SupertableUIStartBool;
                player.CWR().SupertableUIStartBool = value;

                //如果开启UI且没有绑定实体，则加载全局物品
                if (value && !lastValue && TramTP == null) {
                    InstallGlobalItems();
                    Items = GlobalItems;
                }

                //如果设为关闭，请求延迟关闭动画
                if (!value) {
                    _controller.AnimationController.RequestDelayedClose(0);
                    TramTP?.CloseUI(player);
                }

                SyncToNetworkIfNeeded();
            }
        }

        #endregion

        #region 生命周期方法

        void ICWRLoader.SetupData() {//不要试图在这个接口钩子里使用this，对于实例来讲，this只是接口自己new的实例，和UIHandleLoader管理的实例无关
            Instance._controller = new SupertableController();
            Instance._sidebarManager = new RecipeSidebarManager(Instance);
            Instance._recipeNavigator = new RecipeNavigator(Instance, Instance._controller);
            Instance._dragController = new DragController(Instance);
            Instance._quickActionsManager = new QuickActionsManager(Instance, Instance._controller);
            //初始化全局物品存储
            GlobalItems = new Item[SupertableConstants.TOTAL_SLOTS];
            for (int i = 0; i < GlobalItems.Length; i++) {
                GlobalItems[i] = new Item();
            }
            LoadRecipe();

            //初始化侧边栏的配方元素
            Instance._sidebarManager.InitializeRecipeElements();
            Instance._recipeNavigator.LoadAllRecipes();
        }

        void ICWRLoader.UnLoadData() {
            Instance._controller = null;
            Instance._sidebarManager = null;
            Instance._recipeNavigator = null;
            Instance._dragController = null;
            Instance._quickActionsManager = null;
            TramTP = null;
            GlobalItems = null;
            RpsDataStringArrays = [];
            OtherRpsData_ZenithWorld_StringList = [];
            ModCall_OtherRpsData_StringList = [];
            AllRecipes = [];
        }

        private static void InstallGlobalItems() {
            if (GlobalItems == null) {
                //初始化全局物品存储
                GlobalItems = new Item[SupertableConstants.TOTAL_SLOTS];
                for (int i = 0; i < GlobalItems.Length; i++) {
                    GlobalItems[i] = new Item();
                }
            }
        }

        public override void OnEnterWorld() {
            InstallGlobalItems();
            _controller?.AnimationController.ForceClose();
            LoadenWorld();
        }

        public override void SaveUIData(TagCompound tag) {
            tag["SupertableUI_DrawPos_X"] = DrawPosition.X;
            tag["SupertableUI_DrawPos_Y"] = DrawPosition.Y;

            List<TagCompound> itemTags = new List<TagCompound>();
            for (int i = 0; i < GlobalItems.Length; i++) {
                if (GlobalItems[i] == null) {
                    GlobalItems[i] = new Item(0);
                }
                itemTags.Add(ItemIO.Save(GlobalItems[i]));
            }
            tag["GlobalItems"] = itemTags;
        }

        public override void LoadUIData(TagCompound tag) {
            DrawPosition.X = tag.TryGet("SupertableUI_DrawPos_X", out float x) ? x : 500;
            DrawPosition.Y = tag.TryGet("SupertableUI_DrawPos_Y", out float y) ? y : 300;

            if (tag.TryGet("GlobalItems", out List<TagCompound> itemTags)) {
                List<Item> loadedItems = new List<Item>();
                for (int i = 0; i < itemTags.Count; i++) {
                    loadedItems.Add(ItemIO.Load(itemTags[i]));
                }
                GlobalItems = loadedItems.ToArray();
            }
        }

        #endregion

        #region 静态配方加载

        public static void LoadRecipe() {
            Type type = typeof(SupertableRecipeData);
            FieldInfo[] stringArrayFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string[])).ToArray();
            PropertyInfo[] stringArrayProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string[])).ToArray();
            var allMembers = stringArrayFields.Concat<MemberInfo>(stringArrayProperties).ToArray();

            RpsDataStringArrays = allMembers.Select(member => {
                if (member is FieldInfo field)
                    return (string[])field.GetValue(null);
                else if (member is PropertyInfo property)
                    return (string[])property.GetValue(null);
                return null;
            }).Where(array => array != null).ToArray();

            if (ModCall_OtherRpsData_StringList?.Count > 0) {
                RpsDataStringArrays = RpsDataStringArrays.Concat(ModCall_OtherRpsData_StringList).ToArray();
            }

            AllRecipes.Clear();
            foreach (string[] value in RpsDataStringArrays) {
                if (!RecipeUtilities.ValidateRecipeData(value)) {
                    string pag = string.Join(", ", value);
                    throw new InvalidOperationException($"Invalid recipe data: {pag}");
                }

                string targetItemFullName = value[^1];
                int targetItemID = VaultUtils.GetItemTypeFromFullName(targetItemFullName);

                RecipeData recipeData = new RecipeData {
                    Values = value,
                    Target = targetItemID
                };
                recipeData.BuildMaterialTypesCache();
                AllRecipes.Add(recipeData);
            }

            AllRecipesVanillaContentCount = AllRecipes.Count;
            CWRMod.Instance.Logger.Info($"Loaded recipe count: {AllRecipesVanillaContentCount}");

            //初始化控制器的配方引擎
            Instance?._controller?.InitializeRecipes(AllRecipes);
        }

        public static void LoadenWorld() {
            if (AllRecipes.Count > AllRecipesVanillaContentCount) {
                AllRecipes.RemoveRange(AllRecipesVanillaContentCount, AllRecipes.Count - AllRecipesVanillaContentCount);
            }
            SetZenithWorldRecipesData();

            //重新初始化侧边栏配方元素
            Instance?._sidebarManager?.InitializeRecipeElements();
            Instance?._recipeNavigator?.LoadAllRecipes();
        }

        public static void SetZenithWorldRecipesData() {
            if (Main.zenithWorld) {
                foreach (var recipes in OtherRpsData_ZenithWorld_StringList) {
                    RecipeData.Register(recipes);
                }
            }
        }
        #endregion

        #region 更新逻辑

        private int _autoSaveTimer;
        private const int AUTO_SAVE_INTERVAL = 300; //每300帧（5秒）自动保存一次

        public override void Update() {
            if (_controller == null) return;

            UpdateUIPositions();
            UpdateHoveredCell();

            int hoveredIndex = _hoveredCell.IsValid() ? _hoveredCell.ToIndex() : -1;
            //这里用 player.CWR().SupertableUIStartBool 而不是他妈的 Active
            _controller.UpdateAnimations(player.CWR().SupertableUIStartBool, hoveredIndex);

            //先处理拖拽，因为它可能会改变DrawPosition
            _dragController?.Update();

            //如果正在拖拽，占用鼠标接口
            if (_dragController != null && _dragController.IsDragging) {
                player.mouseInterface = true;
            }

            _sidebarManager?.Update();
            _recipeNavigator?.Update();
            _quickActionsManager?.Update();
            //再更新一次基础数据，确保前面的更改被应用了
            UpdateUIPositions();
            UpdateHoveredCell();

            //然后处理其他输入
            HandleInput();

            //定期自动保存到TileProcessor
            if (Active && TramTP != null) {
                _autoSaveTimer++;
                if (_autoSaveTimer >= AUTO_SAVE_INTERVAL) {
                    _autoSaveTimer = 0;
                    TramTP.SaveItemsFromUI();
                }
            }
            else {
                _autoSaveTimer = 0;
            }
        }

        private void UpdateUIPositions() {
            Vector2 topLeft = TopLeft;

            _gridRectangle = new Rectangle(
                (int)topLeft.X,
                (int)topLeft.Y,
                SupertableConstants.CELL_WIDTH * SupertableConstants.GRID_COLUMNS,
                SupertableConstants.CELL_HEIGHT * SupertableConstants.GRID_ROWS
            );

            _resultRectangle = new Rectangle(
                (int)(DrawPosition.X + SupertableConstants.INPUT_SLOT_OFFSET.X),
                (int)(DrawPosition.Y + SupertableConstants.INPUT_SLOT_OFFSET.Y),
                SupertableConstants.INPUT_SLOT_SIZE,
                SupertableConstants.INPUT_SLOT_SIZE
            );

            UIHitBox = new Rectangle(
                (int)(topLeft.X - SupertableConstants.MAIN_UI_OFFSET.X),
                (int)(topLeft.Y - SupertableConstants.MAIN_UI_OFFSET.Y),
                _gridRectangle.Width + 200,
                _gridRectangle.Height + 44
            );

            Rectangle mouseRec = MouseHitBox;
            hoverInMainPage = UIHitBox.Intersects(mouseRec);
            HoverInPutItemCellPage = _gridRectangle.Intersects(mouseRec);
            OnInputSlot = _resultRectangle.Intersects(mouseRec);
            OnCloseButton = !hoverInMainPage && !player.mouseInterface;
        }

        private void UpdateHoveredCell() {
            if (HoverInPutItemCellPage) {
                _hoveredCell = GridCoordinate.FromScreenPosition(MousePosition, TopLeft);
            }
            else {
                _hoveredCell = new GridCoordinate(-1, -1);
            }
        }

        #endregion

        #region 输入处理

        private void HandleInput() {
            //处理关闭
            if (OnCloseButton) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = SupertableConstants.SOUND_PITCH_CLOSE });
                    //立即关闭UI并触发关闭动画
                    Active = false;
                    _controller.AnimationController.RequestDelayedClose(0); //立即开始关闭动画
                    SyncToNetworkIfNeeded();
                }
                return;
            }

            if (OnInputSlot) {
                player.mouseInterface = true;
                HandleResultSlotInput();
                return;
            }

            if (HoverInPutItemCellPage && _hoveredCell.IsValid()) {
                player.mouseInterface = true;
                HandleGridSlotInput();
            }
        }

        private void HandleResultSlotInput() {
            if (_controller.ResultManager.HasResult) {
                _dragController.SetDontDragTime(2);
            }

            if (keyLeftPressState == KeyPressState.Pressed || keyLeftPressState == KeyPressState.Held) {
                if (_controller.TryTakeResult(ref Main.mouseItem)) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    SoundEngine.PlaySound(SoundID.Research);
                    TramTP?.SendData();
                }
            }
        }

        private void HandleGridSlotInput() {
            int slotIndex = _hoveredCell.ToIndex();
            Item slotItem = _controller.SlotManager.GetSlot(slotIndex);

            KeyboardState keyboard = Keyboard.GetState();
            bool shiftPressed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            if (shiftPressed && keyLeftPressState == KeyPressState.Pressed) {
                ItemInteractionHandler.QuickTransferToInventory(slotItem, player);
                _controller.UpdateRecipeMatching();
                return;
            }

            if (keyLeftPressState == KeyPressState.Pressed) {
                ItemInteractionHandler.HandleLeftClick(ref slotItem, ref Main.mouseItem);
                _controller.SlotManager.SetSlot(slotIndex, slotItem);
                _controller.UpdateRecipeMatching();
            }

            if (keyRightPressState == KeyPressState.Pressed) {
                ItemInteractionHandler.HandleRightClick(ref slotItem, ref Main.mouseItem);
                _controller.SlotManager.SetSlot(slotIndex, slotItem);
                _controller.UpdateRecipeMatching();
            }

            if (keyRightPressState == KeyPressState.Held) {
                ItemInteractionHandler.HandleDragPlace(ref slotItem, ref Main.mouseItem);
                _controller.SlotManager.SetSlot(slotIndex, slotItem);
                _controller.UpdateRecipeMatching();
            }

            if (shiftPressed && Main.mouseItem.type == ItemID.None) {
                ItemInteractionHandler.GatherSameItems(_controller.SlotManager.Slots, slotIndex);
                _controller.UpdateRecipeMatching();
            }
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            float alpha = _controller.AnimationController.OpenProgress;

            _sidebarManager?.Draw(spriteBatch, alpha);

            spriteBatch.Draw(Texture, DrawPosition, null, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            DrawPreviewItems(spriteBatch, alpha * 0.25f);
            DrawSlotItems(spriteBatch, alpha);
            DrawResultSlot(spriteBatch, alpha);
            DrawArrow(spriteBatch, alpha);

            _recipeNavigator?.Draw(spriteBatch, alpha);
            _quickActionsManager?.Draw(spriteBatch, alpha);

            DrawHoverTooltips();
        }

        private void DrawPreviewItems(SpriteBatch spriteBatch, float alpha) {
            for (int i = 0; i < SupertableConstants.TOTAL_SLOTS; i++) {
                Item previewItem = _controller.SlotManager.GetPreviewSlot(i);
                if (previewItem?.type != ItemID.None) {
                    var coord = GridCoordinate.FromIndex(i);
                    Vector2 pos = coord.ToScreenPosition(TopLeft);
                    float scale = 1f + _controller.AnimationController.GetSlotHoverProgress(i) * 0.2f;

                    DrawItemIcon(spriteBatch, previewItem, pos, alpha, scale);
                }
            }
        }

        private void DrawSlotItems(SpriteBatch spriteBatch, float alpha) {
            for (int i = 0; i < SupertableConstants.TOTAL_SLOTS; i++) {
                Item slotItem = _controller.SlotManager.GetSlot(i);
                if (slotItem?.type != ItemID.None) {
                    var coord = GridCoordinate.FromIndex(i);
                    Vector2 pos = coord.ToScreenPosition(TopLeft);
                    float scale = 1f + _controller.AnimationController.GetSlotHoverProgress(i) * 0.2f;

                    DrawItemIcon(spriteBatch, slotItem, pos, alpha, scale);
                }
            }
        }

        private void DrawResultSlot(SpriteBatch spriteBatch, float alpha) {
            if (_controller.ResultManager.HasResult) {
                Item resultItem = _controller.ResultManager.ResultItem;
                Vector2 pos = new Vector2(_resultRectangle.X, _resultRectangle.Y);
                DrawItemIcon(spriteBatch, resultItem, pos, alpha, 1.5f);
            }
        }

        private void DrawArrow(SpriteBatch spriteBatch, float alpha) {
            string arrowPath = _controller.ResultManager.HasResult
                ? "CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow"
                : "CalamityOverhaul/Assets/UIs/SupertableUIs/InputArrow2";

            Texture2D arrow = CWRUtils.GetT2DValue(arrowPath);
            spriteBatch.Draw(arrow, DrawPosition + new Vector2(460, 225), null, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        public static void DrawItemIcon(SpriteBatch spriteBatch, Item item, Vector2 position, float alpha, float scale) {
            if (item == null || item.type == ItemID.None) return;

            int dyeItemID = item.CWR().DyeItemID;
            if (dyeItemID > ItemID.None) {
                Main.LocalPlayer.BeginDyeEffectForUI(dyeItemID);
            }

            Rectangle rectangle = Main.itemAnimations[item.type] != null
                ? Main.itemAnimations[item.type].GetFrame(TextureAssets.Item[item.type].Value)
                : TextureAssets.Item[item.type].Value.Frame(1, 1, 0, 0);

            Vector2 vector = rectangle.Size();
            Vector2 offset = new Vector2(SupertableConstants.CELL_WIDTH, SupertableConstants.CELL_HEIGHT) / 2;
            float drawScale = item.GetDrawItemSize(36) * scale;

            if (item.type == DarkMatterBall.ID) {
                DarkMatterBall.DrawItemIcon(spriteBatch, position + offset, item.type, alpha);
            }
            else {
                Texture2D itemValue = TextureAssets.Item[item.type].Value;
                spriteBatch.Draw(itemValue, position + offset, rectangle, Color.White * alpha, 0f, vector / 2, drawScale, 0, 0f);
            }

            if (dyeItemID > ItemID.None) {
                Main.LocalPlayer.EndDyeEffectForUI();
            }

            if (item.stack > 1) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value,
                    item.stack.ToString(), position.X, position.Y + 25,
                    Color.White, Color.Black, new Vector2(0.3f), scale);
            }
        }

        private void DrawHoverTooltips() {
            if (_hoveredCell.IsValid() && HoverInPutItemCellPage) {
                int slotIndex = _hoveredCell.ToIndex();
                Item slotItem = _controller.SlotManager.GetSlot(slotIndex);

                if (slotItem?.type != ItemID.None) {
                    Main.HoverItem = slotItem.Clone();
                    Main.hoverItemName = slotItem.Name;
                }
                else {
                    Item previewItem = _controller.SlotManager.GetPreviewSlot(slotIndex);
                    if (previewItem?.type != ItemID.None) {
                        Main.HoverItem = previewItem.Clone();
                        Main.hoverItemName = previewItem.Name;
                    }
                }
            }

            if (OnInputSlot && _controller.ResultManager.HasResult) {
                Item resultItem = _controller.ResultManager.ResultItem;
                Main.HoverItem = resultItem.Clone();
                Main.hoverItemName = resultItem.Name;
            }
        }

        #endregion

        #region 网络同步

        public static void SyncToNetworkIfNeeded() {
            if (TramTP != null && TramTP.Active) {
                //定期保存UI中的物品数据回TileProcessor
                TramTP.SaveItemsFromUI();
                TramTP?.SendData();
            }
            else {
                GlobalItems = [.. Instance.Items];
            }
        }

        #endregion

        public Vector2 ArcCellPos(int index) {
            var coord = GridCoordinate.FromIndex(index);
            return coord.ToScreenPosition(TopLeft);
        }
    }
}
