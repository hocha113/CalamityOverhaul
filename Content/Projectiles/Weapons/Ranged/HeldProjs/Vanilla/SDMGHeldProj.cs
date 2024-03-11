using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SDMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SDMG].Value;
        public override int targetCayItem => ItemID.SDMG;
        public override int targetCWRItem => ItemID.SDMG;
        public override void SetRangedProperty() {
            FireTime = 5;
            ShootPosToMouLengValue = 15;
            ShootPosNorlLengValue = 10;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.05f;
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
        public override bool KreLoadFulfill() {
            if (BulletNum < 200) {
                BulletNum += 100;
            } else {
                BulletNum = 300;
            }
            if (Item.CWR().AmmoCapacityInFire) {
                Item.CWR().AmmoCapacityInFire = false;
            }
            return true;
        }

        public override void PostFiringShoot() {
            if (BulletNum >= 1) {
                BulletNum -= 1;
            }
        }

        public override void FiringShoot() {
            base.FiringShoot();
        }
    }
}
