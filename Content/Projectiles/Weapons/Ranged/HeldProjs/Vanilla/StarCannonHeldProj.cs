using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class StarCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.StarCannon].Value;
        public override int targetCayItem => ItemID.StarCannon;
        public override int targetCWRItem => ItemID.StarCannon;

        public override void SetRangedProperty() {
            kreloadMaxTime = 60;
            FireTime = 15;
            ShootPosToMouLengValue = 30;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 5;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.9f;
            RangeOfStress = 8;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            RepeatedCartridgeChange = true;
        }

        public override bool KreLoadFulfill() {
            FireTime = 15;
            return true;
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 15, dustID2: 57, dustID3: 58);
            if (FireTime > 6) {
                FireTime--;
            }
            _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
