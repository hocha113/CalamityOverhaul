using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BoomstickHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Boomstick].Value;
        public override int targetCayItem => ItemID.Boomstick;
        public override int targetCWRItem => ItemID.Boomstick;
        public override void SetRangedProperty() {
            fireTime = 25;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            ShootPosNorlLengValue = -20;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 8;
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

        public override Vector2 GetGunInFirePos() {
            return kreloadTimeValue == 0 ? base.GetGunInFirePos() : GetGunBodyPostion();//避免玩家试图在装弹时开火而引发动画冲突
        }

        public override float GetGunInFireRot() {
            return kreloadTimeValue == 0 ? base.GetGunInFireRot() : GetGunBodyRotation();//避免玩家试图在装弹时开火而引发动画冲突
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
            BulletNum += 4;
        }

        public override void PostFiringShoot() {
            if (BulletNum >= 4) {
                BulletNum-=4;
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.24f, 0.24f)) * Main.rand.NextFloat(0.7f, 1.2f) * 1.0f, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0); 
                _ = CreateRecoil();
            }
        }
    }
}
