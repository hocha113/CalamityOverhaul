using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TsunamiOnSpan : BaseOnSpanProj
    {
        protected override Color[] colors => new Color[] { Color.LightSkyBlue, Color.Blue, Color.LightGreen, Color.LawnGreen };
        protected override float halfSpreadAngleRate => 1.15f;
        protected override float edgeBlendLength => 0.17f;
        protected override float edgeBlendStrength => 9;
        public override float MaxCharge => 220;

        public override void SpanProjFunc() {
            int arrowTypes;
            int weaponDamage;
            float weaponKnockback;
            bool haveAmmo = Owner.PickAmmo(Owner.ActiveItem(), out arrowTypes, out _, out weaponDamage, out weaponKnockback, out _, false);
            if (haveAmmo) {
                for (int i = 0; i < 84; i++) {
                    float rot = MathHelper.TwoPi / 64 * i;
                    Vector2 velocity = rot.ToRotationVector2() * (2 + (-1f + rot % MathHelper.PiOver4) * 13);
                    int proj = Projectile.NewProjectile(Owner.FromObjectGetParent(), Projectile.Center, velocity
                        , arrowTypes, weaponDamage, weaponKnockback, Owner.whoAmI);
                    Main.projectile[proj].extraUpdates += 2;
                    Main.projectile[proj].timeLeft = 360;
                }
                return;
            }
        }
    }
}
