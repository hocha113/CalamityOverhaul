using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class HeavenRainbowImpact : ModProjectile, IPrimitiveDrawable
    {
        public const int Lifetime = 45;
        public const float BeamLength = 1600f;
        private const int BeamPointCount = 18;

        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 10000;

        //初始色相由 ai[1] 注入, 不同箭对应不同色调
        private float HueOffset => (Projectile.ai[1] * 0.0173f) % 1f;

        private Color ChromaColor => VaultUtils.MultiStepColorLerp(
            (Projectile.timeLeft * 0.012f + HueOffset) % 1f, HeavenfallLongbow.rainbowColors);

        private Vector2[] beamPoints;
        private Trail beamTrail;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.MaxUpdates = 5;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Projectile.MaxUpdates * 6;
            Projectile.timeLeft = Projectile.MaxUpdates * Lifetime;
        }

        public override void AI() {
            //世界光: 跟随当前色调照亮场景
            Color light = ChromaColor;
            Lighting.AddLight(Projectile.Center, light.ToVector3() * 1.6f);

            if (VaultUtils.isServer) {
                return;
            }

            int totalLife = Projectile.MaxUpdates * Lifetime;
            float subFrame = totalLife - Projectile.timeLeft;
            //生成早期: 喷溅极光冲击环 (一次性, 只在前 ~5 个子帧内触发)
            if (subFrame < Projectile.MaxUpdates && subFrame >= 0) {
                Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 tailEnd = Projectile.Center - forward * BeamLength;
                for (int i = 0; i < 6; i++) {
                    float ang = MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.1f, 0.1f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3.5f, 6.5f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        tailEnd, vel,
                        Main.rand.NextFloat(120f, 200f), Main.rand.NextFloat(22f, 34f),
                        Main.rand.Next(34, 46),
                        huePhase: HueOffset + i / 6f,
                        hueSpeed: 0.03f,
                        driftScale: 1.3f));
                }
            }

            //沿光柱中段随机散星点 (节流: 每 3 子帧 1 个)
            if (subFrame % 3 == 0) {
                Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                float along = Main.rand.NextFloat(0.05f, 0.95f);
                Vector2 pos = Projectile.Center - forward * (BeamLength * along);
                Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
                pos += perp * Main.rand.NextFloat(-18f, 18f);
                Vector2 vel = perp * Main.rand.NextFloat(-1.5f, 1.5f)
                    + forward * Main.rand.NextFloat(-0.5f, 0.5f);
                Color col = VaultUtils.MultiStepColorLerp(
                    (along + HueOffset) % 1f, HeavenfallLongbow.rainbowColors);
                PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                    pos, vel, col,
                    Main.rand.NextFloat(0.8f, 1.4f), Main.rand.Next(18, 28),
                    Main.rand.NextFloat(3.5f, 6f), shortStretch: Main.rand.NextBool(3)));
            }

            //命中端 (Projectile.Center) 持续喷棱镜碎片
            if (subFrame % 2 == 0) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    vel, ChromaColor,
                    Main.rand.NextFloat(1.0f, 1.7f), Main.rand.Next(22, 34),
                    Main.rand.NextFloat(4f, 6.5f), shortStretch: true));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center
                , Projectile.Center + Projectile.velocity.UnitVector() * -BeamLength, Projectile.width, ref point);
        }

        //═════════════ Beam Trail ═════════════
        private float BeamWidthFunc(float progress) {
            //progress: 0=头部(命中端), 1=尾端(高空源头). 中段最宽, 两端微收
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            taper = 0.7f + 0.3f * taper;
            //寿命渐进收窄 (前期粗壮, 后期细弱)
            float totalLife = Projectile.MaxUpdates * Lifetime;
            float progressLife = 1f - MathHelper.Clamp(Projectile.timeLeft / totalLife, 0f, 1f);
            float widthMul = 1f - 0.45f * progressLife;
            return taper * widthMul * 80f;
        }

        private Color BeamColorFunc(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect shader = EffectLoader.HeavenfallStarbeam?.Value;
            if (shader == null) {
                return;
            }
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) {
                return;
            }

            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 head = Projectile.Center;
            Vector2 tail = head - forward * BeamLength;

            beamPoints ??= new Vector2[BeamPointCount];
            for (int i = 0; i < BeamPointCount; i++) {
                float t = i / (float)(BeamPointCount - 1);
                beamPoints[i] = Vector2.Lerp(head, tail, t);
            }

            beamTrail ??= new Trail(beamPoints, BeamWidthFunc, BeamColorFunc);
            beamTrail.TrailPositions = beamPoints;

            int totalLife = Projectile.MaxUpdates * Lifetime;
            float lifeProgress = 1f - MathHelper.Clamp(Projectile.timeLeft / (float)totalLife, 0f, 1f);
            //淡入淡出: 前 10% 淡入, 后 30% 淡出
            float fade = MathHelper.Clamp(lifeProgress / 0.1f, 0f, 1f)
                * (1f - MathHelper.Clamp((lifeProgress - 0.7f) / 0.3f, 0f, 1f));

            //冲击花: 仅生命前 35% 强烈, 后续衰减
            float burst = 1f - MathHelper.Clamp(lifeProgress / 0.35f, 0f, 1f);

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            shader.Parameters["progress"]?.SetValue(lifeProgress);
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["beamWidth"]?.SetValue(0.85f);
            shader.Parameters["hueOffset"]?.SetValue(HueOffset);
            shader.Parameters["impactBurst"]?.SetValue(burst);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            shader.CurrentTechnique = shader.Techniques["StarBeam"];

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState old = device.BlendState;
            device.BlendState = BlendState.Additive;
            beamTrail.DrawTrail(shader);
            device.BlendState = old;
        }
    }
}
