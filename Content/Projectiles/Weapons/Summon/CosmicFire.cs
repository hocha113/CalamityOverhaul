using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class CosmicFire : ModProjectile
    {
        public bool ableToHit = true;
        public NPC target;
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionShot[Projectile.type] = true;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 3;
            Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override bool? CanDamage() {
            return Projectile.timeLeft > 150 ? false : null;
        }

        public override void AI() {
            target = Projectile.Center.FindClosestNPC(1600);
            if (target != null && Projectile.timeLeft <= 150) {
                if (Projectile.timeLeft == 150)
                    Projectile.velocity = Projectile.Center.To(target.Center).UnitVector() * 3;
                Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.01f);
            }
            for (int i = 0; i < 6; i++) {
                CWRRef.CosmicFireEffect(Projectile);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
