using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Nutritional : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override void SetProperty() {
            Color = new Color(113, 224, 88);

            if (Lifetime == 0f) {
                Lifetime = Main.rand.Next(60, 90);
            }

            if (Scale == 0f) {
                Scale = 1f;
            }

            Scale *= Main.rand.NextFloat(1.2f, 2.2f);
            Velocity.Y += Main.rand.NextFloat(-6, 2);
            Velocity.X += Main.rand.NextFloat(-6, 6);
        }

        public override void AI() {
            Velocity.Y += 0.02f;
            Velocity *= 0.96f;
            Scale *= 0.98f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPosition = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            spriteBatch.Draw(tex, drawPosition, null, Color, Rotation, origin, Scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
