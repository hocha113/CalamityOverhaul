using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>飞天鱼羽的羽毛质感词汇库</summary>
    internal static class FishHarpyVFX
    {
        //羽色三件套: 奶白羽面、淡金点缀、冷淡蓝空气
        internal static readonly Color Cream = new(246, 240, 226);
        internal static readonly Color Gold = new(230, 196, 122);
        internal static readonly Color AirCool = new(178, 196, 220);

        /// <summary>剥落绒羽簇</summary>
        internal static void DownBurst(Vector2 pos, Vector2 dir, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            Vector2 d = dir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = d.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(0.35f, 1f) * speed
                    + Main.rand.NextVector2Circular(0.6f, 0.6f);
                PRTLoader.NewParticle<PRT_FishHarpyDown>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , vel, Cream, Main.rand.NextFloat(0.42f, 0.72f)).Configure(Main.rand.Next(26, 44));
            }
        }

        /// <summary>破空涟漪细弧</summary>
        internal static void AirRipple(Vector2 pos, Vector2 orient, float scale) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishHarpyAirLine>(pos, orient.SafeNormalize(Vector2.UnitX) * 0.9f
                , AirCool, scale * 0.3f).ConfigureStreak(Main.rand.Next(10, 15), 0.16f);
        }

        /// <summary>集结/蓄力提示</summary>
        internal static void ChargeCue(Vector2 center, float radius) {
            if (Main.dedServ) {
                return;
            }
            //ArcWave 宽 256px, 成对镜像拼成气环透镜, 从三成扩到羽环直径
            float endScale = radius * 2.05f / 256f;
            float startScale = endScale * 0.3f;
            const int ringLife = 16;
            float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishHarpyAirLine>(center, Vector2.Zero, AirCool, startScale)
                    .ConfigureRing(ringLife, (endScale - startScale) / ringLife, baseRot + MathHelper.Pi * i, 0.15f);
            }
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(0.5f);
                PRTLoader.NewParticle<PRT_FishHarpyDown>(center + ang.ToRotationVector2() * 8f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4f), Cream
                    , Main.rand.NextFloat(0.5f, 0.8f)).Configure(Main.rand.Next(26, 40));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(center + Main.rand.NextVector2Circular(22f, 22f)
                    , Main.rand.NextVector2Circular(1f, 1f), Gold * 0.85f, 0.24f)
                    .Configure(Gold * 0.5f, 9, 0.1f, 0.55f);
            }
        }

        /// <summary>死后残迹</summary>
        internal static void FeatherRemnant(Vector2 pos, Vector2 inheritVel) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishHarpyFall>(pos, Vector2.Zero, Cream, Main.rand.NextFloat(0.8f, 0.95f))
                .Configure(Main.rand.Next(56, 78), inheritVel);
        }
    }

    /// <summary>绒羽小簇</summary>
    internal class PRT_FishHarpyDown : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float swaySeed;
        private float swayFreq;
        private float spin;
        private float sink;
        private Color litTint;

        public PRT_FishHarpyDown Configure(int lifetime, float sinkRate = 0.012f) {
            Lifetime = lifetime;
            sink = sinkRate;
            return this;
        }

        public override void Reset() {
            base.Reset();
            swaySeed = 0f;
            swayFreq = 0f;
            spin = 0f;
            sink = 0f;
            litTint = default;
        }

        public override void SetProperty() {
            //默认 AlphaBlend, 不改绘制模式
            swaySeed = Main.rand.NextFloat(MathHelper.TwoPi);
            swayFreq = Main.rand.NextFloat(0.10f, 0.17f);
            spin = Main.rand.NextFloat(0.018f, 0.05f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(32, 52);
            }
            if (sink == 0f) {
                sink = 0.012f;
            }
            SampleLight();
        }

        private void SampleLight() {
            Color lit = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            litTint = lit.MultiplyRGB(Color);
        }

        public override void AI() {
            //空气几乎立刻吃掉初速度, 之后只剩摆动与沉降
            Velocity *= 0.90f;
            Velocity.X += MathF.Sin(Time * swayFreq + swaySeed) * 0.028f;
            Velocity.Y += sink;
            if (Velocity.Y > 0.9f) {
                Velocity.Y = 0.9f;
            }
            Rotation += spin + Velocity.X * 0.012f;

            float t = LifetimeCompletion;
            Opacity = MathF.Min(Time / 5f, 1f) * (1f - MathF.Pow(t, 1.7f));
            if (Time % 6 == 0) {
                SampleLight();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Color col = litTint * Opacity;

            //三笔交叉短绒: 异角度窄条拼出簇状剪影
            spriteBatch.Draw(tex, pos, null, col * 0.85f, Rotation, origin, new Vector2(0.16f, 0.30f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + Rotation.ToRotationVector2() * 1.5f, null, col * 0.55f
                , Rotation + 0.85f, origin, new Vector2(0.13f, 0.24f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos - Rotation.ToRotationVector2() * 1.2f, null, col * 0.4f
                , Rotation - 0.65f, origin, new Vector2(0.11f, 0.20f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>空气涟漪线</summary>
    internal class PRT_FishHarpyAirLine : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "ArcWave";
        public override bool CanPool => true;

        private bool ring;
        private float expand;
        private float maxAlpha;

        public PRT_FishHarpyAirLine ConfigureStreak(int lifetime, float alpha = 0.16f) {
            Lifetime = lifetime;
            ring = false;
            maxAlpha = alpha;
            //凸缘朝运动方向, 静止时随机
            Rotation = Velocity == Vector2.Zero ? Main.rand.NextFloat(MathHelper.TwoPi) : Velocity.ToRotation();
            return this;
        }

        public PRT_FishHarpyAirLine ConfigureRing(int lifetime, float expandRate, float rotation, float alpha = 0.14f) {
            Lifetime = lifetime;
            ring = true;
            expand = expandRate;
            maxAlpha = alpha;
            Rotation = rotation;
            return this;
        }

        public override void Reset() {
            base.Reset();
            ring = false;
            expand = 0f;
            maxAlpha = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (maxAlpha == 0f) {
                maxAlpha = 0.16f;
            }
            if (Lifetime <= 0) {
                Lifetime = 12;
            }
        }

        public override void AI() {
            if (ring) {
                Scale += expand;
                Velocity *= 0.9f;
            }
            else {
                //线条几乎驻留在空气里
                Velocity *= 0.86f;
            }
            float t = LifetimeCompletion;
            Opacity = MathF.Min(Time / 3f, 1f) * MathF.Pow(1f - t, 1.4f) * maxAlpha;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 scale = ring ? new Vector2(Scale, Scale * 0.55f) : new Vector2(Scale, Scale * 0.34f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>死后落羽</summary>
    internal class PRT_FishHarpyFall : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float swayPhase;
        private Vector2 carry;
        private bool grounded;
        private Color litTint;
        private SpriteEffects flip;

        public PRT_FishHarpyFall Configure(int lifetime, Vector2 inheritVel) {
            Lifetime = lifetime;
            carry = inheritVel;
            return this;
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            carry = default;
            grounded = false;
            litTint = default;
            flip = SpriteEffects.None;
        }

        public override void SetProperty() {
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            flip = Main.rand.NextBool() ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(58, 76);
            }
            SampleLight();
        }

        private void SampleLight() {
            Color lit = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            litTint = lit.MultiplyRGB(FishHarpyVFX.Cream);
        }

        public override void AI() {
            if (grounded) {
                Velocity = Vector2.Zero;
                //触地后双速计时, 提早谢幕
                Time++;
            }
            else {
                swayPhase += 0.055f;
                carry *= 0.88f;
                //摆锤飘落: 端点横速为零悬滞, 中段下滑最快
                Velocity = new Vector2(MathF.Sin(swayPhase) * 1.05f
                    , 0.5f + MathF.Cos(swayPhase * 2f) * 0.17f) + carry;
                Rotation = MathF.Sin(swayPhase) * 0.55f;
                if (Collision.SolidCollision(Position - new Vector2(4f), 8, 8)) {
                    grounded = true;
                }
            }

            float t = LifetimeCompletion;
            Opacity = MathF.Min(Time / 4f, 1f) * (t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
            if (Time % 6 == 0) {
                SampleLight();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[ModContent.ProjectileType<HarpyFeatherOrbit>()].Value;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //贴图尖端朝下, 加 PiOver2 摆成横躺姿态再叠摇摆角
            float rot = MathHelper.PiOver2 + Rotation;
            Color col = litTint * Opacity;

            //单帧姿态残影表达摇摆
            spriteBatch.Draw(tex, pos, null, col * 0.22f, rot - MathF.Sin(swayPhase) * 0.16f, origin, Scale, flip, 0f);
            spriteBatch.Draw(tex, pos, null, col * 0.9f, rot, origin, Scale, flip, 0f);
            return false;
        }
    }
}
