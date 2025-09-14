﻿using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
                BasePRT particle = new PRT_Light(Projectile.Center, vr * (13 * (i + 1)), Main.rand.NextFloat(0.3f, 0.7f), Color.OrangeRed, 22, 0.2f);
                PRTLoader.AddParticle(particle);
                BasePRT particle2 = new PRT_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), VaultUtils.RandVr(3, 16)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.DarkRed)
                    , 13, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
                PRTLoader.AddParticle(particle2);
            }
        }
    }
}
