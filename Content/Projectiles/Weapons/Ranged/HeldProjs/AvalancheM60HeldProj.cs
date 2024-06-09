using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AvalancheM60HeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "AvalancheM60Held";
        public override int targetCayItem => ModContent.ItemType<AvalancheM60>();
        public override int targetCWRItem => ModContent.ItemType<AvalancheM60>();

        private int fireIndex;
        private int fireIndex2;
        private int onFireTime;
        private int onFireTime2;
        public override void SetRangedProperty() {
            Recoil = 0.3f;
            FireTime = 20;
            GunPressure = 0;
            HandDistance = 55;
            HandDistanceY = 5;
            HandFireDistance = 55;
            HandFireDistanceY = -8;
            AngleFirearmRest = -11;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 18;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
        }

        public override void PostInOwnerUpdate() {
            if (onFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 2, 3);
            }
            else {
                Projectile.frame = 4;
            }
            if (kreloadTimeValue > 0) {
                fireIndex = 0;
                FireTime = 20;
            }

            if (onFireTime2 > 0) {
                onFireTime2--;
            }

            if (onFireTime > 0) {
                SoundEngine.PlaySound(SoundID.Item23 with { Pitch = (60 - onFireTime) * 0.15f, MaxInstances = 13, Volume = 0.2f + (60 - onFireTime) * 0.006f }, Projectile.Center);
                if (onFireTime % 15 == 0) {
                    SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
                    onFireTime2 = 8;
                }
                if (onFireTime2 > 0) {
                    CWRUtils.ClockFrame(ref Projectile.frame, 2, 3);
                }
                else {
                    Projectile.frame = 4;
                }

                OffsetPos += CWRUtils.randVr(8f);
                onFireTime--;
            }
            else {
                if (FireTime > 30) {
                    FireTime = 15;
                }
            }
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
        }

        public override void FiringShoot() {
            if (onFireTime > 0) {
                Recoil = 5;
                GunPressure = 0.6f;
                ControlForce = 0.1f;
                RecoilRetroForceMagnitude = 15;
                RecoilOffsetRecoverValue = 0.85f;

                SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.5f, Volume = 0.3f });

                for (int i = 0; i < 18; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.75f, 1.12f)
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    proj.scale += Main.rand.NextFloat(0.3f);
                    if (Main.rand.NextBool(2)) {
                        Projectile proj2 = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f)
                    , ModContent.ProjectileType<FlurrystormIceChunk>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        proj2.extraUpdates += 2;
                    }
                }
                for (int i = 0; i < 33; i++) {
                    Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.15f, 1.12f)
                    , ModContent.ProjectileType<IceExplosionFriend>(), WeaponDamage / 6, WeaponKnockback, Owner.whoAmI, 0);
                }

                ShootCoolingValue = 15;
                FireTime = 8;
                return;
            }

            Recoil = 0.5f;
            GunPressure = 0;
            RecoilRetroForceMagnitude = 5;
            RecoilOffsetRecoverValue = 0.5f;

            if (FireTime > 7) {
                FireTime--;
            }

            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.7f, 1.1f)
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                proj.extraUpdates += 1;
                if (Main.rand.NextBool(2)) {
                    proj.damage /= 3;
                }
                if (Main.rand.NextBool(4) && FireTime <= 15) {
                    proj.scale += Main.rand.NextFloat(0.35f);
                }
                if (Main.rand.NextBool(3) && FireTime <= 10) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 5;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = -1;
                }
            }

            Projectile proj3 = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, ModContent.ProjectileType<ExtremeColdHail>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ShootVelocity.Y);
            proj3.rotation = proj3.velocity.ToRotation() + MathHelper.PiOver2;
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
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(TextureValue, Projectile.frame, 5), lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 5), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
