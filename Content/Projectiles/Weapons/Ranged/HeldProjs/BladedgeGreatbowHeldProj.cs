using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BladedgeGreatbowHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BladedgeGreatbow";
        public override int targetCayItem => ModContent.ItemType<BladedgeGreatbow>();
        public override int targetCWRItem => ModContent.ItemType<BladedgeGreatbowEcType>();

        public override void SetRangedProperty() {
            HandDistance = 20;
            HandDistanceY = 5;
            HandFireDistance = 20;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 25;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ProjectileID.Leaf, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].DamageType = DamageClass.Ranged;
            }
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }
    }
}
