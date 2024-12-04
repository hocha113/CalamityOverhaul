using CalamityMod;
using CalamityMod.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj
{
    internal class AstralPikeBeam : ModProjectile
    {
        private Vector2 targetPos {
            get => new Vector2(Projectile.ai[0], Projectile.ai[1]);
            set {
                Projectile.ai[0] = value.X;
                Projectile.ai[1] = value.Y;
            }
        }
        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 122;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.alpha = 50;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 150;
            Projectile.scale = 0.3f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
        }

        public override void AI() {
            if (Projectile.timeLeft > 90) {
                Projectile.velocity *= 0.98f;
            }
            else {
                if (Projectile.timeLeft == 90) {
                    Projectile.velocity = Projectile.velocity.UnitVector() * 23;
                }
                Projectile.SmoothHomingBehavior(targetPos, 1, 0.05f);
            }
            LineParticle spark2 = new LineParticle(Projectile.Center, -Projectile.velocity * 0.05f, false, 17, 1.7f, Color.Goldenrod);
            GeneralParticleHandler.SpawnParticle(spark2);
        }

        public override bool PreDraw(ref Color lightColor) {
            Projectile.DrawStarTrail(Color.Coral, Color.White);
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }
    }
}
