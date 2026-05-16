using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    internal class StarshipHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override int TargetID => ModContent.ItemType<Starship>();

        //预热帧数，在开火键按下后需要蓄力这么多帧才开始正式射击
        private const int WarmupTime = 30;
        //爆热累积到1所需的开火帧数
        private const float HeatAccumTicks = 450f;
        //达到最高射速时的单发间隔
        private const int MinFireInterval = 2;
        //冷却状态下的单发间隔
        private const int MaxFireInterval = 14;
        //行星生成间隔帧数
        private const int PlanetSpawnInterval = 45;

        private float heat;
        private bool reachedMax;
        private bool wasOnFire;
        private bool cometLoaded;
        private bool rightPressedLast;
        private int plasmaFireTimer;
        private int microStarTimer;
        private int planetTimer;
        private int maxRateRingTick;
        private int wingmenSpawnedTick;

        public override void SetRangedProperty() {
            GunPressure = 0.05f;
            ControlForce = 0.012f;
            HandIdleDistanceX = 36;
            HandIdleDistanceY = -2;
            HandFireDistanceX = 42;
            HandFireDistanceY = -6;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 40;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            CanCreateCaseEjection = false;
            CanCreateSpawnGunDust = false;
            CanCreateRecoilBool = false;
            FiringDefaultSound = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 1.2f;
            RecoilOffsetRecoverValue = 0.35f;
            FireLight = 1.2f;
        }

        public override void PreInOwner() {
            bool isFiring = onFire;

            //右键装填彗星特殊弹（按下边沿触发）
            bool rightPressed = DownRight;
            if (rightPressed && !rightPressedLast && HaveAmmo && !cometLoaded) {
                cometLoaded = true;
                SoundEngine.PlaySound(CWRSound.Gun_Clipin with { Pitch = 0.3f, Volume = 0.9f }, Projectile.Center);
                _ = UpdateConsumeAmmo();
            }
            rightPressedLast = rightPressed;

            //热度变化
            if (isFiring) {
                heat = Math.Min(1f, heat + 1f / HeatAccumTicks);
            }
            else {
                heat = Math.Max(0f, heat - 1f / 120f);
            }

            //初次到达最高射速的演出
            if (isFiring && heat >= 1f && !reachedMax) {
                reachedMax = true;
                maxRateRingTick = 0;
                wingmenSpawnedTick = 0;
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with { Volume = 1.2f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(CWRSound.DeploymentSound with { Volume = 1.1f, Pitch = -0.1f }, Projectile.Center);
                SpawnMaxRateRing();
                SpawnWingmen();
            }

            if (reachedMax) {
                maxRateRingTick++;
            }

            //释放扳机触发终幕
            if (wasOnFire && !isFiring && reachedMax) {
                TriggerFinale();
                ResetState();
            }
            else if (!isFiring && !reachedMax && Time > 6) {
                //普通松手时轻微降热即可
            }

            wasOnFire = isFiring;
        }

        public override bool PreFiringShoot() => false;
        public override bool CanSpanProj() => HaveAmmo;
        public override void SpanProj() { }//完全交由 PostInOwner 调度开火节奏

        public override void PostInOwner() {
            if (!HaveAmmo) {
                return;
            }

            if (!onFire) {
                return;
            }

            if (Time < WarmupTime) {
                PlayWarmupEffects();
                return;
            }

            int fireInterval = (int)MathHelper.Lerp(MaxFireInterval, MinFireInterval, heat);
            int microInterval = (int)MathHelper.Lerp(12, 3, heat);

            plasmaFireTimer--;
            microStarTimer--;

            if (plasmaFireTimer <= 0) {
                plasmaFireTimer = fireInterval;
                FirePlasmaVolley();
            }

            if (microStarTimer <= 0) {
                microStarTimer = microInterval;
                FireMicroStars();
            }

            if (reachedMax) {
                planetTimer--;
                if (planetTimer <= 0) {
                    planetTimer = PlanetSpawnInterval;
                    SpawnPlanetBehind();
                }

                //僚机兜底重生（每5秒检查）
                if (maxRateRingTick - wingmenSpawnedTick > 300) {
                    wingmenSpawnedTick = maxRateRingTick;
                    SpawnWingmen();
                }
            }
        }

        private void FirePlasmaVolley() {
            HanderPlaySound();
            Lighting.AddLight(ShootPos, 0.6f, 0.4f, 1.0f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                _ = UpdateConsumeAmmo();
                return;
            }

            Vector2 vel = ShootVelocity.RotatedByRandom(0.015f);
            if (cometLoaded) {
                cometLoaded = false;
                Projectile.NewProjectile(Source, ShootPos, vel * 1.3f
                    , ModContent.ProjectileType<StarshipComet>()
                    , WeaponDamage * 6, WeaponKnockback * 3f, Owner.whoAmI);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.3f }, Projectile.Center);
            }
            else {
                Projectile.NewProjectile(Source, ShootPos, vel
                    , ModContent.ProjectileType<StarshipPlasmaBolt>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI);
                _ = UpdateConsumeAmmo();
            }
        }

        private void FireMicroStars() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Vector2 normal = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 topPos = ShootPos + normal * 8f;
            Vector2 botPos = ShootPos - normal * 8f;
            Vector2 baseVel = ShootVelocity * 0.85f;

            Projectile.NewProjectile(Source, topPos, baseVel.RotatedByRandom(0.08f) + normal * 1.5f
                , ModContent.ProjectileType<StarshipMicroStar>()
                , Math.Max(1, WeaponDamage / 3), WeaponKnockback * 0.3f, Owner.whoAmI);
            Projectile.NewProjectile(Source, botPos, baseVel.RotatedByRandom(0.08f) - normal * 1.5f
                , ModContent.ProjectileType<StarshipMicroStar>()
                , Math.Max(1, WeaponDamage / 3), WeaponKnockback * 0.3f, Owner.whoAmI);
        }

        public override void HanderPlaySound() {
            float pitch = MathHelper.Lerp(-0.25f, 0.25f, heat);
            SoundEngine.PlaySound(CWRSound.Gun_SMG_Shoot with {
                Pitch = pitch,
                Volume = 0.42f,
                MaxInstances = 6
            }, Projectile.Center);
        }

        private void PlayWarmupEffects() {
            if (Time % 5 == 0) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.7f + Time / WarmupTime * 0.6f, Volume = 0.45f }, Projectile.Center);
            }

            for (int i = 0; i < 2; i++) {
                Vector2 circlePos = ShootPos + Main.rand.NextVector2Circular(28, 28);
                Vector2 toMuzzle = (ShootPos - circlePos).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(2.5f, 5.5f);
                Dust d = Dust.NewDustPerfect(circlePos, DustID.PinkStarfish, toMuzzle, 100, default, 1.2f);
                d.noGravity = true;
            }
            Lighting.AddLight(ShootPos, 0.3f, 0.4f, 0.8f);
        }

        private void SpawnMaxRateRing() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<StarshipMaxRateRing>()
                , 1, 0f, Owner.whoAmI);
        }

        private void SpawnWingmen() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero
                , ModContent.ProjectileType<StarshipWingman>()
                , Math.Max(1, WeaponDamage / 2), 0f, Owner.whoAmI, 1f);
            Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero
                , ModContent.ProjectileType<StarshipWingman>()
                , Math.Max(1, WeaponDamage / 2), 0f, Owner.whoAmI, -1f);
        }

        private void SpawnPlanetBehind() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            int planetType = Main.rand.Next(7);
            Vector2 origin = Owner.Center - ToMouse.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(220f, 320f)
                + Main.rand.NextVector2Circular(80f, 80f);
            Vector2 vel = origin.To(Main.MouseWorld).SafeNormalize(Vector2.UnitX) * 14f;

            Projectile.NewProjectile(Source, origin, vel
                , ModContent.ProjectileType<StarshipPlanet>()
                , WeaponDamage * 2, WeaponKnockback * 2f, Owner.whoAmI, planetType);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 3 }, origin);
        }

        private void TriggerFinale() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.3f, Pitch = -0.3f }, Projectile.Center);

            Vector2 target = Main.MouseWorld;
            int meteorCount = 14;
            for (int i = 0; i < meteorCount; i++) {
                Vector2 start = target + new Vector2(Main.rand.NextFloat(-600f, 600f), -Main.rand.NextFloat(900f, 1300f));
                Vector2 vel = (target - start).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(10f, 16f);
                int delay = i * 4 + Main.rand.Next(0, 6);
                Projectile.NewProjectile(Source, start, vel
                    , ModContent.ProjectileType<StarshipMeteor>()
                    , WeaponDamage * 4, WeaponKnockback * 2f, Owner.whoAmI, delay, meteorCount);
            }

            Projectile.NewProjectile(Source, target + Vector2.UnitY * -400f, Vector2.Zero
                , ModContent.ProjectileType<StarshipMicroGalaxy>()
                , WeaponDamage * 3, WeaponKnockback, Owner.whoAmI, target.X, target.Y);
        }

        private void ResetState() {
            heat = 0f;
            reachedMax = false;
            plasmaFireTimer = 0;
            microStarTimer = 0;
            planetTimer = 0;
            maxRateRingTick = 0;
            cometLoaded = false;
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Texture2D tex = TextureValue;
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);

            //装填彗星时的辉光
            if (cometLoaded) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = 0.65f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.25f;
                Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 160, 80, 0) * pulse * 0.8f
                    , Projectile.rotation + offsetRot, glow.Size() * 0.5f
                    , new Vector2(1.8f, 0.9f) * Projectile.scale, SpriteEffects.None, 0);
            }

            //主体
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor
                , Projectile.rotation + offsetRot, tex.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            //达到最高射速时的外发光
            if (heat > 0.05f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color glowColor = Color.Lerp(new Color(90, 140, 255, 0), new Color(255, 180, 80, 0), heat) * heat * 0.55f;
                Main.EntitySpriteDraw(glow, drawPos, null, glowColor
                    , Projectile.rotation + offsetRot, glow.Size() * 0.5f
                    , new Vector2(tex.Width / 60f, 1.4f) * Projectile.scale, SpriteEffects.None, 0);
            }
        }

        public override void PostGunDraw(Vector2 drawPos, ref Color lightColor) {
            //枪口充能光点
            if (onFire && Time > 0) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float intensity = Math.Min(1f, Time / WarmupTime) * (0.5f + heat * 0.6f);
                Vector2 muzzle = ShootPos - Main.screenPosition;
                Color col = Color.Lerp(new Color(140, 180, 255, 0), new Color(255, 210, 140, 0), heat) * intensity;
                Main.EntitySpriteDraw(glow, muzzle, null, col, 0f
                    , glow.Size() * 0.5f, 0.6f + intensity * 0.4f, SpriteEffects.None, 0);
            }
        }
    }
}
