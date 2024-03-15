using CalamityMod;
using Microsoft.Xna.Framework;
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
            int arrowTypes;
            int weaponDamage;
            float weaponKnockback;
            bool haveAmmo = Owner.PickAmmo(Owner.ActiveItem(), out arrowTypes, out _, out weaponDamage, out weaponKnockback, out _, false);
            if (haveAmmo) {
                for (int i = 0; i < 74; i++) {
                    float rot = MathHelper.TwoPi / 64 * i;
                    Vector2 velocity = rot.ToRotationVector2() * (5 + (-1f + rot % MathHelper.PiOver4) * 11);
                    int proj = Projectile.NewProjectile(Owner.parent(), Projectile.Center, velocity
                        , arrowTypes, weaponDamage, weaponKnockback, Owner.whoAmI);
                    Main.projectile[proj].extraUpdates += 3;
                    Main.projectile[proj].timeLeft = 360;
                }
                return;
            }
        }
    }
}
