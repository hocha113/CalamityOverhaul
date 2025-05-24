using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class PlasmaRifleHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PlasmaRifle";
        public override int TargetID => ModContent.ItemType<PlasmaRifle>();
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
                SoundEngine.PlaySound(PlasmaRifle.HeavyShotSound, Projectile.Center);
            }
            else if (onFireR) {
                SoundEngine.PlaySound(PlasmaRifle.FastShotSound, Projectile.Center);
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(PlasmaRifle.HeavyShotSound, Projectile.Center);
            Item.useTime = 30;
            GunPressure = 0.3f;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<PlasmaShot>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            SoundEngine.PlaySound(PlasmaRifle.FastShotSound, Projectile.Center);
            Item.useTime = 10;
            GunPressure = 0.1f;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<PlasmaBolt>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
