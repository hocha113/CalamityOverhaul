using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_StarPulseRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        private float OriginalScale;
        private float FinalScale;
        private Color BaseColor;
        /// <summary>环心星点与底层泛光的画幅边长(px/Scale)，沿用原 13px 星图 ×3 的观感尺寸</summary>
        private const float CoreSizePerScale = 39f;
        public override bool CanPool => true;
        public void Configure(float originalScale, float finalScale, int lifeTime) {
            BaseColor = Color;
            OriginalScale = originalScale;
            FinalScale = finalScale;
            Scale = originalScale;
            Lifetime = lifeTime;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }
        public override void Reset() {
            base.Reset();
            OriginalScale = 0f;
            FinalScale = 0f;
            BaseColor = default;
        }
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        public override void AI() {
            float pulseProgress = LifetimeCompletion;
            Scale = MathHelper.Lerp(OriginalScale, FinalScale, pulseProgress);

            Opacity = (float)Math.Sin(MathHelper.PiOver2 + LifetimeCompletion * MathHelper.PiOver2);
            Color = BaseColor * Opacity;
            Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
            Velocity *= 0.95f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, tex.Size() / 2f, Scale, SpriteEffects.None, 0);
            Texture2D star = CWRAsset.StarGlow01.Value;
            Texture2D bloom = CWRAsset.BloomSoft01.Value;
            float corePx = Scale * CoreSizePerScale;
            spriteBatch.Draw(bloom, pos, null, Color * 0.5f, 0, bloom.Size() / 2f, corePx / bloom.Width, SpriteEffects.None, 0);
            spriteBatch.Draw(star, pos, null, Color, 0, star.Size() / 2f, corePx / star.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
