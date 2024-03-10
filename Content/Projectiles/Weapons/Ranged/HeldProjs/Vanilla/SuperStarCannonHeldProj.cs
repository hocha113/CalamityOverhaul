using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            Recoil = 0.8f;
            RangeOfStress = 48;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 7;
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 15, dustID2: 57, dustID3: 58);
            _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
            FireTime -= 10;
            if (FireTime < 6) {
                FireTime = 6;
            }
        }

        public override bool KreLoadFulfill() {
            FireTime = 60;
            return true;
        }
    }
}
