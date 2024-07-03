using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GunkShotHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "GunkShot";
        public override int targetCayItem => ModContent.ItemType<GunkShot>();
        public override int targetCWRItem => ModContent.ItemType<GunkShotEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 18;
            FireTime = 25;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 2.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            LoadingAA_Shotgun.loadingAmmoStarg_rot = 50;
            LoadingAA_Shotgun.loadingAmmoStarg_x = 3;
            LoadingAA_Shotgun.loadingAmmoStarg_y = 25;
            if (!MagazineSystem) {
                FireTime += 15;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                if (Main.rand.NextBool(5)) {
                    Main.projectile[proj].Calamity().allProjectilesHome = true;
                }
            }
        }
    }
}
