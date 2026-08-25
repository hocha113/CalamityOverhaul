using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Magic.WheezingWyrms
{
    /// <summary>
    /// 龙焰共享色带。温度 0~1.15 走黑体升温叙事：暗红→红橙→橙黄→黄白→蓝白→炽蓝，
    /// 越热越亮；0.92 以上进入蓝焰段
    /// </summary>
    internal static class Wyrmfire
    {
        //锚点沿 0~1.15 均匀分布
        private static readonly Color[] Ramp = [
            new(92, 16, 8),      //将熄暗红
            new(172, 34, 12),    //深红
            new(232, 66, 18),    //红橙
            new(255, 110, 28),   //橙
            new(255, 160, 52),   //橙黄
            new(255, 208, 104),  //黄
            new(255, 238, 180),  //黄白
            new(208, 228, 255),  //蓝白过渡
            new(96, 140, 255),   //炽蓝
        ];

        /// <summary>温度→焰色</summary>
        public static Color TempColor(float t) {
            float u = MathHelper.Clamp(t, 0f, 1.15f) / 1.15f * (Ramp.Length - 1);
            int i = Math.Min((int)u, Ramp.Length - 2);
            return Color.Lerp(Ramp[i], Ramp[i + 1], u - i);
        }

        /// <summary>温度→亮度系数，现实火焰越热越亮</summary>
        public static float Brightness(float t) => 0.45f + MathHelper.Clamp(t, 0f, 1.15f) * 0.75f;

        /// <summary>
        /// 外鞘色：红黄相位的外焰是更冷的暗红，蓝焰相位的外焰是更深的蓝(预混焰外鞘不发白)
        /// </summary>
        public static Color MantleColor(float t) => TempColor(t < 0.9f ? MathF.Max(t - 0.3f, 0.02f) : 1.15f);

        /// <summary>焰芯色：往白热方向偏但不给纯白，暖相成淡金、蓝相成蓝白</summary>
        public static Color CoreColor(float t)
            => Color.Lerp(TempColor(MathF.Min(t + 0.25f, 1.15f)), Color.White, 0.35f);
    }

    /// <summary>贴根火舌，锚在龙嘴或命中面上向外舔，逐帧长度抖动；按温度定色</summary>
    internal class PRT_WyrmTongue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        private float tongueRot;
        private float lengthMul;
        private float temp;
        private float jitterSeed;

        public PRT_WyrmTongue Configure(Vector2 outwardDir, float length, int lifetime, float temperature) {
            tongueRot = outwardDir.ToRotation() + MathHelper.PiOver2;
            lengthMul = length;
            Lifetime = lifetime;
            temp = temperature;
            return this;
        }

        public override void Reset() {
            base.Reset();
            tongueRot = 0f;
            lengthMul = 1f;
            temp = 0f;
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
            Velocity *= 0.82f;
            float lc = LifetimeCompletion;
            Opacity = (1f - lc) * (0.7f + 0.3f * MathF.Sin((Time + jitterSeed) * 2.9f)) * Wyrmfire.Brightness(temp);
            //舔出去的舌尖在冷却
            Color = Wyrmfire.TempColor(temp - lc * 0.3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            //根锚底边向外舔出，逐帧长度抖动是火的时域签名
            float jitter = 0.82f + 0.34f * MathF.Sin((Time * 2.3f + jitterSeed) * 3.4f);
            var stretch = new Vector2(0.5f, lengthMul * jitter) * Scale * 0.3f;
            var origin = new Vector2(tex.Width * 0.5f, tex.Height);
            spriteBatch.Draw(tex, pos, null, col * Opacity, tongueRot, origin, stretch, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>龙焰余烬：速度拉丝，按出生温度沿整条黑体色带回落冷却(蓝烬同样路过白黄红)，先浮后坠</summary>
    internal class PRT_WyrmEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;

        private float flickerSeed;
        private float buoyancy;
        private float temp;

        public PRT_WyrmEmber Configure(int lifetime, float temperature, float buoyancyStrength = 0.035f) {
            Lifetime = lifetime;
            temp = temperature;
            buoyancy = buoyancyStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
            buoyancy = 0f;
            temp = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 26);
            }
            if (buoyancy == 0f) {
                buoyancy = 0.035f;
            }
        }

        public override void AI() {
            Velocity *= 0.91f;
            float lc = LifetimeCompletion;
            //热态上浮，冷却后坠落
            Velocity.Y += lc < 0.45f ? -buoyancy : buoyancy * 1.8f;

            Color = Wyrmfire.TempColor(temp * (1f - lc));
            float flicker = 0.76f + 0.24f * MathF.Sin(Time * 1.1f + flickerSeed);
            Opacity = MathF.Min(lc * 7f, 1f) * (1f - lc * lc) * flicker * Wyrmfire.Brightness(temp);
            Scale *= 0.968f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D core = TexValue;
            Texture2D streak = StreakTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //顺速拉丝，运动各向异性
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

    /// <summary>
    /// 龙嗽烟团：Fog 真 alpha 可染深色。riseSpeed 上飘当烟，gravityAccel 为正当坠地烟灰
    /// </summary>
    internal class PRT_WyrmSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float rise;
        private float gravity;
        private float spin;

        public PRT_WyrmSmoke Configure(int lifetime, float riseSpeed = 0.05f, float gravityAccel = 0f) {
            Lifetime = lifetime;
            rise = riseSpeed;
            gravity = gravityAccel;
            return this;
        }

        public override void Reset() {
            base.Reset();
            rise = 0f;
            gravity = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.02f, 0.02f);
            ai[0] = Main.rand.Next(2);//镜像位，多团同屏防同贴纸感
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 44);
            }
        }

        public override void AI() {
            Velocity *= 0.93f;
            Velocity.Y += gravity - rise;
            Scale += 0.006f;
            Rotation += spin;
            float lc = LifetimeCompletion;
            //快现缓散
            Opacity = MathF.Min(Time / 4f, 1f) * MathF.Pow(1f - lc, 1.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            SpriteEffects fx = ai[0] == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity
                , Rotation, tex.Size() * 0.5f, Scale, fx, 0f);
            return false;
        }
    }
}
