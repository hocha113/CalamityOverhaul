using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SuperStarCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SuperStarCannon].Value;
        public override int TargetID => ItemID.SuperStarCannon;
        public override void SetRangedProperty() {
            KreloadMaxTime = 60;
            FireTime = 15;
            ShootPosToMouLengValue = 30;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 5;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 8;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            RecoilRetroForceMagnitude = 6;
            SpwanGunDustMngsData.dustID1 = 15;
            SpwanGunDustMngsData.dustID2 = 57;
            SpwanGunDustMngsData.dustID3 = 58;
        }

        public override bool KreLoadFulfill() {
            FireTime = 15;
            return true;
        }

        public override void PostInOwner() {
            if (!CanFire && !MagazineSystem) {
                FireTime = 15;
            }
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(ShootPos, ShootVelocity, dustID1: 15, dustID2: 57, dustID3: 58);
        }

        public override void HanderPlaySound() {
            SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Volume = 0.2f, Pitch = 0.3f }, Projectile.Center);
        }

        public override void SetShootAttribute() {
            int minShootTime = 6;
            if (!MagazineSystem) {
                minShootTime += 1;
            }
            if (FireTime > minShootTime) {
                FireTime--;
            }
        }
    }
}
