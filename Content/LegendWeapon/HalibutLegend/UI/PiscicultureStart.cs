using CalamityMod.Items.Weapons.Ranged;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class PiscicultureStart : UIHandle
    {
        public static float _sengs;
        public static bool HeldHalibut {
            get {
                return false;//TODO:暂时未制作完成，所以不要启动它
                if (Main.gameMenu || !Main.LocalPlayer.active) {
                    return false;
                }
                int type = Main.LocalPlayer.GetItem().type;
                if (type <= 0) {
                    return false;
                }
                return type == ModContent.ItemType<HalibutCannon>()
                    || type == ModContent.ItemType<HalibutCannonOverride>();
            }
        }
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "HalibutStyleButton");
        public override bool Active => HeldHalibut || _sengs > 0;
        public Rectangle mainHitBox;
        public override void Update() {
            if (!HeldHalibut && _sengs > 0) {
                _sengs -= 0.1f;
            }
            if (HeldHalibut && _sengs < 1) {
                _sengs += 0.1f;
            }

            DrawPosition = new Vector2(0, Main.screenHeight / 2);
            mainHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Texture.Width, Texture.Height);
            Rectangle mouseRec = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            bool onUI = mouseRec.Intersects(mainHitBox);
            if (onUI) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    PiscicultureUI.IsOnpen = !PiscicultureUI.IsOnpen;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Texture, DrawPosition, null, Color.White * _sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
    }
}
