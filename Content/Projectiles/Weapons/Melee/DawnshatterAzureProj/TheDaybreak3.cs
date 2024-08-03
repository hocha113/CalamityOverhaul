using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class TheDaybreak3 : TheDaybreak
    {
        public Vector2 origPos {
            get {
                return new Vector2(Projectile.ai[0], Projectile.ai[1]);
            }
            set {
                Projectile.ai[0] = value.X;
                Projectile.ai[1] = value.Y;
            }
        }

        public override void OnKill(int timeLeft) {
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 17f);
            Projectile.Explode(1220, Supernova.ExplosionSound with { Pitch = 0.8f });
            Vector2 vr = origPos.To(Projectile.Center).UnitVector();
            for (int i = 0; i < 3; i++) {
                BaseParticle particle = new PRK_Light(Projectile.Center, vr * (13 * (i + 1)), Main.rand.NextFloat(0.3f, 0.7f), Color.OrangeRed, 22, 0.2f);
                DRKLoader.AddParticle(particle);
                BaseParticle particle2 = new PRK_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), CWRUtils.randVr(3, 16)
                    , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.DarkRed)
                    , 13, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
                DRKLoader.AddParticle(particle2);
            }
        }
    }
}
