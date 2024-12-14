using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Particles
{
    internal class PRT_HellFire : BasePRT
    {
        public override string Texture => CWRConstant.Other + "HellFire";
        public override void SetProperty() {
            Opacity = 255;
            ai[1] = Main.rand.Next(4);
        }
        public override void AI() {
            if (++ai[0] > 5) {
                if (++ai[1] > 3) {
                    ai[1] = 0;
                }
                ai[0] = 0;
            }
            Scale -= 0.015f;
            Opacity -= 1;
            if (Scale <= 0) {
                active = false;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2 + ai[2];
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Rectangle rectangle = CWRUtils.GetRec(TexValue, (int)ai[1], 4);
            Main.EntitySpriteDraw(
                TexValue, Position - Main.screenPosition,
                rectangle, Color.White * (Opacity / 255f), Rotation, rectangle.Size() / 2,
                Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
