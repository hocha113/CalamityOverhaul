using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ClamorRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClamorRifle";
        public override int TargetID => ModContent.ItemType<ClamorRifle>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 50;
            FireTime = 14;
            HandIdleDistanceX = 24;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 24;
            HandFireDistanceY = -4;
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
