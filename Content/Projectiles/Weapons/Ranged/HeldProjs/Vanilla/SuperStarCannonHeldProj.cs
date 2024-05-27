using CalamityOverhaul.Common;
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
        public override int targetCayItem => ItemID.SuperStarCannon;
        public override int targetCWRItem => ItemID.SuperStarCannon;
        public override void SetRangedProperty() {
            kreloadMaxTime = 60;
            FireTime = 15;
            ShootPosToMouLengValue = 30;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 5;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 8;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            SpwanGunDustMngsData.dustID1 = 15;
            SpwanGunDustMngsData.dustID2 = 57;
            SpwanGunDustMngsData.dustID3 = 58;
        }

        public override bool KreLoadFulfill() {
            FireTime = 15;
            return true;
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 15, dustID2: 57, dustID3: 58);
            SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
            if (FireTime > 6) {
                FireTime--;
            }
            _ = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
