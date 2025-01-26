using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ClaretCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClaretCannon";
        public override int targetCayItem => ModContent.ItemType<ClaretCannon>();
        public override int targetCWRItem => ModContent.ItemType<ClaretCannonEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 18;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 22;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 7;
            RepeatedCartridgeChange = true;
            SpwanGunDustMngsData.dustID1 = DustID.Blood;
            SpwanGunDustMngsData.dustID2 = DustID.Water_BloodMoon;
            SpwanGunDustMngsData.dustID3 = DustID.Blood;
            EjectCasingProjSize = 1.2f;
            GunPressure = 0.2f;
            ControlForce = 0.04f;
            Recoil = 1f;
            RangeOfStress = 25;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<ClaretCannonProj>();
        }

        public override void FiringShoot() {
            base.FiringShoot();
            ShootSpeedModeFactor *= 0.9f;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.05f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            ShootSpeedModeFactor *= 0.9f;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.05f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
