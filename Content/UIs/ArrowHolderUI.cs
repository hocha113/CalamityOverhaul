using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal class ArrowHolderUI : UIHandle
    {
        private static Item weapon => player.GetItem();
        private static Item ammo => player.ChooseAmmo(weapon);
        public override Texture2D Texture => TextureAssets.Item[ammo.type].Value;
        public override bool Active => ammo != null && weapon != null && CWRLoad.ItemIsBow[weapon.type];
        private int Weith;
        private int Height;
        public override void Update() {
            Weith = Texture.Width;
            Height = Texture.Height;
            int arrowDrawStackCount = ammo.stack;
            if (arrowDrawStackCount > 6) {
                arrowDrawStackCount = 6;
            }
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Weith + (arrowDrawStackCount * Weith / 2), Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            DrawPosition = new Vector2(20, Main.screenHeight - 100);
            DrawPosition += new Vector2(CWRServerConfig.Instance.CartridgeUI_Offset_X_Value
                , -CWRServerConfig.Instance.CartridgeUI_Offset_Y_Value);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Rectangle rectangle = CWRUtils.GetRec(CWRAsset.Quiver_back_Asset.Value, 0, 4);
            spriteBatch.Draw(CWRAsset.Quiver_back_Asset.Value, DrawPosition, rectangle, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (ammo.type == ItemID.None) {
                return;
            }

            int arrowDrawStackCount = ammo.stack;
            if (arrowDrawStackCount > 6) {
                arrowDrawStackCount = 6;
            }
            for (int i = 0; i < arrowDrawStackCount; i++) {
                Vector2 drawPos = DrawPosition + new Vector2(CWRAsset.Quiver_back_Asset.Width() + i * Weith / 2, 0);
                spriteBatch.Draw(Texture, drawPos, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
                if (i == arrowDrawStackCount - 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, ammo.stack.ToString()
                    , drawPos.X - Weith, drawPos.Y + Height, Color.White, Color.Black, new Vector2(0.2f), 0.8f);
                }
            }

            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, ammo.Name
                , MousePosition.X, MousePosition.Y + 30, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
