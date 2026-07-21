using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>机械殉爆光团，SoftGlow 多层+冲击环，客户端 Additive</summary>
    internal class PRT_MechExplosion : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 3000;

        private Color warmColor = new(255, 130, 40);

        /// <summary>规模看 <see cref="BasePRT.Scale"/></summary>
        public PRT_MechExplosion Configure(int lifetime, Color warm) {
            Lifetime = lifetime;
            warmColor = warm;
            return this;
        }

        public override void Reset() {
            base.Reset();
            warmColor = new Color(255, 130, 40);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 32;
            }
        }

        public override void AI() {
            Velocity *= 0.9f;
            Rotation += 0.02f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = MathHelper.Clamp(LifetimeCompletion, 0f, 1f);
            float fade = 1f - t;
            Vector2 drawPos = Position - Main.screenPosition;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Vector2 glowOrigin = glow.Size() / 2f;
            Vector2 starOrigin = star.Size() / 2f;
            Vector2 ringOrigin = ring.Size() / 2f;

            //白热→暖橙→余烬
            Color whiteHot = Color.Lerp(Color.White, warmColor, Saturate(t / 0.25f));
            Color ember = Color.Lerp(warmColor, new Color(70, 14, 10), Saturate((t - 0.25f) / 0.75f));

            //前20%急胀后缓收
            float expand = t < 0.2f ? t / 0.2f : 1f;
            float shrink = t < 0.2f ? 1f : 1f - (t - 0.2f) / 0.8f * 0.5f;
            float coreSize = Scale * (0.5f + expand * shrink * 1.3f);

            //外晕
            spriteBatch.Draw(glow, drawPos, null, ember * (0.5f * fade), Rotation, glowOrigin, coreSize * 2.2f, SpriteEffects.None, 0f);
            //中层
            spriteBatch.Draw(glow, drawPos, null, Color.Lerp(whiteHot, ember, 0.4f) * (0.8f * fade), Rotation, glowOrigin, coreSize * 1.25f, SpriteEffects.None, 0f);
            //白热核
            float coreAlpha = Saturate(1f - t / 0.5f);
            spriteBatch.Draw(glow, drawPos, null, whiteHot * (0.95f * coreAlpha), Rotation, glowOrigin, coreSize * 0.65f, SpriteEffects.None, 0f);

            //前段星闪
            float flashAlpha = Saturate(1f - t / 0.35f);
            if (flashAlpha > 0.001f) {
                float starScale = Scale * 0.22f * (0.6f + flashAlpha * 0.6f);
                spriteBatch.Draw(star, drawPos, null, whiteHot * flashAlpha, Rotation * 0.5f, starOrigin, starScale, SpriteEffects.None, 0f);
            }

            //冲击环
            float ringT = Saturate(t / 0.6f);
            if (ringT < 1f) {
                float ringScale = Scale * (0.2f + ringT * 0.7f);
                float ringAlpha = (1f - ringT) * 0.55f;
                spriteBatch.Draw(ring, drawPos, null, ember * ringAlpha, Rotation, ringOrigin, ringScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        private static float Saturate(float v) => MathHelper.Clamp(v, 0f, 1f);
    }
}
