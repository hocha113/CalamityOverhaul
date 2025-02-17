using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs
{
    internal class CanOverrideByItemIDUI : UIHandle
    {
        public override bool Active => CWRServerConfig.Instance.ModifiIntercept && ItemOverride.ByID.ContainsKey(player.GetItem().type);
        public bool onDrag;
        public Vector2 dragOffsetPos;
        public override void Update() {
            if (DrawPosition == Vector2.Zero) {
                DrawPosition = new Vector2(Main.screenWidth, Main.screenHeight) / 2;
            }

            UIHitBox = DrawPosition.GetRectangle(100, 40);
            hoverInMainPage = MouseHitBox.Intersects(UIHitBox);

            if (hoverInMainPage && keyLeftPressState == KeyPressState.Pressed) {
                ItemRebuildLoader.SendModifiIntercept(player.GetItem(), player);
            }

            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (keyRightPressState == KeyPressState.Held && !onDrag) {
                    if (!onDrag) {
                        dragOffsetPos = DrawPosition - MousePosition;
                    }
                    onDrag = true;
                }
            }

            if (onDrag) {
                player.mouseInterface = true;
                DrawPosition = MousePosition + dragOffsetPos;
                if (keyRightPressState == KeyPressState.Released) {
                    onDrag = false;
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.SimpleDrawItem(spriteBatch, player.GetItem().type, DrawPosition + UIHitBox.Size() / 2, 40);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 2, UIHitBox, Color.AliceBlue
                , ItemOverride.CanOverrideByID[player.GetItem().type] ? Color.Goldenrod : Color.White * 0.1f);
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, "左键点击切换拦截状态，右键拖动按钮位置"
                        , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
