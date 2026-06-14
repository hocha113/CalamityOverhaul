using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Smoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Smoke";
        private float Spin;
        private bool Glowing;
        private float HueShift;
        public override bool CanPool => true;
        public PRT_Smoke Configure(int lt, float opacity, float rotSpeed = 0f, bool glowing = false, float hueshift = 0f) {
            Lifetime = lt;
            Opacity = opacity;
            Spin = rotSpeed;
            Glowing = glowing;
            HueShift = hueshift;
            return this;
        }
        public override void Reset() {
            base.Reset();
            Spin = 0f;
            Glowing = false;
            HueShift = 0f;
        }
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ai[0] = Main.rand.Next(16);
            //InnoVault 约定 Lifetime < 0 为不限时；遗漏 Configure 时会在屏幕内永久堆积
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(28, 42);
                if (Opacity <= 0f) Opacity = 0.65f;
            }
        }
        public override void AI() {
            if (Time / (float)Lifetime < 0.2f) {
                Scale += 0.01f;
            }
            else {
                Scale *= 0.975f;
            }


            Color = Main.hslToRgb((Main.rgbToHsl(Color).X + HueShift) % 1, Main.rgbToHsl(Color).Y, Main.rgbToHsl(Color).Z);
            Opacity *= 0.98f;
            Rotation += Spin * (Velocity.X > 0 ? 1f : -1f);
            Velocity *= 0.85f;

            float opacity = Utils.GetLerpValue(1f, 0.85f, LifetimeCompletion, true);
            Color *= opacity;

            if (Opacity < 0.02f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameX = index % 4;
            int frameY = index / 4;
            Rectangle frame = new Rectangle(frameX * 256, frameY * 256, 256, 256);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation, frame.Size() / 2f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
