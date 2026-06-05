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
    //万象霜天
    internal class UniversalFrost : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "UniversalFrost";
        public override void SetDefaults() {
            Item.damage = 188;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 96;
            Item.height = 38;
            Item.useTime = Item.useAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2.5f;
            Item.value = Item.buyPrice(0, 32, 0, 0);
            Item.rare = CWRID.Rarity_CosmicPurple;
            Item.UseSound = CWRSound.Gun_Snowblindness_Shoot with { Volume = 0.35f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 32f;
            Item.crit = 12;
            Item.useAmmo = AmmoID.Snowball;
            Item.SetHeldProj<UniversalFrostHeld>();
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(ItemID.LunarBar, 8).
                AddTile(TileID.LunarCraftingStation).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<CrystalDimming>().
                AddIngredient(CWRID.Item_CosmiliteBar, 5).
                AddIngredient(CWRID.Item_EndothermicEnergy, 20).
                AddIngredient(CWRID.Item_EssenceofEleum, 3).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }

    internal class UniversalFrostHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "UniversalFrostHeld";
        public override int TargetID => ModContent.ItemType<UniversalFrost>();
        private int fireIndex2;
        private int onFireTime;
        private int onFireTime2;
        private int blizzardFieldTimer;
        private int fireRateValue = 18;
        public override void SetRangedProperty() {
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
                if (fireRateValue > 35) {
                    fireRateValue = 12;
                }
            }

            if (blizzardFieldTimer > 0) {
                blizzardFieldTimer--;
            }
        }

        public override void FiringShoot() {
            _ = UpdateConsumeAmmo();
            for (int i = 0; i < 35; i++) {
                Vector2 vr = ShootVelocity.RotateRandom(0.08f) * Main.rand.NextFloat(0.8f, 1.15f);
                int index2 = Dust.NewDust(ShootPos, 1, 1, DustID.BlueCrystalShard, vr.X, vr.Y, 0, default, 1.2f);
                Main.dust[index2].noGravity = true;
            }

            if (onFireTime > 0) {
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
                fireRateValue = 6;
                return;
            }

            GunPressure = 0;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.5f;

            fireIndex++;

            if (fireIndex > 1) {
                if (fireRateValue > 5) {
                    fireRateValue--;
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
                if (Main.rand.NextBool(4) && fireRateValue <= 12) {
                    proj.scale += Main.rand.NextFloat(0.4f);
                }
                if (Main.rand.NextBool(3) && fireRateValue <= 8) {
                    proj.extraUpdates += 1;
                    proj.penetrate += 6;
                }
            }

            Projectile frostNova = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity / 2.2f
                , ModContent.ProjectileType<FrostNovaOrb>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 0);
            frostNova.rotation = frostNova.velocity.ToRotation() + MathHelper.PiOver2;

            if (fireRateValue <= 6) {
                fireIndex2++;
                if (fireIndex2 > 25) {
                    fireRateValue = 60;
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

    internal class FrostBlizzardField : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        private float pulsePhase;
        private float expansionProgress;
        private const int MaxRadius = 320;
        private const int ExpandDuration = 25;
        private const int SustainDuration = 140;
        private const int FadeDuration = 20;

        public override void SetDefaults() {
            Projectile.width = MaxRadius * 2;
            Projectile.height = MaxRadius * 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ExpandDuration + SustainDuration + FadeDuration;
            Projectile.hide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.ArmorPenetration = 15;
        }

        public override void AI() {
            int totalTime = ExpandDuration + SustainDuration + FadeDuration;
            int currentPhase = totalTime - Projectile.timeLeft;

            if (currentPhase < ExpandDuration) {
                expansionProgress = currentPhase / (float)ExpandDuration;
            }
            else if (currentPhase < ExpandDuration + SustainDuration) {
                expansionProgress = 1f;
            }
            else {
                int fadeTime = currentPhase - ExpandDuration - SustainDuration;
                expansionProgress = 1f - (fadeTime / (float)FadeDuration);
            }

            Projectile.scale = expansionProgress;
            pulsePhase += 0.1f;

            if (Main.rand.NextBool(2) && expansionProgress > 0.3f) {
                Vector2 randomPos = Projectile.Center + Main.rand.NextVector2Circular(MaxRadius * expansionProgress, MaxRadius * expansionProgress);
                Dust snow = Dust.NewDustPerfect(randomPos, DustID.SnowflakeIce
                    , new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 4f)), 0, default, Main.rand.NextFloat(2f, 3.5f));
                snow.noGravity = true;
            }

            if (Main.rand.NextBool(3) && expansionProgress > 0.5f) {
                Vector2 randomPos = Projectile.Center + Main.rand.NextVector2Circular(MaxRadius * expansionProgress * 0.8f, MaxRadius * expansionProgress * 0.8f);
                Dust frost = Dust.NewDustPerfect(randomPos, DustID.IceTorch
                    , Main.rand.NextVector2Circular(3f, 3f), 0, new Color(200, 230, 255), Main.rand.NextFloat(1.5f, 2.5f));
                frost.noGravity = true;
            }

            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            float lightRadius = MaxRadius * expansionProgress * 0.5f;
            Lighting.AddLight(Projectile.Center, 0.4f * pulse * expansionProgress, 0.7f * pulse * expansionProgress, 1.0f * pulse * expansionProgress);

            if (currentPhase == ExpandDuration) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.6f, Pitch = -0.4f }, Projectile.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Chilled, 180);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float currentRadius = MaxRadius * expansionProgress;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, currentRadius, targetHitbox);
        }

        public override bool? CanDamage() {
            int currentPhase = (ExpandDuration + SustainDuration + FadeDuration) - Projectile.timeLeft;
            if (currentPhase < ExpandDuration) {
                return false;
            }
            if (currentPhase >= ExpandDuration + SustainDuration) {
                return false;
            }
            return null;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 80; i++) {
                Vector2 randomVelocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust frost = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, randomVelocity, 0
                    , new Color(200, 230, 255), Main.rand.NextFloat(2f, 3.5f));
                frost.noGravity = true;
            }

            for (int i = 0; i < 60; i++) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , Main.rand.NextVector2Circular(7f, 7f), 0, default, Main.rand.NextFloat(2.5f, 4f));
                snow.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (expansionProgress < 0.01f) {
                return false;
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            float alpha = expansionProgress * 0.9f;

            if (expansionProgress > 0.85f) {
                alpha = 1f - ((expansionProgress - 0.85f) / 0.15f) * 0.3f;
            }

            Vector2 scale = new Vector2(MaxRadius * 2, MaxRadius * 2) / texture.Size() * expansionProgress;

            Color drawColor = new Color(150, 210, 240) * alpha * 0.8f;
            Main.EntitySpriteDraw(texture, drawPosition, null, drawColor, pulsePhase * 0.5f, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, null, drawColor, -pulsePhase * 0.5f, origin, scale, SpriteEffects.None, 0);

            Color innerColor = new Color(180, 230, 255) * alpha * 0.6f;
            Main.EntitySpriteDraw(texture, drawPosition, null, innerColor, pulsePhase * 0.8f, origin, scale * 0.8f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPosition, null, innerColor, -pulsePhase * 0.8f, origin, scale * 0.8f, SpriteEffects.None, 0);

            float pulse = (float)Math.Sin(pulsePhase * 1.5f) * 0.3f + 0.7f;
            Color coreColor = new Color(200, 240, 255) * alpha * pulse * 0.5f;
            Main.EntitySpriteDraw(texture, drawPosition, null, coreColor, pulsePhase, origin, scale * 0.5f, SpriteEffects.None, 0);

            return false;
        }
    }

    internal class FrostNovaOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "Crystal";
        private float glowIntensity;
        private float rotationSpeed;
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
        }
        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 12;
            Projectile.MaxUpdates = 2;
            Projectile.friendly = true;
            rotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f);
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 4);
            Projectile.rotation += rotationSpeed;
            glowIntensity = 0.5f + (float)Math.Sin(Projectile.ai[0] * 0.15f) * 0.5f;

            if (Projectile.ai[1] > 0) {
                NPC target = Projectile.Center.FindClosestNPC(800, false, true);
                if (target != null) {
                    float distance = target.Center.Distance(Projectile.Center);
                    if (distance > 150) {
                        Projectile.SmoothHomingBehavior(target.Center, 1.2f, 0.28f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }

            if (Main.rand.NextBool(3)) {
                Vector2 dspeed = -Projectile.velocity * 0.5f;
                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.BlueCrystalShard, dspeed.X, dspeed.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            if (Main.rand.NextBool(5)) {
                Dust snow = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                    , DustID.SnowflakeIce, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                snow.velocity = -Projectile.velocity * 0.3f;
                snow.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f * glowIntensity, 0.8f * glowIntensity, 1.2f * glowIntensity);

            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 240);
            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                ExplodeEffect();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] == 0) {
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f, Volume = 0.5f }, Projectile.position);

                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * 1.8f;
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y * 1.8f;
                }

                for (int i = 0; i < 4; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-4, 4), -4);
                    Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Bottom + new Vector2(Main.rand.Next(-20, 20), 0), velocity
                    , ProjectileID.DeerclopsIceSpike, 28, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.85f, 1.15f));
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 25;
                    proj.light = 0.8f;
                }
            }
            Projectile.ai[1]++;
            return false;
        }

        private void ExplodeEffect() {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);

            for (int i = 0; i < 60; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(12f, 12f);
                Dust frost = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard, velocity, 0, default, Main.rand.NextFloat(2f, 3.5f));
                frost.noGravity = true;
                frost.fadeIn = 1.5f;
            }

            for (int i = 0; i < 40; i++) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , Main.rand.NextVector2Circular(10f, 10f), 0, default, Main.rand.NextFloat(2.5f, 4f));
                snow.noGravity = true;
            }

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 8f;
                Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Center, velocity, ProjectileID.FrostBeam, Projectile.damage, 2f, Projectile.owner);
                proj.hostile = false;
                proj.friendly = true;
                proj.DamageType = DamageClass.Ranged;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                proj.ArmorPenetration = 30;
            }
        }

        public override void OnKill(int timeLeft) {
            if (timeLeft > 0) {
                ExplodeEffect();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle frame = value.GetRectangle(Projectile.frame, 4);
            Vector2 origin = frame.Size() / 2f;

            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.15f + i * 0.2f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f) * 0.8f;
                Main.EntitySpriteDraw(value, drawPosition, frame, new Color(100, 200, 255, 0) * glowAlpha
                    , Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            Color mainColor = Color.Lerp(lightColor, new Color(200, 230, 255), glowIntensity * 0.8f);
            Main.EntitySpriteDraw(value, drawPosition, frame, mainColor
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(value, drawPosition, frame, new Color(220, 240, 255, 0) * glowIntensity * 0.9f
                , Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

            if (glowIntensity > 0.7f) {
                Main.EntitySpriteDraw(value, drawPosition, frame, Color.White * glowIntensity * 0.7f
                    , Projectile.rotation, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
