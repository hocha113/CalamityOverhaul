using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_SoulFire : BasePRT
    {
        public override string Texture => CWRConstant.Other + "SoulFire";
        public override void SetProperty() {
            Opacity = 255;
            ai[1] = Main.rand.Next(5);
        }
        public override void AI() {
            if (++ai[0] > 5) {
                if (++ai[1] > 4) {
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
            Rectangle rectangle = TexValue.GetRectangle((int)ai[1], 5);
            Main.EntitySpriteDraw(
                TexValue, Position - Main.screenPosition,
                rectangle, Color.White * (Opacity / 255f), Rotation, rectangle.Size() / 2,
                Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
