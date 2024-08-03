using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Graphics.Primitives;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    internal class CosmicCalamityRay : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        internal Vector2[] RayPoint;
        internal int pointNum => 100;
        public override bool ShouldUpdatePosition() => false;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.scale = 0.1f;
            Projectile.localNPCHitCooldown = -1;
            Projectile.usesLocalNPCImmunity = true;
        }

        public override bool PreAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                RayPoint = new Vector2[pointNum];
                for (int i = 0; i < pointNum; i++) {
                    RayPoint[i] = Projectile.velocity.ToRotation().ToRotationVector2() * (-pointNum * 30 + 60 * i) + Projectile.Center;
                }
                foreach (Vector2 pos in RayPoint) {
                    BaseParticle pulse = new DRK_DWave(pos - Projectile.velocity * 0.52f, Projectile.velocity / 1.5f, Color.Blue, new Vector2(1f, 2f), Projectile.velocity.ToRotation(), 0.52f, 0.06f, 90);
                    DRKLoader.AddParticle(pulse);
                    BaseParticle pulse2 = new DRK_DWave(pos - Projectile.velocity * 0.40f, Projectile.velocity / 1.5f * 0.9f, Color.Gold, new Vector2(0.8f, 1.5f), Projectile.velocity.ToRotation(), 0.28f, 0.02f, 80);
                    DRKLoader.AddParticle(pulse2);
                }
                Projectile.ai[0] = 1;
            }
            if (Projectile.timeLeft > 60) {
                Projectile.scale += 0.5f;
                if (Projectile.scale > 6)
                    Projectile.scale = 6;
            }
            if (Projectile.timeLeft < 20) {
                Projectile.scale -= 1f;
                if (Projectile.scale < 0)
                    Projectile.scale = 0;
            }
            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.rotation.ToRotationVector2() * -3000 + Projectile.Center
                , Projectile.rotation.ToRotationVector2() * 3000 + Projectile.Center, 32, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage = (int)(Projectile.damage * 0.98f);
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 300);
        }

        public float PrimitiveWidthFunction(float completionRatio) => CalamityUtils.Convert01To010(completionRatio) * Projectile.scale * Projectile.width;

        public Color PrimitiveColorFunction(float completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = CalamityUtils.MulticolorLerp(colorInterpolant, new Color(119, 210, 255), Color.Blue, new Color(247, 119, 255));
            return color;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (RayPoint != null) {
                GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].UseImage1("Images/Misc/Perlin");
                GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].Apply();
                PrimitiveRenderer.RenderTrail(RayPoint, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                    , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"]), 50);
            }
            return false;
        }
    }
}
