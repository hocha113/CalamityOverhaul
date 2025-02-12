using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class RecipeSidebarListViewUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public List<RecipeTargetElmt> recipeTargetElmts = [];
        private static SupertableUI supertableUI => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        internal RecipeTargetElmt TargetPecipePointer;
        internal RecipeTargetElmt PreviewTargetPecipePointer;
        internal MouseState oldMouseState;
        internal float rollerValue;
        internal float rollerSengs;
        internal int siderHeight;
        public override void Update() {
            DrawPosition = supertableUI.DrawPosition + new Vector2(supertableUI.UIHitBox.Width + 18, 8);
            for (int i = 0; i < recipeTargetElmts.Count; i++) {
                RecipeTargetElmt targetElmt = recipeTargetElmts[i];
                targetElmt.DrawPosition = DrawPosition + new Vector2(4, i * targetElmt.UIHitBox.Height - rollerValue);
                targetElmt.Update();
            }
            siderHeight = recipeTargetElmts.Count * 64 / recipeTargetElmts.Count * 7;
            MouseState currentMouseState = Mouse.GetState();
            int scrollWheelDelta = currentMouseState.ScrollWheelValue - oldMouseState.ScrollWheelValue;
            rollerValue -= scrollWheelDelta;
            rollerValue = MathHelper.Clamp(rollerValue, 64, recipeTargetElmts.Count * 64 - 64 * 4);
            rollerValue = ((int)rollerValue / 64) * 64;
            oldMouseState = currentMouseState;
            rollerSengs = (rollerValue / recipeTargetElmts.Count * 64) * siderHeight;
            UIHitBox = new Rectangle((int)DrawPosition.X - 4, (int)DrawPosition.Y, 72, siderHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, 70, siderHeight
                    , Color.AliceBlue * 0.8f * SupertableUI.Instance._sengs, Color.Azure * 0, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 4, DrawPosition, 70, siderHeight
                    , Color.AliceBlue * 0, Color.Azure * 1 * SupertableUI.Instance._sengs, 1);


            //进行矩形画布裁剪绘制
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
            Rectangle originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle newScissorRect = VaultUtils.GetClippingRectangle(spriteBatch, UIHitBox);
            spriteBatch.GraphicsDevice.ScissorRectangle = newScissorRect;

            for (int i = 0; i < recipeTargetElmts.Count; i++) {
                RecipeTargetElmt targetElmt = recipeTargetElmts[i];
                targetElmt.Draw(spriteBatch);
            }

            //恢复画布
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);
        }
    }
}
