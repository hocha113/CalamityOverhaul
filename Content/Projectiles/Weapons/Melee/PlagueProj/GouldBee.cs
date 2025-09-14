using CalamityOverhaul.Content.Projectiles.Others;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj
{
    internal class GouldBee : PlagueBee
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.width = Projectile.height = 64;
            Projectile.MaxUpdates = 3;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (!VaultUtils.isServer) {//因为蜜蜂云是纯视觉效果，因此不需要在服务器上运行相关代码，因为服务器看不见这些
                if (Projectile.timeLeft > 60) {
                    for (int i = 0; i < Main.rand.Next(2, 3); i++) {
                        bees.Add(new Bee(Projectile, Projectile.Center + VaultUtils.RandVr(Projectile.width + 10), Projectile.velocity, Main.rand.Next(37, 60)
                            , Color.White, Projectile.rotation, Main.rand.NextFloat(0.9f, 1.3f), 1, Main.rand.Next(4)));
                    }
                }
                bees.RemoveAll((Bee b) => !b.Active);
                foreach (Bee bee in bees) {
                    bee.Update();
                    if (Main.rand.NextBool(3)) {
                        int dustType = 89;
                        int plague = Dust.NewDust(bee.Center, 1, 1, dustType, bee.Velocity.X * 0.2f, bee.Velocity.Y * 0.2f, 100, default, bee.Scale);
                        Dust dust = Main.dust[plague];
                        dust.scale *= 0.6f;
                        dust.noGravity = true;
                    }
                }
            }
            if (Projectile.timeLeft < 330) {
                NPC target = Projectile.Center.FindClosestNPC(1650);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 0.995f, 0.35f);
                }
            }
            if (Projectile.velocity.LengthSquared() < 184) {
                Projectile.velocity *= 1.01f;
            }
        }

        public override bool? CanDamage() {
            return Projectile.numHits > 3 ? false : base.CanDamage();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.timeLeft > 300) {
                Projectile.tileCollide = false;
                return false;
            }
            return !Projectile.tileCollide ? false : base.OnTileCollide(oldVelocity);
        }
    }
}
