using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class LostSoulBullet : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.height = Projectile.width = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 520;
            Projectile.light = 0.5f;
        }

        public override void AI() {
            BasePRT particle = new PRT_SoulLight(Projectile.Center, Projectile.velocity
                , Main.rand.NextFloat(0.2f, 0.8f), Color.White, 23, _flowerProj: Projectile);
            PRTLoader.AddParticle(particle);
            NPC target = Projectile.Center.FindClosestNPC(600);
            if (target != null && Projectile.timeLeft < 480) {
                if (Projectile.Center.To(target.Center).LengthSquared() > 20000) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.005f, 0.2f);
                }
                else {
                    Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                }
                if (Projectile.velocity.Length() < 13) {
                    Projectile.velocity *= 1.03f;
                }
            }
            else if (Projectile.velocity.LengthSquared() < 3) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.timeLeft -= 15;
            for (int i = 0; i < 36; i++) {
                Vector2 vr = CWRUtils.randVr(13);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.SpectreStaff, vr.X, vr.Y, 200, default, Main.rand.NextFloat(1, 2.2f));
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 36; i++) {
                Vector2 vr = CWRUtils.randVr(13);
                BasePRT particle = new PRT_Light(Projectile.Center, vr, Main.rand.NextFloat(0.2f, 0.8f), Color.White, 23);
                PRTLoader.AddParticle(particle);
            }
        }
    }
}
