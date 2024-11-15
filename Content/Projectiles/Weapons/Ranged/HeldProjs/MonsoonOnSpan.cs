using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using InnoVault;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MonsoonOnSpan : BaseOnSpanProj
    {
        protected override Color[] colors => new Color[] { Color.Blue, Color.DarkBlue, Color.LightGreen, Color.LawnGreen };
        protected override float halfSpreadAngleRate => 1.15f;
        protected override float edgeBlendLength => 0.17f;
        protected override float edgeBlendStrength => 9;
        public override float MaxCharge => 190;

        public override void SpanProjFunc() {
            ShootState shootState = Owner.GetShootState();
            if (shootState.HasAmmo) {
                for (int i = 0; i < 74; i++) {
                    float rot = MathHelper.TwoPi / 64 * i;
                    Vector2 velocity = rot.ToRotationVector2() * (5 + (-3f + rot % MathHelper.PiOver4) * 6);
                    int proj = Projectile.NewProjectile(shootState.Source, Projectile.Center, velocity
                        , shootState.AmmoTypes, shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI);
                    Main.projectile[proj].extraUpdates += 3;
                    Main.projectile[proj].timeLeft = 360;
                }
                return;
            }
        }
    }
}
