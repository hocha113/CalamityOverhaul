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
    internal class UniversalFrostHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "UniversalFrostHeld";
        public override int TargetID => ModContent.ItemType<UniversalFrost>();
        private int fireIndex2;
        private int onFireTime;
        private int onFireTime2;
        private int blizzardFieldTimer;
        public override void SetRangedProperty() {
            Recoil = 0.25f;
            FireTime = 18;
            GunPressure = 0;
            HandIdleDistanceX = 42;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 40;
            HandFireDistanceY = -4;
            AngleFirearmRest = 12;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 32;
            RecoilRetroForceMagnitude = 6;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            LoadingAA_None.gunBodyY = 55;
            SpwanGunDustData.dustID1 = 76;
            SpwanGunDustData.dustID2 = 149;
            SpwanGunDustData.dustID3 = 76;
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
                FireTime = 18;
            }

            if (onFireTime2 > 0) {
                onFireTime2--;
            }

            if (onFireTime > 0) {
                SoundEngine.PlaySound(SoundID.Item23 with { Pitch = (70 - onFireTime) * 0.18f, MaxInstances = 15, Volume = 0.25f + onFireTime * 0.008f }, Projectile.Center);
                if (onFireTime % 12 == 0) {
                    SpawnGunFireDust(ShootPos, ShootVelocity, splNum: 3.5f, dustID1: 76, dustID2: 149, dustID3: 76);
                    onFireTime2 = 10;
                }
                if (onFireTime2 > 0) {
                    VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
                }
                else {
                    Projectile.frame = 4;
                }

                OffsetPos += VaultUtils.RandVr(10f);
                onFireTime--;
            }
            else {
                if (FireTime > 35) {
                    FireTime = 12;
                }
            }

            if (blizzardFieldTimer > 0) {
                blizzardFieldTimer--;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 35; i++) {
                Vector2 vr = ShootVelocity.RotateRandom(0.08f) * Main.rand.NextFloat(0.8f, 1.15f);
                int index2 = Dust.NewDust(ShootPos, 1, 1, DustID.BlueCrystalShard, vr.X, vr.Y, 0, default, 1.2f);
                Main.dust[index2].noGravity = true;
            }

            if (onFireTime > 0) {
                Recoil = 6;
                GunPressure = 0.8f;
                ControlForce = 0.12f;
                RecoilRetroForceMagnitude = 18;
                RecoilOffsetRecoverValue = 0.88f;

                SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.6f, Volume = 0.35f });

                for (int i = 0; i < 12; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.15f) * Main.rand.NextFloat(0.8f, 1.15f)
                    , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    proj.scale += Main.rand.NextFloat(0.35f);
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = -1;
                    if (Main.rand.NextBool(2)) {
                        Projectile proj2 = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.08f) * Main.rand.NextFloat(0.8f, 1.15f)
                        , CWRID.Proj_FlurrystormIceChunk, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        proj2.extraUpdates += 3;
                    }
                }

                Vector2 targetPos = Main.MouseWorld;

                PunchCameraModifier modifier = new PunchCameraModifier(targetPos, (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 25f, 8f, 25, 1200f, FullName);
                Main.instance.CameraModifiers.Add(modifier);

                for (int i = 0; i < 150; i++) {
                    Vector2 offset = new Vector2(0, i * 18);
                    if (Framing.GetTileSafely(targetPos + offset).HasSolidTile()) {
                        targetPos += offset;
                        break;
                    }
                }

                for (int i = 0; i < 45; i++) {
                    Projectile.NewProjectile(Source, targetPos + new Vector2(0, i * -10), new Vector2(0, -15).RotatedByRandom(0.25f) * Main.rand.NextFloat(0.5f, 6f)
                    , ModContent.ProjectileType<IceExplosionFriend>(), WeaponDamage / 5, WeaponKnockback, Owner.whoAmI, 0);
                }

                for (int i = 0; i < 40; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-4, 4), -4);
                    Projectile proj = Projectile.NewProjectileDirect(Owner.GetShootState().Source
                    , targetPos + new Vector2(Main.rand.Next(-20, 20), Main.rand.Next(-80, 0)) + new Vector2(0, i * -20 + 80)
                    , velocity, ProjectileID.DeerclopsIceSpike, 28, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.9f, 1.2f) + i * 0.06f);
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 18;
                    proj.light = 0.85f;
                }

                if (blizzardFieldTimer <= 0) {
                    Projectile blizzardField = Projectile.NewProjectileDirect(Source, targetPos, Vector2.Zero
                        , ModContent.ProjectileType<FrostBlizzardField>(), WeaponDamage / 3, 0, Owner.whoAmI);
                    blizzardField.usesLocalNPCImmunity = true;
                    blizzardField.localNPCHitCooldown = 15;
                    blizzardFieldTimer = 180;
                }

                ShootCoolingValue = 18;
                FireTime = 6;
                return;
            }

            Recoil = 0.45f;
            GunPressure = 0;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.5f;

            fireIndex++;

            if (fireIndex > 1) {
                if (FireTime > 5) {
                    FireTime--;
                }
                fireIndex = 0;
            }

            for (int i = 0; i < 4; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.08f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                proj.extraUpdates += 1;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                if (Main.rand.NextBool(2)) {
                    proj.damage /= 3;
                }
                if (Main.rand.NextBool(4) && FireTime <= 12) {
                    proj.scale += Main.rand.NextFloat(0.4f);
                }
                if (Main.rand.NextBool(3) && FireTime <= 8) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 6;
                }
            }

            Projectile frostNova = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity / 2.2f
                , ModContent.ProjectileType<FrostNovaOrb>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0);
            frostNova.rotation = frostNova.velocity.ToRotation() + MathHelper.PiOver2;
            frostNova.CWR().SpanTypes = (byte)SpanTypesEnum.UniversalFrost;

            if (FireTime <= 6) {
                fireIndex2++;
                if (fireIndex2 > 25) {
                    FireTime = 60;
                    onFireTime += 70;
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
