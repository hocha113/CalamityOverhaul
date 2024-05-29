using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class AnimosityOnSpan : BaseOnSpanNoDraw
    {
        public override void SpanProj() {
            ShootState shootState = Owner.GetShootState();
            float rot = Owner.Center.To(Main.MouseWorld).ToRotation();
            if (Projectile.timeLeft % 10 == 0) {
                for (int i = 0; i < 5; i++) {
                    Projectile.NewProjectile(shootState.Source, Projectile.Center,
                    (rot + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * shootState.ScaleFactor * Main.rand.NextFloat(0.6f, 1.1f)
                    , ProjectileID.BulletHighVelocity, (int)(shootState.WeaponDamage * 0.6f), shootState.WeaponKnockback, Owner.whoAmI, 0);
                }
            }
            if (Projectile.timeLeft == 1) {
                int btoole = ModLoader.GetMod("CalamityMod").Find<ModProjectile>("AnimosityBullet").Type;
                for (int i = 0; i < 5; i++) {
                    Projectile.NewProjectile(shootState.Source, Projectile.Center,
                    (rot + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * shootState.ScaleFactor * Main.rand.NextFloat(0.6f, 1.1f)
                    , btoole, (int)(shootState.WeaponDamage * 0.6f), shootState.WeaponKnockback, Owner.whoAmI, 0);
                }
            }
        }
    }
}
