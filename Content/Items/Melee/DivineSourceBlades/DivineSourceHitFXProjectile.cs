using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>神源之刃命中爆点，纯视觉</summary>
    internal class DivineSourceHitFXProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        private const int Lifetime = 24;
        private const float CanvasHalf = 130f;

        private static readonly Color CoreColor = new(255, 248, 225);
        private static readonly Color RingColor = new(255, 205, 95);
        private static readonly Color EmberColor = new(255, 135, 35);

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;

        private int Age => Lifetime - Projectile.timeLeft;
        private float LifeT => MathHelper.Clamp(Age / (float)Lifetime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source) {
            if (Main.dedServ) {
                return;
            }

            int sparkCount = (int)(10 * SizeMul);
            for (int i = 0; i < sparkCount; i++) {
                float ang = MathHelper.TwoPi * i / sparkCount + Main.rand.NextFloat(-0.2f, 0.2f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame);
                dust.velocity = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6f) * SizeMul;
                dust.scale = Main.rand.NextFloat(0.9f, 1.5f);
                dust.noGravity = true;
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            float t = LifeT;
            float lightMul = (1f - t) * SizeMul;
            Lighting.AddLight(Projectile.Center, new Vector3(0.95f, 0.72f, 0.30f) * lightMul);

            if (!Main.dedServ && t > 0.15f && Main.rand.NextBool(3)) {
                float ringR = RingRadiusNow() * CanvasHalf * SizeMul;
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * ringR, DustID.GoldCoin);
                dust.velocity = ang.ToRotationVector2() * 0.8f;
                dust.scale = Main.rand.NextFloat(0.6f, 1.0f);
                dust.noGravity = true;
            }
        }

        private float SphereIntensityNow() {
            float t = LifeT;
            float rise = Math.Min(1f, Age / 4f);
            float fall = 1f - SmoothStep01((t - 0.18f) / 0.42f);
            return rise * fall * 1.35f;
        }

        private float SphereRadiusNow() {
            float grow = 1f - MathF.Pow(1f - Math.Min(1f, Age / 6f), 2.5f);
            return MathHelper.Lerp(0.08f, 0.34f, grow);
        }

        private float RingRadiusNow() {
            float t = MathHelper.Clamp((Age - 2) / (float)(Lifetime - 2), 0f, 1f);
            float eased = 1f - MathF.Pow(1f - t, 2.2f);
            return MathHelper.Lerp(0.10f, 0.92f, eased);
        }

        private float RingThicknessNow() => MathHelper.Lerp(0.16f, 0.05f, LifeT);

        private float RingIntensityNow() {
            float rise = Math.Min(1f, Age / 5f);
            float fall = 1f - SmoothStep01((LifeT - 0.55f) / 0.45f);
            return rise * fall * 1.1f;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D canvas = DivineSourceBladeFX.SoftGlow;
            if (canvas == null) {
                return false;
            }

            Effect effect = DivineSourceBladeFX.Impact;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float canvasScale = CanvasHalf * 2f * SizeMul / canvas.Width;

            if (effect != null) {
                effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
                effect.Parameters["RingRadius"]?.SetValue(RingRadiusNow());
                effect.Parameters["RingThickness"]?.SetValue(RingThicknessNow());
                effect.Parameters["RingIntensity"]?.SetValue(RingIntensityNow());
                effect.Parameters["SphereRadius"]?.SetValue(SphereRadiusNow());
                effect.Parameters["SphereIntensity"]?.SetValue(SphereIntensityNow());
                effect.Parameters["CoreColor"]?.SetValue(CoreColor.ToVector4());
                effect.Parameters["RingColor"]?.SetValue(RingColor.ToVector4());
                effect.Parameters["EmberColor"]?.SetValue(EmberColor.ToVector4());
                Texture2D noise = DivineSourceBladeFX.Noise;
                if (noise != null) {
                    effect.Parameters["NoiseTexture"]?.SetValue(noise);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

                sb.Draw(canvas, drawPos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, canvasScale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                DrawFallback(sb, drawPos);
            }

            float starT = 1f - Math.Min(1f, Age / 9f);
            Texture2D star = DivineSourceBladeFX.BlankStar;
            if (star != null && starT > 0.02f) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Color starCol = CoreColor * (starT * 0.85f);
                starCol.A = 0;
                float starScale = (0.30f + (1f - starT) * 0.18f) * SizeMul;
                sb.Draw(star, drawPos, null, starCol, Age * 0.12f, star.Size() * 0.5f, starScale, SpriteEffects.None, 0f);
                sb.Draw(star, drawPos, null, starCol * 0.6f, -Age * 0.09f + MathHelper.PiOver4,
                    star.Size() * 0.5f, starScale * 0.6f, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private void DrawFallback(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glow = DivineSourceBladeFX.SoftGlow;
            if (glow == null) {
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = glow.Size() * 0.5f;

            float sphere = SphereIntensityNow();
            if (sphere > 0.02f) {
                Color core = CoreColor * (sphere * 0.7f);
                core.A = 0;
                sb.Draw(glow, drawPos, null, core, 0f, origin,
                    SphereRadiusNow() * 4f * SizeMul, SpriteEffects.None, 0f);
            }

            float ringIntensity = RingIntensityNow();
            if (ringIntensity > 0.02f) {
                float ringR = RingRadiusNow() * CanvasHalf * SizeMul;
                const int dots = 24;
                for (int i = 0; i < dots; i++) {
                    float ang = MathHelper.TwoPi * i / dots;
                    Vector2 dotPos = drawPos + ang.ToRotationVector2() * ringR;
                    Color dotCol = Color.Lerp(RingColor, EmberColor, 0.4f) * (ringIntensity * 0.35f);
                    dotCol.A = 0;
                    sb.Draw(glow, dotPos, null, dotCol, 0f, origin,
                        RingThicknessNow() * 2.6f * SizeMul, SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
