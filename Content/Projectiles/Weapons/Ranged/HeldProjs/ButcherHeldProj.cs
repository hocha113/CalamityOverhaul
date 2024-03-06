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
            fireTime = 30;
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
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override void OnKreLoad() {
            BulletNum = heldItem.CWR().AmmoCapacity;
            if (heldItem.CWR().AmmoCapacityInFire) {
                heldItem.CWR().AmmoCapacityInFire = false;
            }
            fireTime = 30;
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            float randomMode = fireTime * 0.006f;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-randomMode, randomMode)) * Main.rand.NextFloat(0.6f, 1.52f) * 0.3f, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
                _ = CreateRecoil();
            }
            fireTime--;
            if (fireTime < 12) {
                fireTime = 12;
            }
        }
    }
}
