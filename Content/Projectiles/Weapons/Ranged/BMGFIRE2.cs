using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class BMGFIRE2 : BMGFIRE
    {
        public override void AI() {
            BasePRT particle = new PRT_Light(Projectile.Center, Projectile.velocity, Main.rand.NextFloat(0.3f, 0.7f), Color.CadetBlue, 22, 0.2f);
            PRTLoader.AddParticle(particle);
            BasePRT particle2 = new PRT_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), Projectile.velocity
                , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.AliceBlue, Color.AntiqueWhite)
                , 32, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
            PRTLoader.AddParticle(particle2);
            if (Main.rand.NextBool(Projectile.timeLeft / 3 + 1)) {
                float slp = (100 - Projectile.timeLeft) * 0.1f;
                if (slp > 5) {
                    slp = 5;
                }
                BasePRT particle3 = new PRT_Light(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(5.3f, 25.7f) * slp
                    , Projectile.velocity, Main.rand.NextFloat(0.3f, 0.7f), Color.Blue, 22, 0.2f);
                PRTLoader.AddParticle(particle3);
                BasePRT particle4 = new PRT_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f) + Main.rand.NextVector2Unit() * Main.rand.NextFloat(3.3f, 15.7f)
                    , Projectile.velocity, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.AliceBlue, Color.White)
                    , 32, Main.rand.NextFloat(0.2f, 1.1f) * slp, 0.5f, 0.1f);
                PRTLoader.AddParticle(particle4);
            }

            Projectile.ai[0]--;
        }
    }
}
