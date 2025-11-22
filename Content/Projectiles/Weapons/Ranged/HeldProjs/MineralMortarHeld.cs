using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MineralMortarHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MineralMortar";
        private bool oldOnFire;
        private int chargeIndex;
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 15;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            RecoilRetroForceMagnitude = 26;
            RecoilOffsetRecoverValue = 0.65f;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = false;
            FiringDefaultSound = false;
            CanCreateRecoilBool = false;
            CanCreateSpawnGunDust = CanCreateCaseEjection = false;
            CanUpdateMagazineContentsInShootBool = false;
        }

        public override void PostInOwner() {
            if (onFire != oldOnFire && onFire) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                chargeIndex = 0;
            }
            if (onFire) {
                chargeIndex++;
                if (chargeIndex > 11) {
                    RecoilOffsetRecoverValue = 0.65f;
                    OffsetPos += VaultUtils.RandVr(0.1f + chargeIndex * 0.05f);
                }
            }
            oldOnFire = onFire;
        }

        public override void FiringShoot() {
            if (chargeIndex > 60) {
                SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Item/ScorchedEarthShot", 3) with { Volume = .2f, Pitch = 1.2f, PitchVariance = 1.1f }, Projectile.Center);
                SpawnGunFireDust(ShootPos, ShootVelocity);
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , CWRID.Proj_MineralMortarProjectile, WeaponDamage, WeaponKnockback, Owner.whoAmI, UseAmmoItemType);
                UpdateMagazineContents();
                OffsetPos -= ShootVelocity.UnitVector() * RecoilRetroForceMagnitude;
                RecoilOffsetRecoverValue = 0.9f;
                chargeIndex = 0;
            }
        }
    }
}
