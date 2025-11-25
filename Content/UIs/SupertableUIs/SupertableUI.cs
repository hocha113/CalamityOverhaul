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
    /// 超级工作台UI - 重构版本
    /// 使用新的模块化架构，职责分离，易于维护和扩展
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

        #endregion

        #region 核心组件

        private SupertableController _controller;
        private RecipeSidebarManager _sidebarManager;
        private RecipeNavigator _recipeNavigator;
        private DragController _dragController;
        private QuickActionsManager _quickActionsManager;

        #endregion

        #region UI状态字段

        public ref Item[] Items => ref _controller.SlotManager.Slots;

        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/MainValue");

        private GridCoordinate _hoveredCell;
        private Rectangle _gridRectangle;
        private Rectangle _resultRectangle;
        private Rectangle _closeButtonRectangle;

        public bool HoverInPutItemCellPage { get; private set; }
        public bool OnInputSlot { get; private set; }
        public bool OnCloseButton { get; private set; }

        public Vector2 TopLeft => DrawPosition + SupertableConstants.MAIN_UI_OFFSET;

        public override bool Active {
            get => player.CWR().SupertableUIStartBool || _controller.AnimationController.OpenProgress > 0;
            set {
                player.CWR().SupertableUIStartBool = value;
                
                // 如果设置为关闭，立即清除延迟并开始关闭动画
                if (!value) {
                    _controller.AnimationController.RequestDelayedClose(0);
                }

                SyncToNetworkIfNeeded();
            }
        }

        #endregion

        #region 生命周期方法

        void ICWRLoader.SetupData() {
            Instance._controller = new SupertableController();
            Instance._sidebarManager = new RecipeSidebarManager(Instance);
            Instance._recipeNavigator = new RecipeNavigator(Instance, Instance._controller);
            Instance._dragController = new DragController(Instance);
            Instance._quickActionsManager = new QuickActionsManager(Instance, Instance._controller);

            LoadRecipe();
            Instance._recipeNavigator.LoadAllRecipes();
        }

        void ICWRLoader.UnLoadData() {
            Instance._controller = null;
            Instance._sidebarManager = null;
            Instance._recipeNavigator = null;
            Instance._dragController = null;
            Instance._quickActionsManager = null;
            TramTP = null;
            RpsDataStringArrays = [];
            OtherRpsData_ZenithWorld_StringList = [];
            ModCall_OtherRpsData_StringList = [];
            AllRecipes = [];
        }

        public override void OnEnterWorld() {
            _controller?.AnimationController.ForceClose();
        }

        public override void SaveUIData(TagCompound tag) {
            tag["SupertableUI_DrawPos_X"] = DrawPosition.X;
            tag["SupertableUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            DrawPosition.X = tag.TryGet("SupertableUI_DrawPos_X", out float x) ? x : 500;
            DrawPosition.Y = tag.TryGet("SupertableUI_DrawPos_Y", out float y) ? y : 300;
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

        public override void Update() {
            if (_controller == null) return;

            UpdateUIPositions();
            UpdateHoveredCell();

            int hoveredIndex = _hoveredCell.IsValid() ? _hoveredCell.ToIndex() : -1;
            _controller.UpdateAnimations(player.CWR().SupertableUIStartBool, hoveredIndex);

            // 先处理拖拽，因为它可能会改变DrawPosition
            _dragController?.Update();
            
            // 如果正在拖拽，占用鼠标接口
            if (_dragController != null && _dragController.IsDragging)
            {
                player.mouseInterface = true;
            }

            // 然后处理其他输入
            HandleInput();

            _sidebarManager?.Update();
            _recipeNavigator?.Update();
            _quickActionsManager?.Update();
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

            _closeButtonRectangle = new Rectangle(
                (int)DrawPosition.X,
                (int)DrawPosition.Y,
                SupertableConstants.CLOSE_BUTTON_SIZE,
                SupertableConstants.CLOSE_BUTTON_SIZE
            );

            UIHitBox = new Rectangle(
                (int)topLeft.X,
                (int)topLeft.Y,
                _gridRectangle.Width + 200,
                _gridRectangle.Height
            );

            Rectangle mouseRec = MouseHitBox;
            hoverInMainPage = UIHitBox.Intersects(mouseRec);
            HoverInPutItemCellPage = _gridRectangle.Intersects(mouseRec);
            OnInputSlot = _resultRectangle.Intersects(mouseRec);
            OnCloseButton = _closeButtonRectangle.Intersects(mouseRec);
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
            // 处理关闭按钮 - 提高优先级
            if (OnCloseButton) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = SupertableConstants.SOUND_PITCH_CLOSE });
                    // 立即关闭UI并触发关闭动画
                    Active = false;
                    _controller.AnimationController.RequestDelayedClose(0); // 立即开始关闭动画
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

            DrawCloseButton(spriteBatch, alpha);
            DrawPreviewItems(spriteBatch, alpha * 0.25f);
            DrawSlotItems(spriteBatch, alpha);
            DrawResultSlot(spriteBatch, alpha);
            DrawArrow(spriteBatch, alpha);

            _recipeNavigator?.Draw(spriteBatch, alpha);
            _quickActionsManager?.Draw(spriteBatch, alpha);

            DrawHoverTooltips();
        }
        
        private void DrawCloseButton(SpriteBatch spriteBatch, float alpha) {
            Texture2D closeIcon = CWRUtils.GetT2DValue("CalamityMod/UI/DraedonSummoning/DecryptCancelIcon");
            spriteBatch.Draw(closeIcon, DrawPosition, null, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            if (OnCloseButton && alpha >= 1) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value,
                    CWRLocText.GetTextValue("SupertableUI_Text1"),
                    DrawPosition.X, DrawPosition.Y, Color.Gold, Color.Black, new Vector2(0.3f),
                    1.1f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.1f));
            }
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

        private void SyncToNetworkIfNeeded() {
            if (TramTP != null && TramTP.Active) {
                if (TramTP.items == null) {
                    TramTP.items = _controller.SlotManager.Slots;
                }
                else {
                    for (int i = 0; i < _controller.SlotManager.Slots.Length; i++) {
                        _controller.SlotManager.Slots[i] = TramTP.items[i];
                    }
                }

                if (!VaultUtils.isSinglePlayer) {
                    TramTP.SendData();
                }
            }
        }

        #endregion

        public Vector2 ArcCellPos(int index) {
            var coord = GridCoordinate.FromIndex(index);
            return coord.ToScreenPosition(TopLeft);
        }
    }
}
