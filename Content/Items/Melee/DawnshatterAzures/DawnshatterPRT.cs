using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>日冕余烬,速度拉丝,生命期内 金→橙红→焦暗 冷却,先浮后坠</summary>
    internal class PRT_DawnEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;

        private static readonly Color HotGold = new(255, 208, 96);
        private static readonly Color EmberRed = new(255, 92, 30);
        private static readonly Color Charred = new(118, 42, 26);

        private float flickerSeed;
        private float buoyancy;

        public PRT_DawnEmber Configure(int lifetime, float buoyancyStrength = 0.035f) {
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
                Lifetime = Main.rand.Next(18, 30);
            }
            if (buoyancy == 0f) {
                buoyancy = 0.035f;
            }
        }

        public override void AI() {
            Velocity *= 0.91f;
            float lc = LifetimeCompletion;
            //热态上浮,冷却后坠落
            Velocity.Y += lc < 0.45f ? -buoyancy : buoyancy * 1.8f;

            //冷却斜坡,颜色即温度叙事
            Color = lc < 0.4f
                ? Color.Lerp(HotGold, EmberRed, lc / 0.4f)
                : Color.Lerp(EmberRed, Charred, (lc - 0.4f) / 0.6f);

            float flicker = 0.76f + 0.24f * MathF.Sin(Time * 1.1f + flickerSeed);
            Opacity = MathF.Min(lc * 7f, 1f) * (1f - lc * lc) * flicker;
            Scale *= 0.968f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D core = TexValue;
            Texture2D streak = StreakTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //运动各向异性,顺速拉丝
            float speed = Velocity.Length();
            if (streak != null && speed > 1.4f) {
                float stretch = MathHelper.Clamp(speed * 0.16f, 0.3f, 1.6f);
                spriteBatch.Draw(streak, pos, null, col * (0.7f * Opacity)
                    , Velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(0.2f, stretch) * Scale, SpriteEffects.None, 0f);
            }

            Vector2 origin = core.Size() * 0.5f;
            spriteBatch.Draw(core, pos, null, col * (0.5f * Opacity), 0f, origin, 0.28f * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(core, pos, null, col * (0.95f * Opacity), 0f, origin, 0.12f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>贴根火舌,锚在刃缘外舔,2~5 帧高频闪变,噪声撕裂端头由贴图承担</summary>
    internal class PRT_DawnTongue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        private static readonly Color TongueGold = new(255, 186, 74);
        private static readonly Color TongueRed = new(240, 96, 34);

        private float tongueRot;
        private float lengthMul;
        private float jitterSeed;

        public PRT_DawnTongue Configure(Vector2 outwardDir, float length, int lifetime) {
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
                Lifetime = Main.rand.Next(3, 6);
            }
        }

        public override void AI() {
            Velocity *= 0.8f;
            float lc = LifetimeCompletion;
            Opacity = (1f - lc) * (0.75f + 0.25f * MathF.Sin((Time + jitterSeed) * 2.7f));
            Color = Color.Lerp(TongueGold, TongueRed, lc);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            //根锚底边,向外舔出;逐帧长度抖动是火的时域签名
            float jitter = 0.85f + 0.3f * MathF.Sin((Time * 2.1f + jitterSeed) * 3.7f);
            var stretch = new Vector2(0.5f, lengthMul * jitter) * Scale;
            var origin = new Vector2(tex.Width * 0.5f, tex.Height);
            spriteBatch.Draw(tex, pos, null, col * Opacity, tongueRot, origin, stretch, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>焰屑烟,余烬冷却的去处,AlphaBlend 暗色团,上升膨胀消散</summary>
    internal class PRT_DawnSoot : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private static readonly Color SootDark = new(46, 30, 26);

        private int frameIdx;
        private float spin;

        public override void Reset() {
            base.Reset();
            frameIdx = 0;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            frameIdx = Main.rand.Next(4);
            spin = Main.rand.NextFloat(-0.03f, 0.03f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 44);
            }
        }

        public override void AI() {
            Velocity *= 0.93f;
            Velocity.Y -= 0.028f;
            Rotation += spin;
            Scale += 0.014f;
            float lc = LifetimeCompletion;
            Opacity = MathF.Min(lc * 5f, 1f) * (1f - lc) * 0.62f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            //2×2 序列帧,帧边长按贴图实际尺寸推导
            int fs = tex.Width / 2;
            var frame = new Rectangle(frameIdx % 2 * fs, frameIdx / 2 * fs, fs, fs);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, SootDark * Opacity
                , Rotation, frame.Size() * 0.5f, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
