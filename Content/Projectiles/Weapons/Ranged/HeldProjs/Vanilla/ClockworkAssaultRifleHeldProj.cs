using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
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
        int thisNeedsTime;
        int chargeAmmoNum;
        int thisBulletNum;
        public override void SetRangedProperty() {
            FireTime = 5;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
            ReturnRemainingBullets = false;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            ReturnRemainingBullets = true;
            if (BulletNum < Item.CWR().AmmoCapacity) {
                if (!onFire) {
                    thisBulletNum += 10;
                    BulletNum = thisBulletNum;
                    CutOutMagazine(Item, thisBulletNum);
                    OnKreload = true;
                    ReturnRemainingBullets = false;
                    kreloadTimeValue = kreloadMaxTime;                    
                }
            }
            if (thisBulletNum >= Item.CWR().AmmoCapacity) {
                thisBulletNum = 0;
            }
            return false;
        }

        public override void PostFiringShoot() {
        }

        public override void PostInOwnerUpdate() {
            if (thisNeedsTime > 0) {
                onFire = false;
                thisNeedsTime--;
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            chargeAmmoNum++;
            if (chargeAmmoNum >= 3) {
                thisNeedsTime += 30;
                chargeAmmoNum = 0;
            }
        }
    }
}
