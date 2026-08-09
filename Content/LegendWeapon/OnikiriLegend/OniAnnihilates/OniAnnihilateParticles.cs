using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>泼墨墨滴,AlphaBlend 暗色(加色画不了黑)</summary>
    internal class PRT_OniInkDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
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
            //朝向由速度锁死,只能靠随机镜像避免墨滴同形
            ai[0] = Main.rand.Next(2);
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
            //沿速度微拉长
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 1f, 1.7f);
            Vector2 scale = new Vector2(0.80f, stretch) * Scale * 0.096f;
            SpriteEffects flip = ai[0] == 0f ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity, Rotation
                , tex.Size() * 0.5f, scale, flip, 0);
            return false;
        }
    }
}
