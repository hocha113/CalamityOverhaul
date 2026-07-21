using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>泼墨墨滴,AlphaBlend 暗色(加色画不了黑)</summary>
    internal class PRT_OniInkDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private Color initialColor;

        public PRT_OniInkDrop Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            if (Lifetime <= 0) {
                Lifetime = 26;
            }
        }

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y += 0.35f; //坠落
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Scale *= 0.985f;

            float t = LifetimeCompletion;
            //前2帧实体化,末35%淡出
            Opacity = MathF.Min(t / 0.08f, 1f) * (1f - SmoothStep01((t - 0.65f) / 0.35f));
            Color = initialColor;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            //沿速度微拉长
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 1f, 1.7f);
            Vector2 scale = new Vector2(0.80f, stretch) * Scale * 0.16f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
