using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ClockworkAssaultRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ClockworkAssaultRifle].Value;
        public override int targetCayItem => ItemID.ClockworkAssaultRifle;
        public override int targetCWRItem => ItemID.ClockworkAssaultRifle;
        public override void SetRangedProperty() {
            //fireTime = 10;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -13);
            }
        }

        public override void OnKreLoad() {
            if (BulletNum < 15) {
                BulletNum += 15;
            } else {
                BulletNum = 30;
            }
            if (heldItem.CWR().AmmoCapacityInFire) {
                heldItem.CWR().AmmoCapacityInFire = false;
            }
        }

        public override void PostFiringShoot() {
            if (BulletNum >= 1) {
                BulletNum -= 1;
            }
        }

        int cdcount = 0;
        public override void FiringShoot() {
            heldItem.useTime.Domp();
            SpawnGunFireDust();
            cdcount ++;
            if (cdcount == 3) {
                heldItem.useTime = 600;
                cdcount = 0;
            }
            else if (cdcount == 4) {
                heldItem.useTime = 12;
                cdcount = 0;
            } 
            //else {
            //    heldItem.useTime = 4;
            //}
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            _ = CreateRecoil();
        }
    }
}
