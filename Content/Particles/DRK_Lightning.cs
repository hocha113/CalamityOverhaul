using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Particles
{
    internal class DRK_Lightning : BaseParticle
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle3";
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;
        public int timeLeftMax;
        public int timeLeft;
        public int timer;
        private float size;
        private float opacity;
        public override void SetDRK() {
            timeLeft = timer = Lifetime = Main.rand.Next(5, 7);
            if (ai[1] == 1) {
                timeLeft = 120;
                size = 3f / 10f;
            }
            if (ai[1] == 2) {
                timeLeft = 36;
                size = 3f / 10f;
            }

            timeLeftMax = timeLeft;
        }

        public override void AI() {
            timer--;
            timeLeft--;

            if (timeLeft <= timeLeftMax / 2f) {
                opacity = MathHelper.Lerp(1f, 0f, 1 - (float)(timeLeftMax / 2f - timeLeft) / (timeLeftMax / 2f));
            }

            if (timeLeft <= 0) {
                Kill();
            }
        }

        public override void CustomDraw(SpriteBatch spriteBatch) {
            int alp = 200;
            Color bright = Color.Multiply(new(255, 255, 255, alp), opacity);
            Color mid = Color.Multiply(new(161, 255, 253, alp), opacity);
            Color dark = Color.Multiply(new(40, 186, 242, alp), opacity);

            switch (ai[0]) {
                case 1:
                    bright = Color.Multiply(new(255, 255, 255, alp), opacity);
                    mid = Color.Multiply(new(255, 255, 174, alp), opacity);
                    dark = Color.Multiply(new(255, 189, 69, alp), opacity);
                    break;
                case 2:
                    bright = Color.Multiply(new(255, 146, 135, alp), opacity);
                    mid = Color.Multiply(new(223, 62, 55, alp), opacity);
                    dark = Color.Multiply(new(150, 20, 54, alp), opacity);
                    break;
                case 3:
                    bright = Color.Multiply(new(158, 57, 248, alp), opacity);
                    mid = Color.Multiply(new(158, 57, 248, alp), opacity);
                    dark = Color.Multiply(new(104, 45, 237, alp), opacity);
                    break;
                case 4:
                    bright = Color.Multiply(new(255, 182, 49, alp), opacity);
                    mid = Color.Multiply(new(255, 182, 49, alp), opacity);
                    dark = Color.Multiply(new(255, 105, 43, alp), opacity);
                    break;
                case 5:
                    bright = Color.Multiply(new(186, 255, 185, alp), opacity);
                    mid = Color.Multiply(new(76, 240, 107, alp), opacity);
                    dark = Color.Multiply(new(23, 165, 107, alp), opacity);
                    break;
            }

            Color emberColor = Color.Multiply(Color.Lerp(bright, dark, (float)(timeLeftMax - timeLeft) / timeLeftMax), opacity);
            Color glowColor = Color.Multiply(Color.Lerp(mid, dark, (float)(timeLeftMax - timeLeft) / timeLeftMax), 1f);

            float pixelRatio = 1f / 64f;

            spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Masking + "SoftGlow")
                , Position - Main.screenPosition, new Rectangle(0, 0, 64, 64), glowColor, Rotation
                , new Vector2(32f, 32f), 1f * Scale, SpriteEffects.None, 0f);

            spriteBatch.Draw(DRKLoader.ParticleIDToTexturesDic[Type]
                , Position - new Vector2(1.5f, 1.5f) - Main.screenPosition
                , new Rectangle(0, 0, 64, 64), emberColor, Rotation, Vector2.Zero
                , 1f * pixelRatio * 3f * Scale, SpriteEffects.None, 0f);
        }
    }
}
