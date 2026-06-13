using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 寰宇咏叹调手持 左键蓄力掷盘 右键压扁领域 Q/R见<see cref="AriaofTheCosmos.HoldItem"/>
    internal class AriaofTheCosmosHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";
        public override int TargetID => ModContent.ItemType<AriaofTheCosmos>();
        public override bool CanRightClick => true;

        //蓄力阶段分界
        private const int Stage1 = 60;
        private const int Stage2 = 120;
        private const int MaxChargeTime = 180;
        private const int MinChargeTime = 30;

        //左键蓄力(按键由基类同步 本地累积驱动表现)
        private int chargeTime;
        private float chargeProgress;
        private int accretionDiskIndex = -1;
        private bool isCharging;
        private float particleTimer;
        private Color currentGlowColor;

        //右键蓄力状态
        private int chargeTimeR;
        private float chargeProgressR;
        private int flattenedDiskIndex = -1;
        private bool isChargingR;
        private float particleTimerR;
        private Color currentGlowColorR;

        //蓄力中不自毁 松键下一帧由释放逻辑收尾
        public override bool StayAlive() => isCharging || isChargingR;

        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandFireDistanceX = 25;
            HandFireDistanceY = -8;
            MuzzleForwardOffset = 30;
            GunPressure = 0;
            ControlForce = 0;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            //左键蓄力
            if (WantsFireLeft) {
                isCharging = true;
                chargeTime = Math.Min(chargeTime + 1, MaxChargeTime);
                chargeProgress = chargeTime / (float)MaxChargeTime;
                UpdateChargeEffects();
                UpdateAccretionDisk();
                PlayChargeSound();
            }
            else if (isCharging) {
                ReleaseAttack();
                ResetCharge();
            }

            //右键蓄力
            if (WantsFireRight) {
                isChargingR = true;
                chargeTimeR = Math.Min(chargeTimeR + 1, MaxChargeTime);
                chargeProgressR = chargeTimeR / (float)MaxChargeTime;
                UpdateChargeEffectsR();
                UpdateFlattenedDisk();
                PlayChargeSoundR();
            }
            else if (isChargingR) {
                ReleaseAttackR();
                ResetChargeR();
            }

            Time++;
        }

        #region 左键：吸积盘
        private void UpdateChargeEffects() {
            //根据蓄力阶段改变颜色：黄橙 → 橙红 → 深红紫
            if (chargeTime < Stage1) {
                currentGlowColor = Color.Lerp(Color.Orange, Color.Yellow, chargeProgress * 3f);
            }
            else if (chargeTime < Stage2) {
                currentGlowColor = Color.Lerp(Color.Yellow, Color.OrangeRed, (chargeTime - Stage1) / (float)(Stage2 - Stage1));
            }
            else {
                currentGlowColor = Color.Lerp(Color.OrangeRed, Color.Purple, (chargeTime - Stage2) / (float)(MaxChargeTime - Stage2));
            }

            particleTimer++;
            if (particleTimer >= 5 - chargeProgress * 3) {
                SpawnChargeParticles();
                particleTimer = 0;
            }

            if (chargeTime >= Stage2) {
                Owner.CWR().GetScreenShake(chargeProgress * 2f);
            }
        }

        private void SpawnChargeParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            int particleCount = (int)(1 + chargeProgress * 3);
            for (int i = 0; i < particleCount; i++) {
                Vector2 particlePos = ShootPos + Main.rand.NextVector2Circular(30, 30);
                Vector2 particleVel = (ShootPos - particlePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);

                int dustType = Main.rand.Next(new[] { 6, 259, 158, 234 });
                Dust dust = Dust.NewDustPerfect(particlePos, dustType, particleVel, 100,
                    currentGlowColor * 0.8f, Main.rand.NextFloat(1f, 2f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            //在高蓄力阶段生成额外的能量环
            if (chargeTime >= Stage2 && chargeTime % 10 == 0) {
                SpawnEnergyRing();
            }
        }

        private void SpawnEnergyRing() {
            const int segments = 32;
            float radius = 20 + chargeProgress * 40;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 offset = angle.ToRotationVector2() * radius;
                Dust dust = Dust.NewDustPerfect(ShootPos + offset, DustID.Sandnado, Vector2.Zero, 100,
                    currentGlowColor * 0.6f, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
                dust.velocity = offset.SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        private void UpdateAccretionDisk() {
            //吸积盘仅主人端生成操控 远端靠弹幕同步
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (accretionDiskIndex == -1 || !Main.projectile[accretionDiskIndex].active
                || Main.projectile[accretionDiskIndex].type != ModContent.ProjectileType<AccretionDisk>()) {
                accretionDiskIndex = Projectile.NewProjectile(Source, ShootPos, Vector2.Zero
                    , ModContent.ProjectileType<AccretionDisk>()
                    , WeaponDamage * 2, WeaponKnockback, Owner.whoAmI);
            }

            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Projectile disk = Main.projectile[accretionDiskIndex];
                disk.Center = ShootPos;
                disk.timeLeft = 10;//保持存活

                if (disk.ModProjectile is AccretionDisk accretionDisk) {
                    //根据蓄力进度调整吸积盘的形态参数
                    disk.scale = MathHelper.Lerp(0.3f, 2.5f, chargeProgress);
                    accretionDisk.RotationSpeed = MathHelper.Lerp(0.5f, 3f, chargeProgress);
                    accretionDisk.InnerRadius = MathHelper.Lerp(0.25f, 0.15f, chargeProgress);
                    accretionDisk.OuterRadius = MathHelper.Lerp(0.7f, 0.9f, chargeProgress);
                    disk.alpha = 0;
                }
            }
        }

        private void PlayChargeSound() {
            if (chargeTime == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }
            else if (chargeTime == Stage1) {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            }
            else if (chargeTime == Stage2) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 0.8f, Pitch = 0f }, Projectile.Center);
            }
        }

        private void ReleaseAttack() {
            //蓄力不足，不发射
            if (chargeTime < MinChargeTime) {
                return;
            }

            PlayReleaseSound();
            SpawnReleaseEffect();
            Owner.CWR().GetScreenShake(5f + chargeProgress * 10f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //将吸积盘转换为掷出的攻击弹幕
            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Projectile disk = Main.projectile[accretionDiskIndex];
                if (disk.ModProjectile is AccretionDisk) {
                    float damageMultiplier = MathHelper.Lerp(1f, 3.5f, chargeProgress);
                    disk.damage = (int)(WeaponDamage * damageMultiplier);
                    disk.knockBack = WeaponKnockback * (1f + chargeProgress);
                    disk.friendly = true;
                    disk.timeLeft = (int)(120 + chargeProgress * 180);//2-5秒
                    disk.velocity = (InMousePos - disk.Center).SafeNormalize(Vector2.Zero) * (8f + chargeProgress * 12f);
                    disk.tileCollide = false;
                    disk.alpha = 50;
                    disk.netUpdate = true;
                }
            }

            //掷出的反作用力与魔力支付
            Owner.velocity -= ShootVelocity.SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
            Owner.statMana = Math.Max(Owner.statMana - (int)(Item.mana * (1f + chargeProgress)), 0);
            HoldManaRegenDelay();
        }

        private void PlayReleaseSound() {
            float volume = 0.8f + chargeProgress * 0.4f;
            float pitch = -0.3f + chargeProgress * 0.5f;

            SoundEngine.PlaySound(SoundID.Item109 with { Volume = volume, Pitch = pitch }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = volume * 0.6f, Pitch = pitch }, Projectile.Center);

            if (chargeProgress >= 0.66f) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void SpawnReleaseEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            int particleCount = (int)(30 + chargeProgress * 70);

            //内爆收缩粒子
            for (int i = 0; i < 15 + particleCount; i++) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(90f, 90f);
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(10f, 18f);

                PRTLoader.NewParticle<PRT_Spark>(spawnPos, velocity, Color.White, Main.rand.NextFloat(1f, 1.8f)).Configure(false, Main.rand.Next(20, 30), Owner);
            }
        }

        private void ResetCharge() {
            chargeTime = 0;
            chargeProgress = 0;
            particleTimer = 0;
            isCharging = false;

            //清理未掷出吸积盘(已掷出 timeLeft 已重设)
            if (Projectile.IsOwnedByLocalPlayer()
                && accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active
                && Main.projectile[accretionDiskIndex].type == ModContent.ProjectileType<AccretionDisk>()
                && !Main.projectile[accretionDiskIndex].friendly) {
                Main.projectile[accretionDiskIndex].Kill();
            }
            accretionDiskIndex = -1;
        }
        #endregion

        #region 右键：压扁吸积盘
        private void UpdateChargeEffectsR() {
            //右键使用蓝色系：青 → 深蓝 → 紫
            if (chargeTimeR < Stage1) {
                currentGlowColorR = Color.Lerp(Color.Cyan, Color.DeepSkyBlue, chargeProgressR * 3f);
            }
            else if (chargeTimeR < Stage2) {
                currentGlowColorR = Color.Lerp(Color.DeepSkyBlue, Color.Blue, (chargeTimeR - Stage1) / (float)(Stage2 - Stage1));
            }
            else {
                currentGlowColorR = Color.Lerp(Color.Blue, Color.Purple, (chargeTimeR - Stage2) / (float)(MaxChargeTime - Stage2));
            }

            particleTimerR++;
            if (particleTimerR >= 5 - chargeProgressR * 3) {
                SpawnChargeParticlesR();
                particleTimerR = 0;
            }

            if (chargeTimeR >= Stage2) {
                Owner.CWR().GetScreenShake(chargeProgressR * 1.5f);
            }
        }

        private void SpawnChargeParticlesR() {
            if (VaultUtils.isServer) {
                return;
            }

            int particleCount = (int)(1 + chargeProgressR * 3);
            for (int i = 0; i < particleCount; i++) {
                Vector2 particlePos = ShootPos + Main.rand.NextVector2Circular(30, 30);
                Vector2 particleVel = (ShootPos - particlePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);

                PRTLoader.NewParticle<PRT_AccretionDiskImpact>(particlePos, particleVel, currentGlowColorR * 0.9f, Main.rand.NextFloat(0.4f, 0.8f)).Configure(Main.rand.Next(15, 25), Main.rand.NextFloat(-0.15f, 0.15f), false, Main.rand.NextFloat(0.15f, 0.25f));
            }

            if (chargeTimeR >= Stage2 && chargeTimeR % 10 == 0) {
                SpawnEnergyRingR();
            }
        }

        private void SpawnEnergyRingR() {
            const int segments = 32;
            float radius = 20 + chargeProgressR * 40;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 2f;

                PRTLoader.NewParticle<PRT_AccretionDiskImpact>(ShootPos + offset, particleVel, currentGlowColorR * 0.7f, Main.rand.NextFloat(0.5f, 0.9f)).Configure(Main.rand.Next(20, 30), Main.rand.NextFloat(-0.2f, 0.2f), false, Main.rand.NextFloat(0.18f, 0.28f));
            }
        }

        private void UpdateFlattenedDisk() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (flattenedDiskIndex == -1 || !Main.projectile[flattenedDiskIndex].active
                || Main.projectile[flattenedDiskIndex].type != ModContent.ProjectileType<FlattenedAccretionDisk>()) {
                flattenedDiskIndex = Projectile.NewProjectile(Source, ShootPos, Vector2.Zero
                    , ModContent.ProjectileType<FlattenedAccretionDisk>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }

            if (flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active) {
                Projectile disk = Main.projectile[flattenedDiskIndex];
                disk.Center = ShootPos;
                disk.timeLeft = 10;
                disk.rotation = ToMouseA;

                if (disk.ModProjectile is FlattenedAccretionDisk flattenedDisk) {
                    disk.scale = MathHelper.Lerp(0.3f, 2.0f, chargeProgressR);
                    flattenedDisk.RotationSpeed = MathHelper.Lerp(0.8f, 2.5f, chargeProgressR);
                    flattenedDisk.FlattenAngle = MathHelper.Lerp(0.8f, 0.5f, chargeProgressR);
                    flattenedDisk.ChargeProgress = chargeProgressR;
                    disk.alpha = 0;
                }
            }
        }

        private void PlayChargeSoundR() {
            if (chargeTimeR == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
            }
            else if (chargeTimeR == Stage1) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
            }
            else if (chargeTimeR == Stage2) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            }
        }

        private void ReleaseAttackR() {
            if (chargeTimeR < MinChargeTime) {
                return;
            }

            PlayReleaseSoundR();
            SpawnReleaseEffectR();
            Owner.CWR().GetScreenShake(3f + chargeProgressR * 8f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Owner.statMana = Math.Max(Owner.statMana - (int)(Item.mana * 0.8f * (1f + chargeProgressR * 0.5f)), 0);
            HoldManaRegenDelay();
        }

        private void PlayReleaseSoundR() {
            float volume = 0.7f + chargeProgressR * 0.3f;
            float pitch = 0.1f + chargeProgressR * 0.4f;

            SoundEngine.PlaySound(SoundID.Item84 with { Volume = volume, Pitch = pitch }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Volume = volume * 0.5f, Pitch = pitch }, Projectile.Center);

            if (chargeProgressR >= 0.66f) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void SpawnReleaseEffectR() {
            if (VaultUtils.isServer) {
                return;
            }

            int particleCount = (int)(25 + chargeProgressR * 50);

            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 12f + chargeProgressR * 8f);
                velocity.Y *= 0.6f;//保持压扁视觉

                PRTLoader.NewParticle<PRT_AccretionDiskImpact>(ShootPos, velocity, currentGlowColorR, Main.rand.NextFloat(0.6f, 1.2f)).Configure(Main.rand.Next(25, 40), Main.rand.NextFloat(-0.3f, 0.3f), true, Main.rand.NextFloat(0.2f, 0.35f));
            }

            //生成扁平冲击波
            for (int i = 0; i < 2; i++) {
                const int segments = 48;
                float radius = 30f + i * 40f + chargeProgressR * 40f;

                for (int j = 0; j < segments; j++) {
                    float angle = MathHelper.TwoPi * j / segments;
                    Vector2 offset = angle.ToRotationVector2() * radius;
                    offset.Y *= 0.6f;
                    Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 2.5f;

                    PRTLoader.NewParticle<PRT_AccretionDiskImpact>(ShootPos + offset, particleVel, currentGlowColorR * 0.6f, Main.rand.NextFloat(0.7f, 1.3f)).Configure(Main.rand.Next(30, 45), Main.rand.NextFloat(-0.25f, 0.25f), false, Main.rand.NextFloat(0.22f, 0.32f));
                }
            }
        }

        private void ResetChargeR() {
            chargeTimeR = 0;
            chargeProgressR = 0;
            particleTimerR = 0;
            isChargingR = false;

            //压扁吸积盘仅蓄力期存在 结束即清理
            if (Projectile.IsOwnedByLocalPlayer()
                && flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active
                && Main.projectile[flattenedDiskIndex].type == ModContent.ProjectileType<FlattenedAccretionDisk>()) {
                Main.projectile[flattenedDiskIndex].Kill();
            }
            flattenedDiskIndex = -1;
        }
        #endregion

        public override void OnKill(int timeLeft) {
            ResetCharge();
            ResetChargeR();
        }
    }
}
