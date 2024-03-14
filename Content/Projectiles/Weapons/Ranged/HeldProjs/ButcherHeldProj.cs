using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ButcherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Butcher";
        public override int targetCayItem => ModContent.ItemType<Butcher>();
        public override int targetCWRItem => ModContent.ItemType<ButcherEcType>();
        public override void SetRangedProperty() {
            FireTime = 30;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            HandFireDistance = 15;
            ShootPosNorlLengValue = -18;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 60;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override bool KreLoadFulfill() {
            BulletNum = Item.CWR().AmmoCapacity;
            if (Item.CWR().AmmoCapacityInFire) {
                Item.CWR().AmmoCapacityInFire = false;
            }
            FireTime = 30;
            return true;
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            float randomMode = FireTime * 0.006f;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-randomMode, randomMode)) * Main.rand.NextFloat(0.6f, 1.52f) * 0.3f, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
                _ = CreateRecoil();
            }
            FireTime--;
            if (FireTime < 12) {
                FireTime = 12;
            }
        }
    }
}
