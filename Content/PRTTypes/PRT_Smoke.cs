using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Smoke : BasePRT
    {
        public override string Texture => "CalamityMod/Particles/HeavySmoke";
        private float Spin;
        private bool StrongVisual;
        private bool Glowing;
        private float HueShift;
        private static int FrameAmount = 6;
        public PRT_Smoke(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale
            , float opacity, float rotationSpeed = 0f, bool glowing = false, float hueshift = 0f, bool required = false) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = scale;
            Lifetime = lifetime;
            Opacity = opacity;
            Spin = rotationSpeed;
            StrongVisual = required;
            Glowing = glowing;
            HueShift = hueshift;

        }
        public override void SetProperty() {
            if (Glowing) {
                PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            }
            else {
                PRTDrawMode = PRTDrawModeEnum.NonPremultiplied;
            }
            ai[0] = Main.rand.Next(7);
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
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int animationFrame = (int)Math.Floor(Time / (float)(Lifetime / (float)FrameAmount));
            Rectangle frame = new Rectangle((int)(80 * ai[0]), 80 * animationFrame, 80, 80);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation, frame.Size() / 2f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
