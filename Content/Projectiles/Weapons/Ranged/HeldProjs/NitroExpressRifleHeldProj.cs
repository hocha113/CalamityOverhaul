using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NitroExpressRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NitroExpressRifle";
        public override int targetCayItem => ModContent.ItemType<NitroExpressRifle>();
        public override int targetCWRItem => ModContent.ItemType<NitroExpressRifleEcType>();
        public override void SetRangedProperty() {
            FireTime = 50;
            Recoil = 2.2f;
            kreloadMaxTime = 25;
            RangeOfStress = 25;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 20;
            ArmRotSengsBackNoFireOffset = -30;
            AutomaticPolishingEffect = true;
            RepeatedCartridgeChange = true;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Shotgun;
            LoadingAA_Shotgun.pump = CWRSound.Gun_NERifle_SlideInt;
            //LoadingAA_Shotgun.loadShellSound = CWRSound.Gun_NERifle_ClipLocked with { Volume = 0.6f, Pitch = -0.8f }; 
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<NitroShot>();
        }
    }
}
