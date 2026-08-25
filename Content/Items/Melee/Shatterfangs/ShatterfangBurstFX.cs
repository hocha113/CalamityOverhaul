using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 剑身崩坏爆点。开头数帧一记小范围震撼判定，之后纯演出：
    /// 白骨核心闪+扩散环+四芒星闪，红白克眼色板
    /// </summary>
    internal class ShatterfangBurstFX : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        private const int Lifetime = 26;
        private const float CanvasHalf = 150f;
        /// <summary>震撼判定半径</summary>
        private const float HitRadius = 105f;

        private int Age => Lifetime - Projectile.timeLeft;
        private float LifeT => MathHelper.Clamp(Age / (float)Lifetime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Age <= 7 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Age > 7) {
                return false;
            }
            Vector2 c = Projectile.Center;
            Vector2 closest = new(MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right)
                , MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, c) <= HitRadius * HitRadius;
        }

        public override void OnSpawn(IEntitySource source) {
            if (Main.dedServ) {
                return;
            }
            //环状骨屑喷发垫场
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.2f, 0.2f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                    , ang.ToRotationVector2() * Main.rand.NextFloat(3f, 7f), 80, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, ShatterfangFX.ScarletBright.ToVector3() * (1.3f * (1f - LifeT)));
        }

        private float SphereIntensity() {
            float rise = Math.Min(1f, Age / 3f);
            float fall = 1f - SmoothStep01((LifeT - 0.15f) / 0.4f);
            return rise * fall * 1.4f;
        }

        private float SphereRadius() {
            float grow = 1f - MathF.Pow(1f - Math.Min(1f, Age / 6f), 2.5f);
            return MathHelper.Lerp(0.08f, 0.36f, grow);
        }

        private float RingRadius01() {
            float t = MathHelper.Clamp((Age - 1) / (float)(Lifetime - 1), 0f, 1f);
            return MathHelper.Lerp(0.08f, 0.95f, 1f - MathF.Pow(1f - t, 2.3f));
        }

        private float RingIntensity() {
            float rise = Math.Min(1f, Age / 4f);
            float fall = 1f - SmoothStep01((LifeT - 0.5f) / 0.5f);
            return rise * fall * 1.15f;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //扩散冲击环
            float ringT = MathHelper.Clamp((Age - 1) / 15f, 0f, 1f);
            if (ringT > 0f && ringT < 1f) {
                float ringR = MathHelper.Lerp(14f, 130f, 1f - MathF.Pow(1f - ringT, 2.2f));
                ShockRingDraw.Draw(sb, Projectile.Center, ringR, 11f
                    , ShatterfangFX.BoneLead, ShatterfangFX.ScarletBright, ShatterfangFX.BloodDeep
                    , (1f - ringT) * 0.9f, innerGlow: 0.25f, timeSeed: Projectile.whoAmI * 0.37f);
            }

            //爆点核心，红白冲击帧
            Effect effect = EffectLoader.DivineSourceImpact?.Value;
            Texture2D canvas = CWRAsset.SoftGlow?.Value;
            if (effect != null && canvas != null) {
                effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
                effect.Parameters["RingRadius"]?.SetValue(RingRadius01());
                effect.Parameters["RingThickness"]?.SetValue(MathHelper.Lerp(0.15f, 0.05f, LifeT));
                effect.Parameters["RingIntensity"]?.SetValue(RingIntensity());
                effect.Parameters["SphereRadius"]?.SetValue(SphereRadius());
                effect.Parameters["SphereIntensity"]?.SetValue(SphereIntensity());
                effect.Parameters["CoreColor"]?.SetValue(ShatterfangFX.BoneLead.ToVector4());
                effect.Parameters["RingColor"]?.SetValue(ShatterfangFX.ScarletBright.ToVector4());
                effect.Parameters["EmberColor"]?.SetValue(ShatterfangFX.BloodMain.ToVector4());
                Texture2D noise = CWRAsset.Fog?.Value ?? CWRAsset.PerlinNoise?.Value;
                if (noise != null) {
                    effect.Parameters["NoiseTexture"]?.SetValue(noise);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f
                    , CanvasHalf * 2f / canvas.Width, SpriteEffects.None, 0f);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //四芒星白闪，黑底星图 A=0 只加亮
            float starT = 1f - Math.Min(1f, Age / 9f);
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null && starT > 0.02f) {
                Color starCol = ShatterfangFX.BoneLead * (starT * 0.85f);
                starCol.A = 0;
                float starScale = 0.26f + (1f - starT) * 0.16f;
                sb.Draw(star, drawPos, null, starCol, Age * 0.1f, star.Size() * 0.5f, starScale, SpriteEffects.None, 0f);
                sb.Draw(star, drawPos, null, starCol * 0.6f, -Age * 0.07f + MathHelper.PiOver4
                    , star.Size() * 0.5f, starScale * 0.55f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
