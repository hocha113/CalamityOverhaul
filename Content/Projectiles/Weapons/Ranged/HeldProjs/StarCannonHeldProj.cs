using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StarCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.StarCannon].Value;
        public override int targetCayItem => ItemID.StarCannon;
        public override int targetCWRItem => ItemID.StarCannon;

        public override void SetRangedProperty() {
            kreloadMaxTime = 120;
            fireTime = 60;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 5;
            RepeatedCartridgeChange = true;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = -2f;
            RangeOfStress = 8;
        }

        public override void KreloadSoundCaseEjection() {
            base.KreloadSoundCaseEjection();
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
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

        public override void OnSpanProjFunc() {
            SpawnGunDust(GunShootPos, ShootVelocity, dustID1: 15, dustID2: 57, dustID3: 58);
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            UpdateConsumeAmmo();
            fireTime -= 10;
            if (fireTime < 6) {
                fireTime = 6;
            }
        }

        public override void OnKreLoad() {
            base.OnKreLoad();//装弹
            fireTime = 60;
        }
    }
}
