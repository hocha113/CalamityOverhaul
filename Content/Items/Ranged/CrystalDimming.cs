using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CrystalDimming : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimming";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 122;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = -0.1f };
            Item.SetHeldProj<CrystalDimmingHeld>();
            Item.value = Item.buyPrice(0, 16, 75, 0);
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient(ItemID.LunarBar, 5).
                AddTile(TileID.LunarCraftingStation).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<Snowblindness>().
                AddIngredient(CWRID.Item_PridefulHuntersPlanarRipper, 1).
                AddIngredient(CWRID.Item_RuinousSoul, 12).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class CrystalDimmingHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "CrystalDimmingHeld";
        public override int TargetID => ModContent.ItemType<CrystalDimming>();
        private int fireIndex2;
        private int onFireTime;
        private int onFireTime2;
        private int fireRateValue = 20;
        public override void SetRangedProperty() {
            GunPressure = 0;
            HandIdleDistanceX = 36;
            HandIdleDistanceY = -10;
            HandFireDistanceX = 35;
            HandFireDistanceY = -8;
            AngleFirearmRest = 2;
            ShootPosNorlLengValue = 5;
            ShootPosToMouLengValue = 28;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
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

                OffsetPos += VaultUtils.RandVr(8f);
                onFireTime--;
            }
            else {
                if (fireRateValue > 30) {
                    fireRateValue = 15;
                }
            }
        }

        public override void FiringShoot() {
            _ = UpdateConsumeAmmo();
            for (int i = 0; i < 33; i++) {
                Vector2 vr = ShootVelocity.RotateRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f);
                int index2 = Dust.NewDust(ShootPos, 1, 1, DustID.BlueCrystalShard, vr.X, vr.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }

            if (onFireTime > 0) {
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
                        , CWRID.Proj_FlurrystormIceChunk, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
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
                fireRateValue = 8;
                return;
            }

            GunPressure = 0;
            RecoilRetroForceMagnitude = 5;
            RecoilOffsetRecoverValue = 0.5f;

            fireIndex++;

            if (fireIndex > 1) {
                if (fireRateValue > 7) {
                    fireRateValue--;
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
                if (Main.rand.NextBool(4) && fireRateValue <= 15) {
                    proj.scale += Main.rand.NextFloat(0.35f);
                }
                if (Main.rand.NextBool(3) && fireRateValue <= 10) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 5;
                }
            }

            Projectile iceorb = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity / 2
                , ModContent.ProjectileType<IceSoulOrb>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0);
            iceorb.rotation = iceorb.velocity.ToRotation() + MathHelper.PiOver2;

            if (fireRateValue <= 8) {
                fireIndex2++;
                if (fireIndex2 > 20) {
                    fireRateValue = 50;
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

    internal class IceSoulOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "IceSoulOrb";
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
            Projectile.MaxUpdates = 2;
            Projectile.friendly = true;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.ai[1] > 0) {
                NPC target = Projectile.Center.FindClosestNPC(600, false, true);
                if (target != null) {
                    float num = target.Center.Distance(Projectile.Center);
                    if (num > 120) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.22f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }
            Projectile.ai[2] = Projectile.ai[0] >= 135
                ? Utils.Remap(Projectile.ai[0], 225f, 270, 1.5f, 0f)
                : Utils.Remap(Projectile.ai[0], 135, 225f, 0f, 1.5f);

            Projectile.ai[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] == 0) {
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * (Utils.Remap(Projectile.ai[0], 0f, 135f, 0.9f, 2f));
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y * (Utils.Remap(Projectile.ai[0], 0f, 135f, 0.9f, 2f));
                }
                for (int i = 0; i < 3; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-3, 3), -3);
                    Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Bottom + new Vector2(Main.rand.Next(-16, 16), 0), velocity
                    , ProjectileID.DeerclopsIceSpike, 23, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.8f, 1.1f));
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 20;
                    proj.light = 0.75f;
                }
            }
            Projectile.ai[1]++;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(value, drawPosition, value.GetRectangle(Projectile.frame, 4)
                , Color.White, Projectile.rotation, VaultUtils.GetOrig(value, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
