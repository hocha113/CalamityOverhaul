using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BulletFilledShotgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BulletFilledShotgun";
        public override int targetCayItem => ModContent.ItemType<BulletFilledShotgun>();
        public override int targetCWRItem => ModContent.ItemType<BulletFilledShotgunEcType>();
        public override void SetRangedProperty() {
            fireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.4f;
            ControlForce = 0.05f;
            Recoil = 2.8f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override bool PreFireReloadKreLoad() {
            if (BulletNum <= 0) {
                loadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                isKreload = false;
                if (heldItem.type != ItemID.None) {
                    heldItem.CWR().IsKreload = false;
                }
                BulletNum = 0;
            }
            return false;
        }

        public override void OnKreLoad() {
            if (BulletNum < heldItem.CWR().AmmoCapacity - 1) {
                onKreload = true;
                BulletNum++;
            }
            if (heldItem.CWR().AmmoCapacityInFire) {
                heldItem.CWR().AmmoCapacityInFire = false;
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 0.3f, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
                _ = CreateRecoil();
            }
        }
    }
}
