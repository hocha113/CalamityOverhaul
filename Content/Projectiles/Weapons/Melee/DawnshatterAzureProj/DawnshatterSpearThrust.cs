using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterSpearThrust : BaseHeldProj
    {
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        private int comboStage;//连击阶段
        private bool spawnedShockwave;
        private float energyIntensity;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 124;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 38;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 4, 3);
            SetHeld();

            int maxTime = 38;
            float progress = 1f - Projectile.timeLeft / (float)maxTime;

            //判断连击阶段
            comboStage = (int)Projectile.ai[0] % 3;

            //使用更激进的曲线
            float thrustProgress;
            if (progress < 0.35f) {
                //前35%快速爆发式前刺
                thrustProgress = CWRUtils.EaseOutExpo(progress / 0.35f);
            }
            else if (progress < 0.6f) {
                //中间25%短暂停顿
                thrustProgress = 1f + (float)Math.Sin((progress - 0.35f) / 0.25f * MathHelper.Pi) * 0.15f;
            }
            else {
                //后40%快速回收
                thrustProgress = 1f - CWRUtils.EaseInCubic((progress - 0.6f) / 0.4f);
            }

            //根据连击阶段调整距离
            float maxDistance = 200f + comboStage * 30f;
            float currentDistance = MathHelper.Lerp(0, maxDistance, thrustProgress);

            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero);
            Projectile.Center = Owner.MountedCenter + Projectile.velocity * currentDistance;
            Projectile.rotation = Projectile.velocity.ToRotation();
            SetDirection();

            //能量强度随时间变化
            energyIntensity = (float)Math.Sin(progress * MathHelper.Pi) * (1f + comboStage * 0.3f);

            //持续生成华丽的粒子效果
            if (progress < 0.7f) {
                SpawnContinuousEffects(progress);
            }

            //在刺击最远处时爆发
            if (Projectile.timeLeft == maxTime / 2 && !spawnedShockwave) {
                SpawnThrustExplosion(comboStage);
                spawnedShockwave = true;
            }

            //连击第三段时生成额外的能量波
            if (comboStage == 2 && progress > 0.3f && progress < 0.4f) {
                if (Main.rand.NextBool(3)) {
                    SpawnEnergyWave();
                }
            }

            //添加光照效果
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.3f) * energyIntensity);
        }

        //持续的华丽粒子效果
        private void SpawnContinuousEffects(float progress) {
            Vector2 tipPos = Projectile.Center + Projectile.velocity * 60f;

            //主火焰拖尾
            if (Main.rand.NextBool(2)) {
                Vector2 particlePos = Projectile.Center + Projectile.velocity * Main.rand.NextFloat(20f, 80f);
                Vector2 particleVel = -Projectile.velocity * Main.rand.NextFloat(2f, 6f);

                BasePRT flame = new PRT_Light(particlePos, particleVel, Main.rand.NextFloat(0.8f, 1.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Gold, Color.White)
                    , 15, 0.5f, 1.2f);
                PRTLoader.AddParticle(flame);
            }

            //能量光点环绕
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(30f, 60f);
                Vector2 particlePos = Projectile.Center + offset;

                BasePRT spark = new PRT_Light(particlePos, Vector2.Zero, Main.rand.NextFloat(0.5f, 1f)
                    , Color.Gold, 12, 0.3f, 1f, _entity: Owner, _followingRateRatio: 0.8f);
                PRTLoader.AddParticle(spark);
            }

            //烈焰螺旋
            for (int i = 0; i < 2; i++) {
                float spiralAngle = progress * MathHelper.TwoPi * 3f + i * MathHelper.Pi;
                Vector2 spiralOffset = spiralAngle.ToRotationVector2() * 40f;
                Vector2 spiralPos = Projectile.Center + Projectile.velocity * 40f + spiralOffset;

                int dust = Dust.NewDust(spiralPos, 1, 1, DustID.Torch, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 2f;
            }

            //金色能量流
            if (Main.rand.NextBool(4)) {
                Vector2 energyPos = tipPos + Main.rand.NextVector2Circular(15f, 15f);
                int dust = Dust.NewDust(energyPos, 1, 1, DustID.GoldCoin, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3f, 3f);
                Main.dust[dust].fadeIn = 1.5f;
            }
        }

        //刺击爆发特效
        private void SpawnThrustExplosion(int stage) {
            Vector2 tipPos = Projectile.Center + Projectile.velocity * 70f;

            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonFireOrb".GetSound() with { Volume = 0.7f, Pitch = -0.2f + stage * 0.1f }, tipPos);

            //大范围火焰爆发
            int particleCount = 30 + stage * 15;
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);

                BasePRT explosion = new PRT_Light(tipPos, vel, Main.rand.NextFloat(1.2f, 2f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , 25, 0.6f, 1.5f);
                PRTLoader.AddParticle(explosion);
            }

            //金色冲击波
            for (int i = 0; i < 3; i++) {
                float radius = 40f + i * 20f;
                int segments = 24;
                for (int j = 0; j < segments; j++) {
                    float angle = MathHelper.TwoPi * j / segments;
                    Vector2 shockPos = tipPos + angle.ToRotationVector2() * radius;

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.GoldCoin, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 8f;
                }
            }

            //额外的烈焰旋涡
            for (int i = 0; i < 60; i++) {
                Vector2 randVel = Main.rand.NextVector2Circular(12f, 12f);
                int dust = Dust.NewDust(tipPos, 1, 1, DustID.Torch, randVel.X, randVel.Y, 100, default, Main.rand.NextFloat(2f, 3.5f));
                Main.dust[dust].noGravity = true;
            }

            //在第三段连击时生成超大爆炸
            if (stage == 2) {
                SpawnFinaleExplosion(tipPos);
            }

            //生成火焰弹
            if (Projectile.IsOwnedByLocalPlayer()) {
                int projectileCount = 3 + stage * 2;
                for (int i = 0; i < projectileCount; i++) {
                    float spreadAngle = MathHelper.TwoPi * i / projectileCount;
                    Vector2 fireballVel = (Projectile.velocity.ToRotation() + spreadAngle).ToRotationVector2() * Main.rand.NextFloat(10f, 15f);

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), tipPos, fireballVel
                        , ModContent.ProjectileType<DawnshatterFireball>(), (int)(Projectile.damage * 0.6f), 3f, Projectile.owner);
                }
            }
        }

        //第三段终结爆炸
        private void SpawnFinaleExplosion(Vector2 position) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonRoarShort".GetSound() with { Volume = 0.8f, Pitch = 0.1f }, position);

            //超大范围爆炸粒子
            for (int i = 0; i < 100; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(20f, 20f);

                BasePRT finale = new PRT_Light(position, vel, Main.rand.NextFloat(1.5f, 2.8f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Gold, Color.White)
                    , 35, 0.8f, 1.8f);
                PRTLoader.AddParticle(finale);
            }

            //环形冲击波扩散
            for (int ring = 0; ring < 5; ring++) {
                for (int i = 0; i < 32; i++) {
                    float angle = MathHelper.TwoPi * i / 32f;
                    Vector2 shockPos = position + angle.ToRotationVector2() * (80f + ring * 30f);

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 12f;
                    Main.dust[dust].fadeIn = 2f;
                }
            }
        }

        //能量波
        private void SpawnEnergyWave() {
            Vector2 wavePos = Projectile.Center + Projectile.velocity * Main.rand.NextFloat(30f, 70f);

            BasePRT wave = new PRT_Light(wavePos, Projectile.velocity * 5f, Main.rand.NextFloat(1f, 1.5f)
                , Color.Cyan, 20, 0.5f, 1.3f);
            PRTLoader.AddParticle(wave);

            int dust = Dust.NewDust(wavePos, 1, 1, DustID.Electric, 0, 0, 100, default, 2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Projectile.velocity * 6f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            target.AddBuff(BuffID.Daybreak, 300);

            //强力击退
            if (comboStage == 2) {
                target.velocity += Projectile.velocity * 25f;
            }

            //命中音效
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = 0.2f + comboStage * 0.15f }, target.Center);

            //华丽的命中粒子
            for (int i = 0; i < 20 + comboStage * 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);

                BasePRT hitEffect = new PRT_Light(target.Center, vel, Main.rand.NextFloat(0.8f, 1.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Gold)
                    , 18, 0.5f, 1.2f);
                PRTLoader.AddParticle(hitEffect);
            }

            //连击第三段时造成范围爆炸
            if (comboStage == 2 && Projectile.IsOwnedByLocalPlayer()) {
                SpawnHitExplosion(target.Center);
            }
        }

        //命中爆炸
        private void SpawnHitExplosion(Vector2 position) {
            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);

                BasePRT explosion = new PRT_Light(position, vel, Main.rand.NextFloat(1.2f, 2f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , 30, 0.6f, 1.5f);
                PRTLoader.AddParticle(explosion);
            }

            //生成额外伤害弹幕
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Vector2 vel = angle.ToRotationVector2() * 12f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, vel
                        , ModContent.ProjectileType<DawnshatterFireball>(), Projectile.damage / 2, 4f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float drawRotation = Projectile.rotation + (Owner.direction > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition - Projectile.velocity.UnitVector() * 70;
            Vector2 origin = VaultUtils.GetOrig(texture, 4);
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //多层发光效果
            for (int i = 0; i < 3; i++) {
                float glowScale = 0.75f + i * 0.05f;
                float glowAlpha = (0.4f - i * 0.1f) * energyIntensity;
                Color glowColor = VaultUtils.MultiStepColorLerp(i / 3f, Color.Red, Color.Orange, Color.Yellow) with { A = 0 };
                glowColor *= glowAlpha;

                Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), glowColor
                    , drawRotation, origin, Projectile.scale * glowScale, effects, 0);
            }

            //主体绘制
            Color drawColor = Projectile.GetAlpha(lightColor);
            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), drawColor
                , drawRotation, origin, Projectile.scale * 0.7f, effects, 0);

            //额外的能量光晕
            Color energyColor = new Color(255, 200, 50, 0) * energyIntensity * 0.6f;
            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), energyColor
                , drawRotation, origin, Projectile.scale * 0.78f, effects, 0);

            return false;
        }
    }
}
