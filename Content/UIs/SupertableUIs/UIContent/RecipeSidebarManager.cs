using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.UIContent
{
    /// <summary>
    /// 配方侧边栏管理器
    /// </summary>
    internal class RecipeSidebarManager
    {
        private readonly SupertableUI _mainUI;
        public List<RecipeTargetElement> RecipeElements { get; private set; }
        public RecipeTargetElement SelectedRecipe { get; set; }
        public RecipeTargetElement HoveredRecipe { get; set; }

        private MouseState _oldMouseState;
        private float _scrollValue;
        private int _sidebarHeight;
        private Rectangle _hitbox;

        public RecipeSidebarManager(SupertableUI mainUI) {
            _mainUI = mainUI;
            RecipeElements = new List<RecipeTargetElement>();
        }

        public void InitializeRecipeElements() {
            RecipeElements.Clear();
            foreach (var recipe in SupertableUI.AllRecipes) {
                RecipeElements.Add(new RecipeTargetElement { RecipeData = recipe });
            }
        }

        public void Update() {
            Vector2 drawPosition = _mainUI.DrawPosition + new Vector2(_mainUI.UIHitBox.Width + 18, 8);

            for (int i = 0; i < RecipeElements.Count; i++) {
                RecipeTargetElement element = RecipeElements[i];
                element.DrawPosition = drawPosition + new Vector2(4, i * 64 - _scrollValue);
                element.Update(_mainUI, this);
            }

            _sidebarHeight = Math.Max(RecipeElements.Count * 64 / Math.Max(1, RecipeElements.Count) * 7, 64);

            MouseState currentMouseState = Mouse.GetState();
            int scrollDelta = currentMouseState.ScrollWheelValue - _oldMouseState.ScrollWheelValue;
            _scrollValue -= scrollDelta;
            _scrollValue = MathHelper.Clamp(_scrollValue, 64, Math.Max(64, RecipeElements.Count * 64 - 64 * 4));
            _scrollValue = (int)_scrollValue / 64 * 64;
            _oldMouseState = currentMouseState;

            _hitbox = new Rectangle((int)drawPosition.X - 4, (int)drawPosition.Y, 72, _sidebarHeight);

            if (_hitbox.Intersects(_mainUI.MouseHitBox)) {
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/RecipeSidebar");
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            Vector2 drawPosition = _mainUI.DrawPosition + new Vector2(_mainUI.UIHitBox.Width + 18, 8);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, drawPosition, 70, _sidebarHeight,
                Color.AliceBlue * 0.8f * alpha, Color.Azure * 0, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, drawPosition, 70, _sidebarHeight,
                Color.AliceBlue * 0, Color.Azure * 1 * alpha, 1);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null,
                new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);

            Rectangle originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle newScissorRect = VaultUtils.GetClippingRectangle(spriteBatch, _hitbox);
            spriteBatch.GraphicsDevice.ScissorRectangle = newScissorRect;

            foreach (var element in RecipeElements) {
                element.Draw(spriteBatch, alpha);
            }

            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.End();
            spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);
        }

        public float ScrollValue {
            get => _scrollValue;
            set => _scrollValue = value;
        }
    }

    /// <summary>
    /// 配方目标元素
    /// </summary>
    internal class RecipeTargetElement
    {
        public RecipeData RecipeData { get; set; }
        public Vector2 DrawPosition { get; set; }
        private float _scale = 1f;
        private Color _backgroundColor = Color.Azure * 0.2f;

        public void Update(SupertableUI mainUI, RecipeSidebarManager sidebar) {
            Rectangle hitbox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 64, 64);
            Rectangle mouseRect = new Rectangle((int)mainUI.MousePosition.X, (int)mainUI.MousePosition.Y, 1, 1);
            bool isHovered = hitbox.Intersects(mouseRect) && mainUI.hoverInMainPage;

            float targetScale = 1f;
            Color targetColor = Color.Azure * 0.2f;

            if (isHovered) {
                UIHandle.player.mouseInterface = true;

                if (sidebar.HoveredRecipe != this) {
                    SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.6f, Volume = 0.4f });
                    sidebar.HoveredRecipe = this;
                }

                if (mainUI.keyLeftPressState == KeyPressState.Pressed) {
                    if (sidebar.SelectedRecipe != this) {
                        sidebar.SelectedRecipe = this;
                        SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.6f, Volume = 0.8f });
                    }
                }

                Item item = new Item(RecipeData.Target);
                if (item.type > ItemID.None) {
                    CWRUI.HoverItem = item;
                    CWRUI.DontSetHoverItem = true;
                }

                targetScale = 1.2f;
                targetColor = Color.LightGoldenrodYellow;
            }

            if (sidebar.SelectedRecipe == this) {
                targetScale = 1.2f;
                targetColor = Color.Gold;
            }

            _backgroundColor = Color.Lerp(_backgroundColor, targetColor, 0.1f);
            _scale = MathHelper.Lerp(_scale, targetScale, 0.1f);
            _scale = MathHelper.Clamp(_scale, 1f, 1.2f);
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, 64, 64,
                Color.AliceBlue * 0.8f * alpha, _backgroundColor * alpha, _scale);

            Item item = new Item(RecipeData.Target);
            if (item.type > ItemID.None) {
                float drawSize = item.GetDrawItemSize(64) * _scale;
                Vector2 drawPos = DrawPosition + new Vector2(32, 32);
                VaultUtils.SimpleDrawItem(spriteBatch, item.type, drawPos, drawSize, 0, Color.White * alpha);
            }
        }
    }
}
