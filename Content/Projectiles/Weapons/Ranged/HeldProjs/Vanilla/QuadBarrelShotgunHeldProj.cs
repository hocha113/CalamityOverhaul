using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class QuadBarrelShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.QuadBarrelShotgun].Value;
        public override int targetCayItem => ItemID.QuadBarrelShotgun;
        public override int targetCWRItem => ItemID.QuadBarrelShotgun;
        public override void SetRangedProperty() {
            FireTime = 35;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 2.8f;
            RangeOfStress = 10;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 75;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            BulletNum += 6;
            if (Item.CWR().AmmoCapacityInFire) {
                Item.CWR().AmmoCapacityInFire = false;
            }
            return true;
        }

        public override void PostFiringShoot() {
            if (BulletNum >= 6) {
                BulletNum -= 6;
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            for (int i = 0; i < 5; i++) {
                _ = Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.36f, 0.36f)) * Main.rand.NextFloat(0.7f, 1.3f), AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            }
        }
    }
}
