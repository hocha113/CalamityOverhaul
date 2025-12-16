using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class PlasmaRifleHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PlasmaRifle";
        public override int TargetID => CWRID.Item_PlasmaRifle;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -5;
            ShootPosNorlLengValue = -5;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            GunPressure = 0.3f;
            ControlForce = 0.02f;
            Recoil = 0;
            CanRightClick = true;
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaRifleMain".GetSound(), Projectile.Center);
            }
            else if (onFireR) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaRifleAlt".GetSound(), Projectile.Center);
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaRifleMain".GetSound(), Projectile.Center);
            Item.useTime = 30;
            GunPressure = 0.3f;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , CWRID.Proj_PlasmaShot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaRifleAlt".GetSound(), Projectile.Center);
            Item.useTime = 10;
            GunPressure = 0.1f;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , CWRID.Proj_PlasmaBolt, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
