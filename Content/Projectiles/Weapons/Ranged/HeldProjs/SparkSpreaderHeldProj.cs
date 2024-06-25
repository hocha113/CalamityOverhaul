using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SparkSpreaderHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SparkSpreader";
        public override int targetCayItem => ModContent.ItemType<SparkSpreader>();
        public override int targetCWRItem => ModContent.ItemType<SparkSpreaderEcType>();
        public override void SetRangedProperty() {
            FireTime = 12;
            HandDistance = 25;
            HandDistanceY = 4;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 28;
            kreloadMaxTime = 90;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<SparkSpreaderFire>();
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.08f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner);
        }
    }
}
