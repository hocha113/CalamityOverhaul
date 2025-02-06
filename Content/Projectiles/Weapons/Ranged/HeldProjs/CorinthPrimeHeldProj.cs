using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CorinthPrimeHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorinthPrime";
        public override int TargetID => ModContent.ItemType<CorinthPrime>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.2f;
            Recoil = 2;
            HandIdleDistanceX = 27;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 27;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 15;
            CanRightClick = true;
        }

        public override void PostInOwnerUpdate() {
            if (onFireR) {
                GunPressure = 0.7f;
                Recoil = 6;
                RangeOfStress = 25;
                Item.useTime = 80;
            }
            else {
                GunPressure = 0.2f;
                Recoil = 1;
                RangeOfStress = 5;
                Item.useTime = 30;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Source, Projectile.Center,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.7f, 1.1f)
                    , ModContent.ProjectileType<RealmRavagerBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            _ = UpdateConsumeAmmo();
        }

        public override void FiringShootR() {
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<CorinthPrimeAirburstGrenade>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
        }
    }
}
