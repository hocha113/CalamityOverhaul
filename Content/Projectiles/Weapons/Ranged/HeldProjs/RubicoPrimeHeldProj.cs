using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class RubicoPrimeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "RubicoPrime";
        public override int targetCayItem => ModContent.ItemType<RubicoPrime>();
        public override int targetCWRItem => ModContent.ItemType<RubicoPrimeEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void PreInOwnerUpdate() {
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = -MathHelper.ToRadians(50) * DirSign;
                FeederOffsetPos = new Vector2(DirSign * -3, -25);
            }
        }

        public override void PostInOwnerUpdate() {
            if (!onFire && IsKreload) {
                if (++fireIndex > 50) {
                    NPC target = Projectile.Center.FindClosestNPC(1900, false, true);
                    if (target != null) {
                        UpdateMagazineContents();
                        SoundEngine.PlaySound(CommonCalamitySounds.LargeWeaponFireSound with { Volume = CommonCalamitySounds.LargeWeaponFireSound.Volume * 0.45f, Pitch = 0.2f }, Projectile.Center);
                        Vector2 vr = GunShootPos.To(target.Center).UnitVector() * ScaleFactor;
                        Projectile.NewProjectile(Source, GunShootPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        SpawnGunFireDust(GunShootPos, vr);
                    }
                    fireIndex = 0;
                }
            }
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<ImpactRound>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            base.FiringShootR();
        }

        public override void PostFiringShoot() {
            base.PostFiringShoot();
        }
    }
}
