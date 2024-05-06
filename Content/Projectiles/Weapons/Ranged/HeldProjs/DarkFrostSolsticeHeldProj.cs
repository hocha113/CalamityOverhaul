using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Turret;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using System;
using Terraria.Graphics.CameraModifiers;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DarkFrostSolsticeHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "DarkFrostSolsticeHeld";
        public override int targetCayItem => ModContent.ItemType<DarkFrostSolstice>();
        public override int targetCWRItem => ModContent.ItemType<DarkFrostSolstice>();
        int fireIndex;
        int fireIndex2;
        int onFireTime;
        int onFireTime2;
        public override void SetRangedProperty() {
            Recoil = 0.3f;
            FireTime = 20;
            GunPressure = 0;
            HandDistance = 80;
            HandDistanceY = 10;
            HandFireDistance = 80;
            HandFireDistanceY = 0;
            AngleFirearmRest = -1;
            ShootPosNorlLengValue = 5;
            ShootPosToMouLengValue = 18;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
        }

        public override void PostInOwnerUpdate() {
            ArmRotSengsBackNoFireOffset = -50;
            SetCompositeArm();
            if (onFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 2, 3);
            } else {
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
                    SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
                    onFireTime2 = 8;
                }
                if (onFireTime2 > 0) {
                    CWRUtils.ClockFrame(ref Projectile.frame, 2, 3);
                } else {
                    Projectile.frame = 4;
                }

                OffsetPos += CWRUtils.randVr(8f);
                onFireTime--;
            } else {
                if (FireTime > 30) {
                    FireTime = 15;
                }
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 33; i++) {
                Vector2 vr = ShootVelocity.RotateRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f);
                int index2 = Dust.NewDust(GunShootPos, 1, 1, DustID.BlueCrystalShard, vr.X, vr.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }

            if (onFireTime > 0) {
                Recoil = 5;
                GunPressure = 0.6f;
                ControlForce = 0.1f;
                RecoilRetroForceMagnitude = 15;
                RecoilOffsetRecoverValue = 0.85f;

                SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.5f, Volume = 0.5f });
                SoundEngine.PlaySound(CosmicCalamityProjectile.BelCanto with { PitchRange = (-0.1f, 0.1f), Volume = 0.9f });

                bool intile = false;
                int overdmg = 1500;
                Vector2 targetPos = Main.MouseWorld;
                for (int i = 0; i < 128; i++) {
                    Vector2 offset = new Vector2(0, i * 16);
                    if (CWRUtils.GetTile(CWRUtils.WEPosToTilePos(targetPos + offset)).HasSolidTile()) {
                        targetPos += offset;
                        intile = true;
                        break;
                    }
                }

                if (!intile) {
                    overdmg = 0;
                    targetPos.Y += 530;
                }

                for (int i = 0; i < 35; i++) {
                    Projectile.NewProjectile(Source, targetPos + new Vector2(0, i * -8), new Vector2(0, -13).RotatedByRandom(0.5f) * Main.rand.NextFloat(0.35f, 3.12f)
                    , ModContent.ProjectileType<IceExplosionFriend>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }

                for (int i = 0; i < 40; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-3, 3) * (i * 0.01f), -3);
                    Projectile proj = Projectile.NewProjectileDirect(Source2
                    , targetPos + new Vector2(Main.rand.Next(-16, 16), Main.rand.Next(-64, 0)) + new Vector2(0, i * -25 + 64)
                    , velocity, 961, WeaponDamage * 5 + overdmg, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(1f, 1.3f) + i * 0.06f);
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = -1;
                    proj.light = 0.75f;
                    proj.CWR().GetHitAttribute.SuperAttack = true;
                }

                targetPos.Y -= 700;

                Vector2 inLVr = new Vector2(-3, -0.5f);
                for (int i = 0; i < 10; i++) {
                    Vector2 velocity = inLVr;
                    velocity.Y -= Main.rand.NextFloat(0.3f);
                    Projectile proj = Projectile.NewProjectileDirect(Source2, targetPos + inLVr * i * 16, velocity, 961
                        , WeaponDamage * 3 + overdmg, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(1.8f, 2.1f) + i * 0.07f);
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = -1;
                    proj.light = 0.75f;
                }

                Vector2 inRVr = new Vector2(3, -0.5f);
                for (int i = 0; i < 10; i++) {
                    Vector2 velocity = inRVr;
                    velocity.Y -= Main.rand.NextFloat(0.3f);
                    Projectile proj = Projectile.NewProjectileDirect(Source2, targetPos + inRVr * i * 16, velocity, 961
                        , WeaponDamage * 3 + overdmg, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(1.8f, 2.1f) + i * 0.07f);
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = -1;
                    proj.light = 0.75f;
                }

                ModOwner.SetScreenShake(5.3f);
                PunchCameraModifier modifier = new PunchCameraModifier(targetPos, (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);

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
                if (FireTime > 6) {
                    FireTime--;
                }
                fireIndex = 0;
            }

            for (int i = 0; i < 3; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.7f, 1.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                proj.extraUpdates += 1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                if (Main.rand.NextBool(2)) {
                    proj.damage /= 2;
                }
                if (Main.rand.NextBool(4) && FireTime <= 15) {
                    proj.scale += Main.rand.NextFloat(0.35f);
                }
                if (Main.rand.NextBool(3) && FireTime <= 10) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 5;
                }
            }

            for (int i = 0; i < 3; i++) {
                Projectile iceorb = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.06f)
                , ModContent.ProjectileType<Crystal>(), WeaponDamage * 3, WeaponKnockback, Owner.whoAmI, 0, 0);
                iceorb.rotation = iceorb.velocity.ToRotation();
                iceorb.CWR().SpanTypes = (byte)SpanTypesEnum.CrystalDimming;
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
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(TextureValue, Projectile.frame, 5), onFire ? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 5), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
