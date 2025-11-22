using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarinadeHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinade";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            BowstringData.DeductRectangle = new Rectangle(4, 10, 4, 32);
        }

        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.RotatedBy(-0.55f), ShootVelocity.RotatedBy(0.025f)
                , CWRID.Proj_BarinadeArrow, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            Main.projectile[proj].SetArrowRot();
            proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.RotatedBy(0.55f), ShootVelocity.RotatedBy(-0.025f)
                , CWRID.Proj_BarinadeArrow, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            Main.projectile[proj].SetArrowRot();
        }
    }
}
