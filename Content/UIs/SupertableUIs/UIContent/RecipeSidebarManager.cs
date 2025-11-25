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

            //修复侧边栏高度计算 - 应该是固定高度，用于显示区域
            int visibleSlots = 7; //一次显示7个配方
            _sidebarHeight = visibleSlots * 64;

            MouseState currentMouseState = Mouse.GetState();
            int scrollDelta = currentMouseState.ScrollWheelValue - _oldMouseState.ScrollWheelValue;
            _scrollValue -= scrollDelta;
            
            //修复滚动范围计算
            int maxScroll = Math.Max(0, RecipeElements.Count * 64 - _sidebarHeight);
            _scrollValue = MathHelper.Clamp(_scrollValue, 0, maxScroll);
            
            //对齐到64的倍数，使滚动更流畅
            _scrollValue = ((int)_scrollValue / 64) * 64;
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
        
        /// <summary>
        /// 检查元素是否在可见区域内
        /// </summary>
        public bool IsElementVisible(RecipeTargetElement element)
        {
            if (element == null) return false;
            
            //获取元素相对于侧边栏的位置
            Rectangle elementBounds = element.Hitbox;
            
            //检查是否与侧边栏的可见区域相交
            return _hitbox.Intersects(elementBounds);
        }
        
        /// <summary>
        /// 获取侧边栏的可见区域
        /// </summary>
        public Rectangle VisibleArea => _hitbox;
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
        private Rectangle _hitbox;

        public void Update(SupertableUI mainUI, RecipeSidebarManager sidebar)
        {
            _hitbox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 64, 64);
            Rectangle mouseRect = new Rectangle((int)mainUI.MousePosition.X, (int)mainUI.MousePosition.Y, 1, 1);
            
            //检查是否在侧边栏的可见区域内
            bool isInSidebarBounds = sidebar.IsElementVisible(this);
            bool isHovered = _hitbox.Intersects(mouseRect) && isInSidebarBounds;

            float targetScale = 1f;
            Color targetColor = Color.Azure * 0.2f;

            if (isHovered)
            {
                UIHandle.player.mouseInterface = true;

                if (sidebar.HoveredRecipe != this)
                {
                    SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.6f, Volume = 0.4f });
                    sidebar.HoveredRecipe = this;
                }

                if (mainUI.keyLeftPressState == KeyPressState.Pressed)
                {
                    if (sidebar.SelectedRecipe != this)
                    {
                        sidebar.SelectedRecipe = this;
                        SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.6f, Volume = 0.8f });
                    }
                }

                Item item = new Item(RecipeData.Target);
                if (item.type > ItemID.None)
                {
                    CWRUI.HoverItem = item;
                    CWRUI.DontSetHoverItem = true;
                }

                targetScale = 1.2f;
                targetColor = Color.LightGoldenrodYellow;
            }

            if (sidebar.SelectedRecipe == this)
            {
                targetScale = 1.2f;
                targetColor = Color.Gold;
            }

            _backgroundColor = Color.Lerp(_backgroundColor, targetColor, 0.1f);
            _scale = MathHelper.Lerp(_scale, targetScale, 0.1f);
            _scale = MathHelper.Clamp(_scale, 1f, 1.2f);
        }

        public void Draw(SpriteBatch spriteBatch, float alpha)
        {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, 64, 64,
                Color.AliceBlue * 0.8f * alpha, _backgroundColor * alpha, _scale);

            Item item = new Item(RecipeData.Target);
            if (item.type > ItemID.None)
            {
                float drawSize = item.GetDrawItemSize(64) * _scale;
                Vector2 drawPos = DrawPosition + new Vector2(32, 32);
                VaultUtils.SimpleDrawItem(spriteBatch, item.type, drawPos, drawSize, 0, Color.White * alpha);
            }
        }
        
        public Rectangle Hitbox => _hitbox;
    }
}
