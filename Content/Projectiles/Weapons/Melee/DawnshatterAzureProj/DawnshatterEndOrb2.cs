using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterEndOrb2 : DawnshatterEndOrb
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.timeLeft = 70;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<MakeDamage>(), Projectile.damage * 5, 0f, Projectile.owner);
                for (int i = 0; i < 4; i++) {
                    float rot = MathHelper.PiOver2 * i;
                    Vector2 vr = rot.ToRotationVector2() * 10;
                    for (int j = 0; j < 126; j++) {
                        HeavenfallStarParticle spark = new HeavenfallStarParticle(Projectile.Center, vr * (0.3f + j * 0.1f), false, 37, Main.rand.Next(3, 17), Color.Gold);
                        DRKLoader.AddParticle(spark);
                    }
                }
            }
            Projectile.Explode(3000, spanSound: false);
        }
    }
}
