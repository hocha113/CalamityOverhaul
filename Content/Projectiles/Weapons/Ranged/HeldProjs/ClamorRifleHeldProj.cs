using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ClamorRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClamorRifle";
        public override int targetCayItem => ModContent.ItemType<ClamorRifle>();
        public override int targetCWRItem => ModContent.ItemType<ClamorRifleEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 50;
            FireTime = 15;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -0;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.6f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
            RecoilRetroForceMagnitude = 7;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<ClamorRifleProj>();
        }
    }
}
