using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ShadethrowerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shadethrower";
        public override int targetCayItem => ModContent.ItemType<Shadethrower>();
        public override int targetCWRItem => ModContent.ItemType<ShadethrowerEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            FireTime = 9;
            HandDistance = 25;
            HandDistanceY = 4;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = -16;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            kreloadMaxTime = 90;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<ShadeFire>();
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 0;
            LoadingAA_None.loadingAA_None_Y = 13;
        }

        public override void SetShootAttribute() {
            if (++fireIndex > 2) {
                SoundEngine.PlaySound(Item.UseSound, GunShootPos);
                fireIndex = 0;
            }
        }
    }
}
