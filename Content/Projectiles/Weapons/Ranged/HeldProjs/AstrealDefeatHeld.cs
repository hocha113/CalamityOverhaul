using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AstrealDefeatHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstrealDefeat";
        public override void SetShootAttribute() {
            FiringDefaultSound = false;
            if (++fireIndex >= 3) {
                FiringDefaultSound = true;
                fireIndex = 0;
            }
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                , CWRID.Proj_AstrealArrow, WeaponDamage, WeaponKnockback, Owner.whoAmI, Main.rand.Next(4));
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
