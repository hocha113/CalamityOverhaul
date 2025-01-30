using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AcesHighHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AcesHigh";
        public override int TargetID => ModContent.ItemType<AcesHigh>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 20;
            HandIdleDistanceX = 22;
            HandIdleDistanceY = 4;
            HandFireDistanceX = 22;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.8f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
            SpwanGunDustMngsData.splNum = 0.5f;
            SpwanGunDustMngsData.dustID1 = DustID.BlueTorch;
            SpwanGunDustMngsData.dustID2 = DustID.BoneTorch;
            SpwanGunDustMngsData.dustID3 = DustID.CorruptTorch;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.gunBodyX = -3;
            LoadingAA_Handgun.gunBodyY = -10;
            LoadingAA_Handgun.Roting = -22;
        }

        public override void HanderPlaySound() {
            if (fireIndex > 2) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
        }

        public override void SetShootAttribute() {
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RecoilRetroForceMagnitude = 0;
            FireTime = 1;
            if (++fireIndex > 3) {
                Recoil = 1.2f;
                RecoilRetroForceMagnitude = 7;
                FireTime = 15;
                fireIndex = 0;
            }
            AmmoTypes = Utils.SelectRandom(Main.rand, new int[]
            {
                ModContent.ProjectileType<CardHeart>(),
                ModContent.ProjectileType<CardSpade>(),
                ModContent.ProjectileType<CardDiamond>(),
                ModContent.ProjectileType<CardClub>()
            });
        }
    }
}
