using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class FlurrystormCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "FlurrystormCannonShooting";
        public override int targetCayItem => ModContent.ItemType<FlurrystormCannon>();
        public override int targetCWRItem => ModContent.ItemType<FlurrystormCannonEcType>();

        private int fireIndex;
        private int fireIndex2;
        private int onFireTime;
        public override void SetRangedProperty() {
            Recoil = 0.3f;
            FireTime = 20;
            GunPressure = 0;
            HandDistance = 20;
            HandDistanceY = 0;
            HandFireDistance = 16;
            HandFireDistanceY = -8;
            AngleFirearmRest = -11;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 18;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
        }

        public override void PostInOwnerUpdate() {
            if (onFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 5, 1);
            }
            if (kreloadTimeValue > 0) {
                fireIndex = 0;
                FireTime = 20;
            }
            if (onFireTime > 0) {
                SoundEngine.PlaySound(SoundID.Item23 with { Pitch = (60 - onFireTime) * 0.15f, MaxInstances = 13, Volume = 0.2f }, Projectile.Center);
                if (onFireTime % 15 == 0) {
                    SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
                }
                OffsetPos += CWRUtils.randVr(5f);
                onFireTime--;
            }
        }

        public override void FiringShoot() {
            if (onFireTime > 0) {
                Recoil = 5;
                GunPressure = 0.6f;
                ControlForce = 0.1f;
                RecoilRetroForceMagnitude = 15;
                RecoilOffsetRecoverValue = 0.85f;
                SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.6f, Volume = 0.2f });
                for (int i = 0; i < 16; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.75f, 1.12f)
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    proj.scale += Main.rand.NextFloat(0.3f);
                    if (Main.rand.NextBool(2)) {
                        Projectile proj2 = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f)
                    , ModContent.ProjectileType<FlurrystormIceChunk>(), (int)(WeaponDamage * 0.7f), WeaponKnockback, Owner.whoAmI, 0);
                        proj2.extraUpdates += 2;
                    }
                }
                ShootCoolingValue = 15;
                FireTime = 8;
                return;
            }

            Recoil = 0.5f;
            GunPressure = 0;
            RecoilRetroForceMagnitude = 5;
            RecoilOffsetRecoverValue = 0.5f;

            fireIndex++;

            if (fireIndex > 1) {
                if (FireTime > 8) {
                    FireTime--;
                }
                fireIndex = 0;
            }

            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.7f, 1.1f)
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                if (Main.rand.NextBool(3)) {
                    proj.damage /= 3;
                }
                if (Main.rand.NextBool(4) && FireTime <= 15) {
                    proj.scale += Main.rand.NextFloat(0.35f);
                }
                if (Main.rand.NextBool(3) && FireTime <= 10) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 5;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 30;
                }
            }

            if (FireTime <= 15 && Main.rand.NextBool(3)) {
                Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<FlurrystormIceChunk>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ShootVelocity.Y);
            }

            if (FireTime <= 8) {
                fireIndex2++;
                if (fireIndex2 > 20) {
                    FireTime = 50;
                    onFireTime += 60;
                    fireIndex2 = 0;
                }
            }
        }

        public override void GunDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(TextureValue, Projectile.frame, 2), onFire ? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 2), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
