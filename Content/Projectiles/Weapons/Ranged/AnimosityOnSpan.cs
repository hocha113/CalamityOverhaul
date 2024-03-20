using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class AnimosityOnSpan : BaseOnSpanNoDraw
    {
        public override void SpanProj() {
            if (Projectile.timeLeft % 10 == 0) {
                ShootState shootState = Owner.GetShootState();
                float rot = Owner.Center.To(Main.MouseWorld).ToRotation();
                for (int i = 0; i < 6; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                    (rot + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * shootState.ScaleFactor * Main.rand.NextFloat(0.6f, 1.1f)
                    , ProjectileID.BulletHighVelocity, shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI, 0);
                }
            }
        }
    }
}
