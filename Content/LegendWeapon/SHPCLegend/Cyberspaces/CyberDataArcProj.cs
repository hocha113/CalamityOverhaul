using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>两点间颤动动电弧，改件 spawn；Trail+CyberDataArc.fx</summary>
    internal class CyberDataArcProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 22;
        private const int PointCount = 18;

        private Vector2[] points;
        private float fadeAlpha;
        private float jitterSeed;

        //外部覆色，默认电蓝
        public Vector3 CoreColor = new Color(220, 240, 255).ToVector3();
        public Vector3 GlowColor = new Color(80, 200, 255).ToVector3();

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //首帧采样折线
            if (Projectile.localAI[0] == 0f) {
                Vector2 start = Projectile.Center;
                Vector2 end = start + new Vector2(Projectile.ai[0], Projectile.ai[1]);
                jitterSeed = Main.rand.NextFloat(MathHelper.TwoPi);
                points = new Vector2[PointCount];
                Vector2 dir = end - start;
                Vector2 perp = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float length = dir.Length();
                float jitterAmp = MathF.Min(length * 0.10f, 26f);
                for (int i = 0; i < PointCount; i++) {
                    float t = (float)i / (PointCount - 1);
                    float taper = MathF.Sin(t * MathHelper.Pi); //两端 0 中段 1
                    float n = MathF.Sin(jitterSeed + t * 9.4f) * 0.5f
                        + MathF.Sin(jitterSeed * 1.3f + t * 17.7f) * 0.3f;
                    points[i] = Vector2.Lerp(start, end, t) + perp * n * jitterAmp * taper;
                }
                Projectile.localAI[0] = 1f;

                //溅射粒子点缀两端
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(2.5f, 2.5f);
                        PRTLoader.NewParticle<PRT_CyberSquare>(start, vel, new Color(GlowColor), Main.rand.NextFloat(0.5f, 1.2f)).Configure(new Color(CoreColor), Main.rand.Next(8, 18));
                        PRTLoader.NewParticle<PRT_CyberSquare>(end, vel, new Color(GlowColor), Main.rand.NextFloat(0.5f, 1.2f)).Configure(new Color(CoreColor), Main.rand.Next(8, 18));
                    }
                }
            }

            float t01 = 1f - (float)Projectile.timeLeft / MaxLife;
            //快速渐入再缓出
            if (t01 < 0.2f) {
                fadeAlpha = MathHelper.SmoothStep(0f, 1f, t01 / 0.2f);
            }
            else {
                fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t01 - 0.2f) / 0.8f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (points == null || fadeAlpha < 0.2f) return false;
            //仅在前几帧（视觉显著时）允许命中检测
            if (Projectile.timeLeft < MaxLife - 6) return false;
            float _ = 0f;
            for (int i = 0; i < points.Length - 1; i++) {
                if (Collision.CheckAABBvLineCollision(
                    new Vector2(targetHitbox.X, targetHitbox.Y),
                    new Vector2(targetHitbox.Width, targetHitbox.Height),
                    points[i], points[i + 1], 16f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        private float WidthFunction(float progress) {
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.32f + progress * 6f);
            return taper * pulse * 18f;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        private Trail trail;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (points == null || fadeAlpha < 0.01f) return;

            Effect shader = EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(points, WidthFunction, ColorFunction);
            trail.TrailPositions = points;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.06f);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["coreColor"]?.SetValue(CoreColor);
            shader.Parameters["glowColor"]?.SetValue(GlowColor);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public override bool ShouldUpdatePosition() => false;
    }
}
