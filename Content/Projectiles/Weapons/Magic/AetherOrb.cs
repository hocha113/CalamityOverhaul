using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class AetherOrb : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/LaserProj";
        public ref float BeamLength => ref Projectile.localAI[0];
        public override void SetDefaults() {
            Projectile.width = 5;
            Projectile.height = 5;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.alpha = 255;
            Projectile.penetrate = 3;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 30 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Projectile.tileCollide = Projectile.ai[0] == 0;
            Projectile.damage += Projectile.originalDamage / 90;
            Projectile.alpha = Utils.Clamp(Projectile.alpha - 25, 0, 255);
            BeamLength = MathHelper.Clamp(BeamLength + 2f, 0f, 100f);
            Lighting.AddLight(Projectile.Center, 1f, 0f, 0.7f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(250, 50, 200, 0);

        public override bool PreDraw(ref Color lightColor) => CWRRef.DrawBeam(Projectile, 100f, 2f, lightColor);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.numHits == 0 && Projectile.ai[0] == 0) {
                for (int i = 0; i < 8; i++) {
                    Vector2 velocity = ((MathHelper.TwoPi * i / 8f) - (MathHelper.Pi / 8f)).ToRotationVector2() * 6f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - Projectile.velocity, velocity
                        , ModContent.ProjectileType<AetherOrb>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner, 1);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.CountProjectilesOfID(Type) > 120) {
                return;
            }

            if (Projectile.IsOwnedByLocalPlayer() && Projectile.numHits == 0 && Projectile.ai[0] == 0) {
                for (int i = 0; i < 8; i++) {
                    Vector2 velocity = ((MathHelper.TwoPi * i / 8f) - (MathHelper.Pi / 8f)).ToRotationVector2() * 6f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - Projectile.velocity, velocity
                        , ModContent.ProjectileType<AetherOrb>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner, 1);
                }
            }
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.ai[0] == 1) {
                for (int i = 0; i < 2; i++) {
                    Vector2 velocity = Projectile.velocity.RotatedBy((-1 + i) * 0.1f) * 2f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - Projectile.velocity, velocity
                        , ModContent.ProjectileType<AetherOrb>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner, 2);
                }
            }
        }
    }
}
