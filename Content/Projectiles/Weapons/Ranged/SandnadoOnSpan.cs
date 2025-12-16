using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class SandnadoOnSpan : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public Player Owner => Main.player[Projectile.owner];
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 140;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.2f;
        }

        public override void AI() {
            Color newColor3 = Color.Yellow;
            for (int j = 0; j < 12; j++) {
                if (Main.rand.NextBool(3)) {
                    Vector2 dustVel = Vector2.UnitY.RotatedBy((double)(j * 3.14159274f), default).RotatedBy(Projectile.rotation, default);
                    Dust dusty = Main.dust[Dust.NewDust(Projectile.Center, 0, 0, DustID.Sandnado, 0f, 0f, 225, newColor3, 1f)];
                    dusty.noGravity = true;
                    dusty.noLight = true;
                    dusty.scale = 0.2f + j * 0.05f;
                    dusty.position = Projectile.Center;
                    dusty.velocity = dustVel * 2.5f;
                }
                Vector2 dustVel2 = Vector2.UnitY.RotatedBy((double)(j * 3.14159274f), default);
                Dust dustier = Main.dust[Dust.NewDust(Projectile.Center, 0, 0, DustID.Sandnado, 0f, 0f, 225, newColor3, 1.5f)];
                dustier.noGravity = true;
                dustier.noLight = true;
                dustier.scale = 0.2f + j * 0.05f;
                dustier.position = Projectile.Center;
                dustier.velocity = dustVel2 * 2.5f;
            }

            if (Main.rand.NextBool(30)) {
                _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(Main.rand.Next(-3, 3), -11)
                    , CWRID.Proj_SandstormBullet, Projectile.damage, 0f, Main.myPlayer);
            }
            Projectile.ai[0]++;
        }
    }
}
