using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>哈基鱼雷</summary>
    internal static class FishCatVFX
    {
        /// <summary>奶油声波（平时的喵）</summary>
        public static readonly Color MeowCream = new(255, 238, 205);
        /// <summary>警告声波（临爆喵，转橙）</summary>
        public static readonly Color MeowWarn = new(255, 156, 78);
        /// <summary>踢起的干土</summary>
        public static readonly Color DustBrown = new(150, 122, 92);
        /// <summary>浑浊油滴</summary>
        public static readonly Color OilDripCol = new(148, 136, 90);
        /// <summary>哑光烟团</summary>
        public static readonly Color PuffGray = new(118, 102, 90);
        /// <summary>爆炸冲击环暖橙</summary>
        public static readonly Color BoomOrange = new(255, 168, 88);

        /// <summary>鳞片五彩纸屑色板</summary>
        public static readonly Color[] ScaleConfetti = new Color[] {
            new(252, 208, 120), //淡金
            new(244, 158, 168), //腮红粉
            new(168, 224, 190), //薄荷
            new(196, 172, 228), //丁香紫
            new(236, 232, 220), //珠白
        };

        /// <summary>嘴部声弧，双波前细线半环，strength 0..1 控制尺寸与寿命</summary>
        public static void MeowArc(Vector2 mouth, float dirRotation, float strength, Color color) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishCatMeowRing>(mouth, dirRotation.ToRotationVector2() * (1.1f + strength), color, 1f)
                ?.Configure(0.10f + 0.06f * strength, 0.40f + 0.34f * strength, (int)(18 + 10 * strength), dirRotation);
        }

        /// <summary>爆点全环</summary>
        public static void BoomRing(Vector2 center) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishCatMeowRing>(center, Vector2.Zero, new Color(255, 246, 228), 1f)
                ?.Configure(0.3f, 2.4f, 12, 0f, true);
        }

        /// <summary>浑浊油滴</summary>
        public static void OilDrip(Vector2 pos, Vector2 baseVel, int count) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(5f, 5f)
                    , baseVel + Main.rand.NextVector2Circular(0.9f, 0.9f), OilDripCol * 0.85f, Main.rand.NextFloat(0.4f, 0.62f))
                    ?.Configure(Main.rand.Next(18, 30), 0.24f, 0.99f);
            }
        }

        /// <summary>抛掷出手</summary>
        public static void ThrowBurst(Vector2 pos, Vector2 direction) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = direction.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(pos, DustID.Smoke
                    , dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 8f), 120
                    , new Color(205, 195, 180), Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos, dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(6f, 11f)
                    , MeowCream, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(true, Main.rand.Next(12, 18));
            }
            var wave = PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, MeowCream * 0.5f, 0.12f);
            wave?.Configure(new Vector2(1f, 0.55f), dir.ToRotation(), 0.5f, 9);
            MeowArc(pos + dir * 12f, dir.ToRotation(), 0.5f, MeowCream);
            OilDrip(pos, -dir * 1.5f, 2);
        }

        /// <summary>起跳踢尘</summary>
        public static void JumpDust(Vector2 feet, int dirX, float power, bool bigWave) {
            if (Main.dedServ) {
                return;
            }
            int count = 4 + (int)(power * 2f);
            for (int i = 0; i < count; i++) {
                Vector2 vel = new Vector2(-dirX * Main.rand.NextFloat(0.6f, 2.8f) * (0.6f + power * 0.7f)
                    , -Main.rand.NextFloat(1f, 3f));
                if (Main.rand.NextBool()) {
                    Dust dirt = Dust.NewDustPerfect(feet + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f)
                        , DustID.Dirt, vel, 40, default, Main.rand.NextFloat(0.9f, 1.4f));
                    dirt.noGravity = false;
                }
                else {
                    Dust smoke = Dust.NewDustPerfect(feet + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f)
                        , DustID.Smoke, vel * 0.6f, 130, DustBrown, Main.rand.NextFloat(1f, 1.5f));
                    smoke.noGravity = true;
                }
            }
            if (bigWave) {
                var wave = PRTLoader.NewParticle<PRT_DWave>(feet, Vector2.Zero, DustBrown * 0.5f, 0.1f);
                wave?.Configure(new Vector2(1f, 0.32f), 0f, 0.55f, 10);
            }
        }

        /// <summary>落地扬尘</summary>
        public static void LandDust(Vector2 feet, Vector2 oldVelocity, bool hard) {
            if (Main.dedServ) {
                return;
            }
            int count = hard ? 8 : 5;
            for (int i = 0; i < count; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(0.5f, 2.2f));
                Dust dust = Dust.NewDustPerfect(feet + new Vector2(Main.rand.NextFloat(-12f, 12f), -3f)
                    , Main.rand.NextBool() ? DustID.Dirt : DustID.Smoke, vel, 110
                    , DustBrown, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = !Main.rand.NextBool(3);
            }
            OilDrip(feet + new Vector2(0f, -8f), new Vector2(0f, -Math.Abs(oldVelocity.Y) * 0.12f), 2);
            if (hard) {
                PRTLoader.NewParticle<PRT_FishCatPuff>(feet + new Vector2(0f, -6f), new Vector2(0f, -0.4f)
                    , PuffGray, 0.16f)?.Configure(Main.rand.Next(26, 36));
                var wave = PRTLoader.NewParticle<PRT_DWave>(feet, Vector2.Zero, DustBrown * 0.45f, 0.1f);
                wave?.Configure(new Vector2(1f, 0.3f), 0f, 0.6f, 9);
            }
        }

        /// <summary>爆炸合成</summary>
        public static void Explode(Vector2 center, int layer, int facing) {
            if (Main.dedServ) {
                return;
            }
            BoomRing(center);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(center, Vector2.Zero, BoomOrange * 0.85f, 1f)
                ?.Configure(0.16f, 0.92f, 13);
            float baseRot = facing >= 0 ? 0f : MathHelper.Pi;
            for (int i = -1; i <= 1; i++) {
                float rot = baseRot + i * 0.55f * (facing >= 0 ? 1f : -1f);
                MeowArc(center + rot.ToRotationVector2() * 10f, rot, 1f, MeowWarn);
            }
            int confetti = Math.Min(16 + layer * 2, 24);
            for (int i = 0; i < confetti; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                vel.Y -= Main.rand.NextFloat(0f, 2.6f);
                PRTLoader.NewParticle<PRT_FishCatScale>(center + Main.rand.NextVector2Circular(8f, 8f), vel
                    , ScaleConfetti[Main.rand.Next(ScaleConfetti.Length)], Main.rand.NextFloat(0.7f, 1.15f))
                    ?.Configure(Main.rand.Next(46, 72));
            }
            for (int i = 0; i < 5; i++) {
                Vector2 off = Main.rand.NextVector2Circular(16f, 16f);
                Color col = Color.Lerp(PuffGray, new Color(150, 132, 116), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_FishCatPuff>(center + off, off.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.8f, 2.2f) + new Vector2(0f, -0.25f)
                    , col, Main.rand.NextFloat(0.2f, 0.3f))?.Configure(Main.rand.Next(34, 50));
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);
                vel.Y -= 2f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(center, vel, OilDripCol * 0.85f
                    , Main.rand.NextFloat(0.42f, 0.6f))?.Configure(Main.rand.Next(22, 34), 0.28f, 0.99f);
            }
            Punch(center);
        }

        /// <summary>克制的爆点震屏，尊重服务器配置</summary>
        public static void Punch(Vector2 pos) {
            if (Main.dedServ || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, Main.rand.NextVector2Unit(), 4f, 7f, 9, 640f, "FishCat"));
        }
    }

    /// <summary>喵声可视化</summary>
    internal class PRT_FishCatMeowRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Ring01";
        public override bool CanPool => true;

        private float startScale;
        private float endScale;
        private bool fullRing;
        private float wobbleSeed;
        private Color baseColor;

        public PRT_FishCatMeowRing Configure(float originScale, float finalScale, int lifetime, float dirRotation, bool asFullRing = false) {
            startScale = originScale;
            endScale = finalScale;
            Scale = originScale;
            Lifetime = lifetime;
            Rotation = dirRotation;
            fullRing = asFullRing;
            wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            startScale = 0f;
            endScale = 0f;
            fullRing = false;
            wobbleSeed = 0f;
            baseColor = default;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //波前快出缓收
            Scale = MathHelper.Lerp(startScale, endScale, 1f - MathF.Pow(1f - t, 2.4f));
            Opacity = MathHelper.Clamp(t * 6f, 0f, 1f) * MathF.Pow(1f - t, 1.55f);
            Velocity *= 0.93f;
            //爆点全环的白过冲只留头两帧,随即衰入暖橙
            if (fullRing && Time > 2) {
                baseColor = Color.Lerp(baseColor, FishCatVFX.BoomOrange, 0.4f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Rectangle src;
            Vector2 origin;
            if (fullRing) {
                src = new Rectangle(0, 0, tex.Width, tex.Height);
                origin = src.Size() / 2f;
            }
            else {
                //右半环切片:弧口朝Rotation方向鼓出,环心在切片左缘中点
                src = new Rectangle(tex.Width / 2, 0, tex.Width / 2, tex.Height);
                origin = new Vector2(0f, tex.Height * 0.5f);
            }
            Vector2 pos = Position - Main.screenPosition;
            //颤音squish:沿传播向与垂直向微缩放摆动
            float wob = MathF.Sin(Time * 0.55f + wobbleSeed) * 0.05f;
            Vector2 drawScale = new Vector2(Scale * (1f + wob), Scale * (1f - wob));
            Color main = baseColor * (Opacity * 0.8f);
            Color echo = baseColor * (Opacity * 0.38f);
            Color kiss = new Color(baseColor.R, baseColor.G, baseColor.B, 0) * (Opacity * 0.22f);
            spriteBatch.Draw(tex, pos, src, main, Rotation, origin, drawScale, SpriteEffects.None, 0f);
            //滞后回声波前:制造声波厚度
            spriteBatch.Draw(tex, pos, src, echo, Rotation, origin, drawScale * 0.74f, SpriteEffects.None, 0f);
            //极轻加色吻边,暗处保读性
            spriteBatch.Draw(tex, pos, src, kiss, Rotation, origin, drawScale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>鱼鳞五彩纸屑，受重力</summary>
    internal class PRT_FishCatScale : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 2000;

        private float swayPhase;
        private float swayRate;
        private float tumble;
        private Color baseColor;

        public PRT_FishCatScale Configure(int lifetime) {
            Lifetime = lifetime;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayRate = Main.rand.NextFloat(0.13f, 0.22f);
            tumble = Main.rand.NextFloat(0.18f, 0.4f) * (Main.rand.NextBool() ? 1f : -1f);
            baseColor = Color;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            swayRate = 0f;
            tumble = 0f;
            baseColor = default;
        }

        public override void AI() {
            swayPhase += swayRate;
            //纸屑摆振下落:横向来回荡+轻重力,初速衰减后进入飘落
            Velocity.X = Velocity.X * 0.94f + MathF.Sin(swayPhase) * 0.24f;
            Velocity.Y = Math.Min(Velocity.Y + 0.14f, 3.6f);
            Rotation += tumble * 0.5f + Velocity.X * 0.02f;
            tumble *= 0.995f;
            float t = LifetimeCompletion;
            Opacity = t < 0.68f ? 1f : 1f - (t - 0.68f) / 0.32f;
            Color = baseColor * Opacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            //翻面:cos接近0时鳞片侧对镜头,横向压扁
            float face = MathF.Abs(MathF.Cos(swayPhase * 0.5f));
            Vector2 drawScale = new Vector2(0.30f * (0.25f + 0.75f * face), 0.22f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, tex.Size() / 2f, drawScale, SpriteEffects.None, 0f);
            //翻面瞬间的单帧镜面小闪
            float glint = MathF.Max(0f, face - 0.92f) / 0.08f;
            if (glint > 0f) {
                Color spec = new Color(255, 248, 235, 0) * (glint * 0.55f * Opacity);
                spriteBatch.Draw(tex, pos, null, spec, Rotation, tex.Size() / 2f, drawScale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    internal class PRT_FishCatPuff : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private float baseScale;
        private Color baseColor;

        public PRT_FishCatPuff Configure(int lifetime) {
            Lifetime = lifetime;
            spin = Main.rand.NextFloat(0.015f, 0.045f) * (Main.rand.NextBool() ? 1f : -1f);
            baseScale = Scale;
            baseColor = Color;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            baseScale = 0f;
            baseColor = default;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //快胀缓散
            Scale = baseScale * (0.55f + 0.65f * (1f - MathF.Pow(1f - t, 2.2f)));
            Velocity *= 0.90f;
            Velocity.Y -= 0.012f;
            Rotation += spin;
            Opacity = MathHelper.Clamp(t * 5f, 0f, 1f) * MathF.Pow(1f - t, 1.4f) * 0.88f;
            Color = baseColor * Opacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation
                , tex.Size() / 2f, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
