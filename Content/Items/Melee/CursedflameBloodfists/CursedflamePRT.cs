using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.CursedflameBloodfists
{
    /// <summary>
    /// 咒焰火舌，根部锚在拳锋上向外舔，长度逐帧抖动。
    /// 撕裂端头交给贴图，颜色从绿核冷却到锈橙
    /// </summary>
    internal class PRT_CursedTongue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        private float tongueRot;
        private float lengthMul;
        private float jitterSeed;

        public PRT_CursedTongue Configure(Vector2 outwardDir, float length, int lifetime) {
            tongueRot = outwardDir.ToRotation() + MathHelper.PiOver2;
            lengthMul = length;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            tongueRot = 0f;
            lengthMul = 1f;
            jitterSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            jitterSeed = Main.rand.NextFloat(100f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(4, 8);
            }
        }

        public override void AI() {
            Velocity *= 0.82f;
            float lc = LifetimeCompletion;
            Opacity = (1f - lc) * (0.72f + (0.28f * MathF.Sin((Time + jitterSeed) * 2.9f)));
            //绿焰只在前三成寿命里存在，之后迅速退到橙，这是这套火的辨识点
            Color = CursedflameFX.Ramp(MathHelper.Clamp(lc * 1.35f, 0f, 1f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            float jitter = 0.82f + (0.34f * MathF.Sin((Time * 2.3f + jitterSeed) * 3.9f));
            var stretch = new Vector2(0.46f, lengthMul * jitter) * Scale;
            var origin = new Vector2(tex.Width * 0.5f, tex.Height);
            spriteBatch.Draw(tex, pos, null, col * Opacity, tongueRot, origin, stretch, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 咒焰余烬，顺速度拉丝，热态上浮冷却后坠落。颜色即温度：绿→橙→焦棕
    /// </summary>
    internal class PRT_CursedEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;

        private float flickerSeed;
        private float buoyancy;

        public PRT_CursedEmber Configure(int lifetime, float buoyancyStrength = 0.04f) {
            Lifetime = lifetime;
            buoyancy = buoyancyStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
            buoyancy = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 28);
            }
            if (buoyancy == 0f) {
                buoyancy = 0.04f;
            }
        }

        public override void AI() {
            Velocity *= 0.92f;
            float lc = LifetimeCompletion;
            Velocity.Y += lc < 0.42f ? -buoyancy : buoyancy * 1.9f;
            Color = CursedflameFX.Ramp(lc);
            float flicker = 0.74f + (0.26f * MathF.Sin((Time * 1.2f) + flickerSeed));
            Opacity = MathF.Min(lc * 7f, 1f) * (1f - (lc * lc)) * flicker;
            Scale *= 0.965f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D core = TexValue;
            Texture2D streak = StreakTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            float speed = Velocity.Length();
            if (streak != null && speed > 1.3f) {
                float stretch = MathHelper.Clamp(speed * 0.17f, 0.3f, 1.7f);
                spriteBatch.Draw(streak, pos, null, col * (0.7f * Opacity)
                    , Velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(0.2f, stretch) * Scale, SpriteEffects.None, 0f);
            }

            Vector2 origin = core.Size() * 0.5f;
            spriteBatch.Draw(core, pos, null, col * (0.5f * Opacity), 0f, origin, 0.26f * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(core, pos, null, col * (0.95f * Opacity), 0f, origin, 0.11f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
