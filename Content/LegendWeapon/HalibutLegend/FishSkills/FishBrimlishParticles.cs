using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>硫火余烬</summary>
    internal class PRT_FishBrimlishEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 2000;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        private static readonly Color ColHot = new(255, 168, 66);
        private static readonly Color ColBrim = new(218, 66, 20);
        private static readonly Color ColDeep = new(96, 22, 12);

        private float flickerSeed;
        private float gravity;
        private float baseScale;

        public PRT_FishBrimlishEmber Configure(int lifetime, float gravityStrength = 0.045f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
            gravity = 0f;
            baseScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 30);
            }
            if (gravity == 0f) {
                gravity = 0.045f;
            }
            if (baseScale == 0f) {
                baseScale = Scale;
            }
        }

        public override void AI() {
            if (baseScale <= 0f) {
                baseScale = Scale;
            }
            //急减速后余烬下坠
            Velocity *= 0.91f;
            if (Velocity.Length() < 2.6f) {
                Velocity.Y += gravity;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            float t = LifetimeCompletion;
            //冷却色程
            Color = t < 0.35f
                ? Color.Lerp(ColHot, ColBrim, t / 0.35f)
                : Color.Lerp(ColBrim, ColDeep, (t - 0.35f) / 0.65f);

            float flicker = 0.76f + 0.24f * MathF.Sin(Time * 0.85f + flickerSeed);
            Opacity = MathF.Min(t * 7f, 1f) * (1f - t * t) * flicker;
            Scale = baseScale * (1f - t * 0.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D streak = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //顺速度拉丝，速度快时火星呈线
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.13f, 0.35f, 1.6f);
            spriteBatch.Draw(streak, pos, null, col * (0.8f * Opacity), Rotation
                , streak.Size() * 0.5f, new Vector2(0.2f, stretch) * Scale, SpriteEffects.None, 0f);

            //同色小热核，不引入纯白
            Texture2D glow = SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, col * (0.5f * Opacity), 0f
                    , glow.Size() * 0.5f, 0.16f * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>余燃残焰</summary>
    internal class PRT_FishBrimlishResidue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fire";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        private static readonly Color ColIgnite = new(255, 150, 58);
        private static readonly Color ColBrim = new(212, 62, 20);
        private static readonly Color ColFade = new(70, 18, 10);

        private int frameOffset;
        private float baseScale;

        public PRT_FishBrimlishResidue Configure(int lifetime) {
            Lifetime = lifetime;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            frameOffset = 0;
            baseScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            frameOffset = Main.rand.Next(16);
            Rotation = Main.rand.NextFloat(-0.22f, 0.22f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 32);
            }
            if (baseScale == 0f) {
                baseScale = Scale;
            }
        }

        public override void AI() {
            if (baseScale <= 0f) {
                baseScale = Scale;
            }
            //残焰锚在原地缓慢上飘
            Velocity *= 0.86f;
            Velocity.Y -= 0.03f;

            float t = LifetimeCompletion;
            //起燃短暂胀大 → 余燃收缩熄灭
            float swell = t < 0.22f
                ? MathHelper.Lerp(0.68f, 1.06f, t / 0.22f)
                : MathHelper.Lerp(1.06f, 0.26f, (t - 0.22f) / 0.78f);
            Scale = baseScale * swell;

            Color = t < 0.30f
                ? Color.Lerp(ColIgnite, ColBrim, t / 0.30f)
                : Color.Lerp(ColBrim, ColFade, (t - 0.30f) / 0.70f);
            Opacity = MathF.Min(t * 6f, 1f) * (1f - MathF.Pow(t, 2.2f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            int frameW = tex.Width / 4;
            int frameH = tex.Height / 4;
            int idx = (int)(Time / 3f + frameOffset) % 16;
            Rectangle frame = new(frameW * (idx % 4), frameH * (idx / 4), frameW, frameH);
            //原点取焰根，焰舌从锚点向上生长
            Vector2 origin = new(frameW * 0.5f, frameH * 0.92f);
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //底光，暗红压底
            Texture2D glow = SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, new Color(120, 30, 14, 0) * (0.4f * Opacity), 0f
                    , glow.Size() * 0.5f, 0.5f * Scale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(tex, pos, frame, col * Opacity, Rotation
                , origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
