using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CrystalDimmingHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimmingHeld";
        public override int TargetID => ModContent.ItemType<CrystalDimming>();
        private int fireIndex2;
        private int onFireTime;
        private int onFireTime2;
        public override void SetRangedProperty() {
            Recoil = 0.3f;
            FireTime = 20;
            GunPressure = 0;
            HandIdleDistanceX = 36;
            HandIdleDistanceY = -10;
            HandFireDistanceX = 35;
            HandFireDistanceY = -8;
            AngleFirearmRest = -11;
            ShootPosNorlLengValue = 5;
            ShootPosToMouLengValue = 28;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            LoadingAA_None.gunBodyY = 50;
            SpwanGunDustMngsData.dustID1 = 76;
            SpwanGunDustMngsData.dustID2 = 149;
            SpwanGunDustMngsData.dustID3 = 76;
        }

        public override void PostInOwner() {
            if (onFire) {
                VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
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
                SoundEngine.PlaySound(SoundID.Item23 with { Pitch = (60 - onFireTime) * 0.15f, MaxInstances = 13, Volume = 0.2f + onFireTime * 0.006f }, Projectile.Center);
                if (onFireTime % 15 == 0) {
                    SpawnGunFireDust(ShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
                    onFireTime2 = 8;
                }
                if (onFireTime2 > 0) {
                    VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
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

        public override void FiringShoot() {
            for (int i = 0; i < 33; i++) {
                Vector2 vr = ShootVelocity.RotateRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f);
                int index2 = Dust.NewDust(ShootPos, 1, 1, DustID.BlueCrystalShard, vr.X, vr.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }

            if (onFireTime > 0) {
                Recoil = 5;
                GunPressure = 0.6f;
                ControlForce = 0.1f;
                RecoilRetroForceMagnitude = 15;
                RecoilOffsetRecoverValue = 0.85f;

                SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.5f, Volume = 0.3f });
                for (int i = 0; i < 9; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.75f, 1.12f)
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    proj.scale += Main.rand.NextFloat(0.3f);
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = -1;
                    if (Main.rand.NextBool(2)) {
                        Projectile proj2 = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f)
                    , ModContent.ProjectileType<FlurrystormIceChunk>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        proj2.extraUpdates += 2;
                    }
                }

                Vector2 targetPos = Main.MouseWorld;

                PunchCameraModifier modifier = new PunchCameraModifier(targetPos, (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);

                for (int i = 0; i < 128; i++) {
                    Vector2 offset = new Vector2(0, i * 16);
                    if (Framing.GetTileSafely(targetPos + offset).HasSolidTile()) {
                        targetPos += offset;
                        break;
                    }
                }

                for (int i = 0; i < 35; i++) {
                    Projectile.NewProjectile(Source, targetPos + new Vector2(0, i * -8), new Vector2(0, -13).RotatedByRandom(0.2f) * Main.rand.NextFloat(0.45f, 5.12f)
                    , ModContent.ProjectileType<IceExplosionFriend>(), WeaponDamage / 6, WeaponKnockback, Owner.whoAmI, 0);
                }

                for (int i = 0; i < 33; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-3, 3), -3);
                    Projectile proj = Projectile.NewProjectileDirect(Owner.GetShootState().Source
                    , targetPos + new Vector2(Main.rand.Next(-16, 16), Main.rand.Next(-64, 0)) + new Vector2(0, i * -16 + 64)
                    , velocity, ProjectileID.DeerclopsIceSpike, 23, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.8f, 1.1f) + i * 0.05f);
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 20;
                    proj.light = 0.75f;
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
                if (FireTime > 7) {
                    FireTime--;
                }
                fireIndex = 0;
            }

            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.7f, 1.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                proj.extraUpdates += 1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                if (Main.rand.NextBool(2)) {
                    proj.damage /= 3;
                }
                if (Main.rand.NextBool(4) && FireTime <= 15) {
                    proj.scale += Main.rand.NextFloat(0.35f);
                }
                if (Main.rand.NextBool(3) && FireTime <= 10) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 5;
                }
            }

            Projectile iceorb = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity / 2
                , ModContent.ProjectileType<IceSoulOrb>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0);
            iceorb.rotation = iceorb.velocity.ToRotation() + MathHelper.PiOver2;
            iceorb.CWR().SpanTypes = (byte)SpanTypesEnum.CrystalDimming;

            if (FireTime <= 8) {
                fireIndex2++;
                if (fireIndex2 > 20) {
                    FireTime = 50;
                    onFireTime += 60;
                    fireIndex2 = 0;
                }
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, TextureValue.GetRectangle(Projectile.frame, 5), lightColor
                , Projectile.rotation, VaultUtils.GetOrig(TextureValue, 5), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
