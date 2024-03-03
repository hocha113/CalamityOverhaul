using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SurgeDriverHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SurgeDriver";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SurgeDriver>();
        public override int targetCWRItem => ModContent.ItemType<SurgeDriverEcType>();

        public override void SetRangedProperty() {
            loadTheRounds = CWRSound.CaseEjection2 with { Pitch = -0.2f };
            kreloadMaxTime = 120;
            fireTime = 20;
            HandDistance = 52;
            HandFireDistance = 52;
            HandFireDistanceY = -13;
            ShootPosNorlLengValue = -4;
            ShootPosToMouLengValue = 35;
            RepeatedCartridgeChange = true;
        }

        public override void KreloadSoundCaseEjection() {
            base.KreloadSoundCaseEjection();
        }

        public override void KreloadSoundloadTheRounds() {
            SoundEngine.PlaySound(loadTheRounds with { Pitch = - 0.3f }, Projectile.Center);
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(30) * DirSign;
                FeederOffsetPos = new Vector2(0, -23);
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

        public override void FiringShoot() {
            SpawnGunFireDust();
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity
                , ModContent.ProjectileType<PrismaticEnergyBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
