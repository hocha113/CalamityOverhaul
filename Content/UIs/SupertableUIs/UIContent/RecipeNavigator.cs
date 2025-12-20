using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.UIContent
{
    /// <summary>
    /// 配方导航器，用于浏览和预览配方
    /// </summary>
    internal class RecipeNavigator
    {
        [VaultLoaden("CalamityOverhaul/Assets/UIs/SupertableUIs/")]
        private static Texture2D BlueArrow = null;
        [VaultLoaden("CalamityOverhaul/Assets/UIs/SupertableUIs/")]
        private static Texture2D BlueArrow2 = null;
        [VaultLoaden("CalamityOverhaul/Assets/UIs/SupertableUIs/")]
        private static Texture2D RecPBook = null;

        private readonly SupertableUI _mainUI;
        private readonly SupertableController _controller;

        private List<Item> _recipeTargets = new();
        private List<string[]> _recipeValues = new();

        private Rectangle _mainRect;
        private Rectangle _rightArrowRect;
        private Rectangle _leftArrowRect;
        private bool _hoverMain;
        private bool _hoverRight;
        private bool _hoverLeft;

        public int CurrentIndex { get; private set; }

        public RecipeNavigator(SupertableUI mainUI, SupertableController controller) {
            _mainUI = mainUI;
            _controller = controller;
        }

        public void LoadAllRecipes() {
            _recipeTargets.Clear();
            _recipeValues.Clear();

            foreach (var recipe in SupertableUI.AllRecipes) {
                _recipeTargets.Add(new Item(recipe.Target));
                _recipeValues.Add(recipe.Values);
            }
        }

        public void Update() {
            UpdatePositions();
            HandleInput();
        }

        private void UpdatePositions() {
            Vector2 drawPos = _mainUI.DrawPosition + SupertableConstants.RECIPE_UI_OFFSET;

            _mainRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, RecPBook?.Width ?? 100, RecPBook?.Height ?? 100);
            _rightArrowRect = new Rectangle((int)drawPos.X + 62, (int)drawPos.Y + 20, 25, 25);
            _leftArrowRect = new Rectangle((int)drawPos.X - 30, (int)drawPos.Y + 20, 25, 25);

            Rectangle mouseRect = new Rectangle((int)_mainUI.MousePosition.X, (int)_mainUI.MousePosition.Y, 1, 1);
            _hoverMain = _mainRect.Intersects(mouseRect);
            _hoverRight = _rightArrowRect.Intersects(mouseRect);
            _hoverLeft = _leftArrowRect.Intersects(mouseRect);
        }

        private void HandleInput() {
            if (_hoverRight || _hoverLeft || _hoverMain) {
                DragController.SetGlobalDontDragTime(2);
            }

            if (_mainUI.keyLeftPressState == KeyPressState.Pressed) {
                if (_hoverRight) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = SupertableConstants.SOUND_PITCH_HIGH });
                    CurrentIndex++;
                    UpdateIndex();
                }
                else if (_hoverLeft) {
                    SoundEngine.PlaySound(SoundID.Chat with { Pitch = SupertableConstants.SOUND_PITCH_LOW });
                    CurrentIndex--;
                    UpdateIndex();
                }
                else if (_hoverMain) {
                    DragController.SetGlobalDontDragTime(2);
                }
            }
        }

        private void UpdateIndex() {
            if (CurrentIndex < 0) {
                CurrentIndex = _recipeTargets.Count - 1;
            }
            else if (CurrentIndex >= _recipeTargets.Count) {
                CurrentIndex = 0;
            }

            LoadPreviewRecipe();

            //同步更新侧边栏的选中状态
            SyncToSidebar();
        }

        /// <summary>
        /// 设置当前配方索引（由外部调用，如侧边栏选择）
        /// </summary>
        public void SetRecipeIndex(int index, bool syncToSidebar = false) {
            if (index < 0 || index >= _recipeTargets.Count) return;

            CurrentIndex = index;
            LoadPreviewRecipe();

            if (syncToSidebar) {
                SyncToSidebar();
            }
        }

        /// <summary>
        /// 根据配方数据设置索引
        /// </summary>
        public void SetRecipeByData(RecipeData recipeData) {
            if (recipeData == null) return;

            for (int i = 0; i < SupertableUI.AllRecipes.Count; i++) {
                if (SupertableUI.AllRecipes[i] == recipeData) {
                    SetRecipeIndex(i, syncToSidebar: false); //从侧边栏调用，不需要反向同步
                    return;
                }
            }
        }

        /// <summary>
        /// 同步选中状态到侧边栏
        /// </summary>
        private void SyncToSidebar() {
            var sidebarManager = _mainUI.SidebarManager;

            if (sidebarManager != null && CurrentIndex >= 0 && CurrentIndex < sidebarManager.RecipeElements.Count) {
                sidebarManager.SelectedRecipe = sidebarManager.RecipeElements[CurrentIndex];

                //自动滚动到选中的配方
                sidebarManager.ScrollToRecipe(CurrentIndex);
            }
        }

        private void LoadPreviewRecipe() {
            if (CurrentIndex < 0 || CurrentIndex >= _recipeValues.Count) return;

            string[] recipeNames = _recipeValues[CurrentIndex];
            if (recipeNames == null) return;

            for (int i = 0; i < SupertableConstants.TOTAL_SLOTS && i < recipeNames.Length; i++) {
                int itemType = VaultUtils.GetItemTypeFromFullName(recipeNames[i], true);
                _controller.SlotManager.SetPreviewSlot(i, new Item(itemType));
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (RecPBook == null) return;

            Vector2 drawPos = _mainUI.DrawPosition + SupertableConstants.RECIPE_UI_OFFSET;

            spriteBatch.Draw(RecPBook, drawPos, null, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            Texture2D rightArrow = _hoverRight ? BlueArrow : BlueArrow2;
            Texture2D leftArrow = _hoverLeft ? BlueArrow : BlueArrow2;

            spriteBatch.Draw(rightArrow, drawPos + new Vector2(62, 20), null, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(leftArrow, drawPos + new Vector2(-30, 20), null, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);

            DrawRecipeInfo(spriteBatch, drawPos, alpha);
        }

        private void DrawRecipeInfo(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            if (_recipeTargets.Count == 0 || CurrentIndex < 0 || CurrentIndex >= _recipeTargets.Count)
                return;

            string indexText = $"{CurrentIndex + 1} -:- {_recipeTargets.Count}";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(indexText);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, indexText,
                drawPos.X - textSize.X / 2 + (RecPBook?.Width ?? 100) / 2, drawPos.Y + 65,
                Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);

            Item targetItem = _recipeTargets[CurrentIndex];
            SupertableUI.DrawItemIcon(spriteBatch, targetItem, drawPos + new Vector2(5, 5), 0.6f * alpha, 1.5f * alpha);

            string name = targetItem.HoverName;
            string recipeText = $"{CWRLocText.GetTextValue("SupertableUI_Text2")}:{(string.IsNullOrEmpty(name) ? CWRLocText.GetTextValue("SupertableUI_Text3") : name)}";
            Vector2 recipeTextSize = FontAssets.MouseText.Value.MeasureString(recipeText);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, recipeText,
                drawPos.X - recipeTextSize.X / 2 + (RecPBook?.Width ?? 100) / 2, drawPos.Y - 25,
                Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);

            if (_hoverMain && targetItem.type != ItemID.None) {
                Main.HoverItem = targetItem.Clone();
                Main.hoverItemName = targetItem.Name;
            }
        }
    }
}
