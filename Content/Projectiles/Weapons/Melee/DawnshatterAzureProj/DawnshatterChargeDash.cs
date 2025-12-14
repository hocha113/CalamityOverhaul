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
    //破晓长枪的毁灭性蓄力突进，终末武器的终极攻击
    internal class DawnshatterChargeDash : BaseHeldProj
    {
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";

        private enum DashPhase
        {
            Charging,
            Dashing,
            Exploding,
            Recovery
        }

        private DashPhase currentPhase;
        private Vector2 dashDirection;
        private float chargeProgress;
        private int phaseTimer;
        private bool hasExploded;
        private float screenShakeIntensity;
        private int hitEnemyCount;

        private const int ChargeDuration = 35;
        private const int DashDuration = 30;
        private const int ExplodeDuration = 20;
        private const int RecoveryDuration = 15;
        private const float MaxDashSpeed = 45f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 160;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = ChargeDuration + DashDuration + ExplodeDuration + RecoveryDuration;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 3, 3);
            SetHeld();
            phaseTimer++;

            //阶段切换
            if (currentPhase == DashPhase.Charging && phaseTimer >= ChargeDuration) {
                EnterDashPhase();
            }
            else if (currentPhase == DashPhase.Dashing && phaseTimer >= DashDuration) {
                EnterExplodePhase();
            }
            else if (currentPhase == DashPhase.Exploding && phaseTimer >= ExplodeDuration) {
                EnterRecoveryPhase();
            }

            //执行对应阶段逻辑
            switch (currentPhase) {
                case DashPhase.Charging:
                    UpdateCharging();
                    break;
                case DashPhase.Dashing:
                    UpdateDashing();
                    break;
                case DashPhase.Exploding:
                    UpdateExploding();
                    break;
                case DashPhase.Recovery:
                    UpdateRecovery();
                    break;
            }

            Projectile.rotation = dashDirection.ToRotation();
            SetDirection();

            //屏幕震动
            if (screenShakeIntensity > 0) {
                Owner.CWR().ScreenShakeValue = screenShakeIntensity;
                screenShakeIntensity *= 0.9f;
            }

            //超强光照
            float lightIntensity = currentPhase == DashPhase.Dashing ? 2f : chargeProgress * 1.5f;
            Lighting.AddLight(Projectile.Center, new Vector3(1.5f, 1.2f, 0.5f) * lightIntensity);
        }

        //蓄力阶段，积蓄毁灭性的能量
        private void UpdateCharging() {
            chargeProgress = CWRUtils.EaseOutCubic(phaseTimer / (float)ChargeDuration);
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);

            //长枪在玩家前方蓄力
            float chargeDistance = MathHelper.Lerp(50f, 80f, chargeProgress);
            Projectile.Center = Owner.MountedCenter + dashDirection * chargeDistance;

            //强制减速玩家
            Owner.velocity *= 0.7f;

            //能量环绕效果
            SpawnChargeRings();

            //能量粒子向长枪汇聚
            if (Main.rand.NextBool()) {
                SpawnConvergingEnergy();
            }

            //蓄力音效循环
            if (phaseTimer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with { Volume = 0.4f * chargeProgress, Pitch = chargeProgress * 0.5f }, Projectile.Center);
            }

            //蓄力完成前的预警
            if (phaseTimer == ChargeDuration - 5) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonRoarShort".GetSound() with { Volume = 0.8f, Pitch = -0.3f }, Owner.Center);
                SpawnChargeCompleteEffect();
            }
        }

        //进入突进阶段，爆发性冲刺
        private void EnterDashPhase() {
            currentPhase = DashPhase.Dashing;
            phaseTimer = 0;
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);

            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.2f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.8f, Pitch = 0.3f }, Owner.Center);

            //启动爆发
            SpawnDashStartExplosion();
            screenShakeIntensity = 15f;
        }

        //毁灭性突进
        private void UpdateDashing() {
            float dashProgress = phaseTimer / (float)DashDuration;

            //先加速后减速的曲线
            float speedCurve;
            if (dashProgress < 0.3f) {
                speedCurve = CWRUtils.EaseOutCubic(dashProgress / 0.3f);
            }
            else {
                speedCurve = 1f - CWRUtils.EaseInQuad((dashProgress - 0.3f) / 0.7f) * 0.4f;
            }

            float dashSpeed = MaxDashSpeed * speedCurve;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity = dashDirection * dashSpeed;
                Owner.GivePlayerImmuneState(3, false);
            }

            //长枪保持在前方
            Projectile.Center = Owner.MountedCenter + dashDirection * 120f;

            //华丽的拖尾效果
            SpawnDashTrail(dashProgress);

            //每隔一段时间播放冲击音效
            if (phaseTimer % 5 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            //持续的小型爆炸
            if (phaseTimer % 3 == 0) {
                SpawnDashMiniExplosion();
            }
        }

        //进入爆炸阶段
        private void EnterExplodePhase() {
            currentPhase = DashPhase.Exploding;
            phaseTimer = 0;

            //停止移动
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.2f;
            }

            //终极爆炸
            SpawnUltimateExplosion();
            screenShakeIntensity = 25f;

            //音效组合
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonFireOrb".GetSound() with { Volume = 1f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
        }

        //爆炸阶段
        private void UpdateExploding() {
            float explodeProgress = phaseTimer / (float)ExplodeDuration;

            //长枪在爆炸中心旋转
            Projectile.rotation += 0.5f * (1f - explodeProgress);
            Projectile.Center = Owner.MountedCenter + dashDirection * MathHelper.Lerp(120f, 80f, explodeProgress);

            //持续的爆炸效果
            if (phaseTimer % 2 == 0) {
                SpawnContinuousExplosion(explodeProgress);
            }

            //减速玩家
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.85f;
            }
        }

        //进入恢复阶段
        private void EnterRecoveryPhase() {
            currentPhase = DashPhase.Recovery;
            phaseTimer = 0;
        }

        //恢复阶段
        private void UpdateRecovery() {
            float recoveryProgress = phaseTimer / (float)RecoveryDuration;
            float pullbackDistance = MathHelper.Lerp(80f, 45f, CWRUtils.EaseInQuad(recoveryProgress));
            Projectile.Center = Owner.MountedCenter + dashDirection * pullbackDistance;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.88f;
            }
        }

        //蓄力能量环
        private void SpawnChargeRings() {
            if (phaseTimer % 3 != 0) return;

            float ringRadius = 60f + chargeProgress * 80f;
            int segments = 16;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments + phaseTimer * 0.1f;
                Vector2 ringPos = Projectile.Center + angle.ToRotationVector2() * ringRadius;

                BasePRT ring = new PRT_Light(ringPos, Vector2.Zero, Main.rand.NextFloat(0.8f, 1.2f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Gold, Color.Orange, Color.Red)
                    , 15, 0.4f, 1f, _entity: Owner, _followingRateRatio: 1f);
                PRTLoader.AddParticle(ring);
            }
        }

        //能量汇聚
        private void SpawnConvergingEnergy() {
            Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(200f * (1f - chargeProgress), 200f * (1f - chargeProgress));
            Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * 8f * chargeProgress;

            BasePRT energy = new PRT_Light(spawnPos, velocity, Main.rand.NextFloat(1f, 1.8f)
                , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Yellow, Color.Orange, Color.Red)
                , 25, 0.5f, 1.3f);
            PRTLoader.AddParticle(energy);

            int dust = Dust.NewDust(spawnPos, 1, 1, DustID.GoldCoin, velocity.X, velocity.Y, 100, default, 2f);
            Main.dust[dust].noGravity = true;
        }

        //蓄力完成特效
        private void SpawnChargeCompleteEffect() {
            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);

                BasePRT complete = new PRT_Light(Projectile.Center, vel, Main.rand.NextFloat(1.5f, 2.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow, Color.White)
                    , 30, 0.7f, 1.8f);
                PRTLoader.AddParticle(complete);
            }

            //冲击波
            for (int ring = 0; ring < 3; ring++) {
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f;
                    Vector2 shockPos = Projectile.Center + angle.ToRotationVector2() * (100f + ring * 40f);

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 3.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 15f;
                }
            }
        }

        //突进开始爆炸
        private void SpawnDashStartExplosion() {
            for (int i = 0; i < 120; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(25f, 25f);

                BasePRT explosion = new PRT_Light(Owner.Center, vel, Main.rand.NextFloat(2f, 3.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow)
                    , 40, 0.8f, 2f);
                PRTLoader.AddParticle(explosion);
            }

            //大范围火焰爆发
            for (int i = 0; i < 200; i++) {
                int dust = Dust.NewDust(Owner.Center, 1, 1, DustID.Torch, 0, 0, 100, default, Main.rand.NextFloat(3f, 5f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(20f, 20f);
                Main.dust[dust].noGravity = true;
            }
        }

        //突进拖尾
        private void SpawnDashTrail(float dashProgress) {
            //烈焰拖尾
            for (int i = 0; i < 6; i++) {
                Vector2 trailPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f) - dashDirection * i * 30f;
                Vector2 trailVel = -dashDirection * Main.rand.NextFloat(5f, 15f);

                BasePRT trail = new PRT_Light(trailPos, trailVel, Main.rand.NextFloat(2f, 3.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , 20, 0.6f, 1.8f);
                PRTLoader.AddParticle(trail);
            }

            //金色能量流
            if (Main.rand.NextBool(2)) {
                Vector2 energyPos = Projectile.Center + Main.rand.NextVector2Circular(50f, 50f);
                int dust = Dust.NewDust(energyPos, 1, 1, DustID.GoldCoin, -dashDirection.X * 10f, -dashDirection.Y * 10f, 100, default, 3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 2f;
            }

            //螺旋火焰
            float spiralAngle = phaseTimer * 0.4f;
            for (int i = 0; i < 3; i++) {
                float angle = spiralAngle + i * MathHelper.TwoPi / 3f;
                Vector2 spiralPos = Projectile.Center + angle.ToRotationVector2() * 60f;

                int dust = Dust.NewDust(spiralPos, 1, 1, DustID.Torch, -dashDirection.X * 8f, -dashDirection.Y * 8f, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        //突进小型爆炸
        private void SpawnDashMiniExplosion() {
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);

                BasePRT mini = new PRT_Light(Projectile.Center, vel, Main.rand.NextFloat(1.2f, 2f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Orange, Color.Yellow)
                    , 15, 0.5f, 1.5f);
                PRTLoader.AddParticle(mini);
            }

            //生成额外火焰弹
            if (Projectile.IsOwnedByLocalPlayer() && Main.rand.NextBool(3)) {
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.PiOver2 * i;
                    Vector2 vel = (dashDirection.ToRotation() + angle).ToRotationVector2() * 12f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , ModContent.ProjectileType<DawnshatterFireball>(), (int)(Projectile.damage * 0.8f), 5f, Projectile.owner);
                }
            }
        }

        //终极爆炸
        private void SpawnUltimateExplosion() {
            //超大范围粒子爆发
            for (int i = 0; i < 300; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(35f, 35f);

                BasePRT ultimate = new PRT_Light(Projectile.Center, vel, Main.rand.NextFloat(2.5f, 4.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow, Color.White)
                    , 60, 0.9f, 2.5f);
                PRTLoader.AddParticle(ultimate);
            }

            //多层冲击波
            for (int ring = 0; ring < 8; ring++) {
                int segments = 36;
                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments;
                    Vector2 shockPos = Projectile.Center + angle.ToRotationVector2() * (120f + ring * 60f);

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 4f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 20f;
                    Main.dust[dust].fadeIn = 3f;
                }
            }

            //超大范围尘埃
            for (int i = 0; i < 400; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.FireworkFountain_Red;
                int dust = Dust.NewDust(Projectile.Center, 1, 1, dustType, 0, 0, 100, default, Main.rand.NextFloat(4f, 7f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(30f, 30f);
                Main.dust[dust].noGravity = true;
            }

            //生成大量火焰弹
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 36; i++) {
                    float angle = MathHelper.TwoPi * i / 36f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 25f);

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , ModContent.ProjectileType<DawnshatterFireball>(), Projectile.damage, 6f, Projectile.owner);
                }
            }
        }

        //持续爆炸
        private void SpawnContinuousExplosion(float progress) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(20f, 20f) * (1f - progress);

                BasePRT continuous = new PRT_Light(Projectile.Center, vel, Main.rand.NextFloat(1.8f, 3f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , 25, 0.6f, 1.8f);
                PRTLoader.AddParticle(continuous);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hitEnemyCount++;

            //强力debuff
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Daybreak, 480);
            target.AddBuff(BuffID.Ichor, 360);

            //突进阶段的超强击退
            if (currentPhase == DashPhase.Dashing) {
                target.velocity += dashDirection * 35f;

                //每命中5个敌人生成爆炸
                if (hitEnemyCount % 5 == 0) {
                    SpawnHitExplosion(target.Center);
                }
            }

            //命中粒子
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);

                BasePRT hit2 = new PRT_Light(target.Center, vel, Main.rand.NextFloat(1.5f, 2.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , 20, 0.6f, 1.5f);
                PRTLoader.AddParticle(hit2);
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = 0.1f }, target.Center);
        }

        //命中爆炸
        private void SpawnHitExplosion(Vector2 position) {
            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(18f, 18f);

                BasePRT explosion = new PRT_Light(position, vel, Main.rand.NextFloat(1.8f, 3f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow)
                    , 35, 0.7f, 2f);
                PRTLoader.AddParticle(explosion);
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12f;
                    Vector2 velocity = angle.ToRotationVector2() * 15f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, velocity
                        , ModContent.ProjectileType<DawnshatterFireball>(), (int)(Projectile.damage * 0.7f), 5f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float drawRotation = Projectile.rotation + (Owner.direction > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = VaultUtils.GetOrig(texture, 4);
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //根据阶段计算发光强度
            float glowIntensity = 0f;
            if (currentPhase == DashPhase.Charging) {
                glowIntensity = chargeProgress * 1.5f;
            }
            else if (currentPhase == DashPhase.Dashing || currentPhase == DashPhase.Exploding) {
                glowIntensity = 2f;
            }
            else {
                glowIntensity = 1f - phaseTimer / (float)RecoveryDuration;
            }

            //多层超强发光
            for (int i = 0; i < 5; i++) {
                float layerScale = 0.75f + i * 0.08f;
                float layerAlpha = (0.5f - i * 0.08f) * glowIntensity;
                Color glowColor = VaultUtils.MultiStepColorLerp(i / 5f, Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow, Color.White) with { A = 0 };
                glowColor *= layerAlpha;

                Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), glowColor
                    , drawRotation, origin, Projectile.scale * layerScale, effects, 0);
            }

            //主体
            Color drawColor = Projectile.GetAlpha(lightColor);
            if (currentPhase == DashPhase.Dashing) {
                drawColor = Color.Lerp(drawColor, Color.White, 0.5f);
            }

            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), drawColor
                , drawRotation, origin, Projectile.scale * 0.7f, effects, 0);

            //额外能量光晕
            if (glowIntensity > 0.5f) {
                Color energyColor = new Color(255, 180, 60, 0) * glowIntensity * 0.8f;
                Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), energyColor
                    , drawRotation, origin, Projectile.scale * 0.85f, effects, 0);
            }

            return false;
        }
    }
}
