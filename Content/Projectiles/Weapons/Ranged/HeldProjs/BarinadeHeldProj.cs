using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarinadeHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinade";
        public override int TargetID => ModContent.ItemType<Barinade>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            HandIdleDistanceY = 5;
            BowstringData.DeductRectangle = new Rectangle(4, 10, 4, 32);
        }

        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.RotatedBy(-0.55f), ShootVelocity.RotatedBy(0.025f)
                , ModContent.ProjectileType<BarinadeArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI);
            Main.projectile[proj].SetArrowRot();
            proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.RotatedBy(0.55f), ShootVelocity.RotatedBy(-0.025f)
                , ModContent.ProjectileType<BarinadeArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI);
            Main.projectile[proj].SetArrowRot();
        }
    }
}
