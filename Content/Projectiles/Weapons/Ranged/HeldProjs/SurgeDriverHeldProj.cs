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
            FiringDefaultSound = false;
            Recoil = 0;
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

        public override bool PreFireReloadKreLoad() {
            if (BulletNum <= 0) {
                loadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                isKreload = false;
                if (heldItem.type != ItemID.None) {
                    heldItem.CWR().IsKreload = false;
                }
                BulletNum = 0;
            }
            fireTime--;
            if (fireTime < 6) {
                fireTime = 6;
            }
            return false;
        }

        public override void OnKreLoad() {
            base.OnKreLoad();
            fireTime = 20;
        }

        public override void FiringShoot() {
            Recoil = 0;
            GunPressure = 0;
            ControlForce = 0.01f;
            RangeOfStress = 5;
            if (BulletNum > 40) {
                float sengs = (98 - BulletNum) * 0.05f;
                SoundEngine.PlaySound(heldItem.UseSound.Value with { Pitch = sengs > 0.95f ? 0.95f : sengs }, Projectile.Center);
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity * (1 + sengs)
                    , ModContent.ProjectileType<PrismEnergyBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            else {
                if (BulletNum == 1) {
                    Recoil = 7;
                    GunPressure = 0.8f;
                    ControlForce = 0.03f;
                    RangeOfStress = 55;
                    Vector2 vr = ShootVelocity.UnitVector();
                    float lengValue = 16;
                    float lengInXValue = 80;
                    for (int i = 0; i < 26; i++) {
                        Vector2 vr2 = vr * lengValue + vr.GetNormalVector() * Main.rand.NextFloat(-lengInXValue, lengInXValue);
                        lengValue += Main.rand.Next(60, 90);
                        lengInXValue += 7;
                        Projectile.NewProjectile(Owner.GetSource_FromThis(), vr2 + Projectile.Center, Vector2.Zero, ModContent.ProjectileType<PrismExplosionLarge>(), Projectile.damage, 0f, Projectile.owner);
                    }
                    SoundEngine.PlaySound(heldItem.UseSound.Value with { Pitch = -0.3f, Volume = 1.5f, MaxInstances = 3 }, Projectile.Center);
                    return;
                }
                SoundEngine.PlaySound(heldItem.UseSound.Value with { Pitch = 1f }, Projectile.Center);
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity
                    , ModContent.ProjectileType<PrismaticEnergyBlast>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
