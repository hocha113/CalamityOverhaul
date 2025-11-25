using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 寰宇咏叹调手持弹幕
    /// </summary>
    internal class AriaofTheCosmosHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";
        public override int TargetID => ModContent.ItemType<AriaofTheCosmos>();

        //左键相关
        private int chargeTime;
        private int maxChargeTime = 180;
        private int minChargeTime = 30;
        private float chargeProgress;
        private int accretionDiskIndex = -1;
        private bool isCharging;
        private bool hasReleasedAttack;

        //右键相关
        private int chargeTimeR;
        private int maxChargeTimeR = 180;
        private int minChargeTimeR = 30;
        private float chargeProgressR;
        private int flattenedDiskIndex = -1;
        private bool isChargingR;
        private bool hasReleasedAttackR;

        //蓄力阶段
        private const int Stage1 = 60;
        private const int Stage2 = 120;
        private const int Stage3 = 180;

        //视觉效果参数
        private float particleTimer;
        private Color currentGlowColor;

        //右键视觉效果参数
        private float particleTimerR;
        private Color currentGlowColorR;

        //技能相关
        private int qSkillCooldown;
        private int rSkillCooldown;
        private const int QSkillMaxCooldown = 120;  // 2秒冷却
        private const int RSkillMaxCooldown = 180;  // 3秒冷却

        public override void SetMagicProperty() {
            Recoil = 0;
            HandFireDistanceX = 25;
            HandFireDistanceY = -8;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 30;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            ControlForce = 0;
            GunPressure = 0;
            EnableRecoilRetroEffect = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            CanRightClick = true;
        }

        public override void PostInOwner() {
            //技能冷却更新
            if (qSkillCooldown > 0) {
                qSkillCooldown--;
            }
            if (rSkillCooldown > 0) {
                rSkillCooldown--;
            }

            //Q技能检测 - 星环守护
            if (CWRKeySystem.AriaofTheCosmos_Q.JustPressed && qSkillCooldown <= 0) {
                if (Owner.CountProjectilesOfID<AriaQSkill>() == 0) {
                    int qSkillIndex = Projectile.NewProjectile(
                        Source,
                        Owner.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<AriaQSkill>(),
                        WeaponDamage,
                        WeaponKnockback,
                        Owner.whoAmI
                    );

                    if (qSkillIndex >= 0) {
                        qSkillCooldown = QSkillMaxCooldown;

                        // 消耗魔力
                        int manaCost = Item.mana * 2;
                        Owner.statMana -= manaCost;
                        if (Owner.statMana < 0) {
                            Owner.statMana = 0;
                        }

                        // 播放激活音效
                        SoundEngine.PlaySound(SoundID.Item109 with {
                            Volume = 0.8f,
                            Pitch = 0.3f
                        }, Projectile.Center);
                    }
                }
            }

            //R技能检测 - 伽马暴击
            if (CWRKeySystem.AriaofTheCosmos_R.JustPressed && rSkillCooldown <= 0) {
                if (Owner.CountProjectilesOfID<AriaRSkill>() == 0) {
                    int rSkillIndex = Projectile.NewProjectile(
                        Source,
                        Owner.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<AriaRSkill>(),
                        (int)(WeaponDamage * 1.5f),
                        WeaponKnockback * 1.5f,
                        Owner.whoAmI
                    );

                    if (rSkillIndex >= 0) {
                        rSkillCooldown = RSkillMaxCooldown;

                        // 消耗魔力
                        int manaCost = Item.mana * 3;
                        Owner.statMana -= manaCost;
                        if (Owner.statMana < 0) {
                            Owner.statMana = 0;
                        }

                        // 播放激活音效
                        SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with {
                            Volume = 0.9f,
                            Pitch = -0.3f
                        }, Projectile.Center);
                    }
                }
            }

            //左键逻辑
            if (onFire && !hasReleasedAttack) {
                isCharging = true;
                chargeTime++;

                if (chargeTime > maxChargeTime) {
                    chargeTime = maxChargeTime;
                }

                chargeProgress = MathHelper.Clamp(chargeTime / (float)maxChargeTime, 0f, 1f);
                UpdateChargeEffects();
                UpdateAccretionDisk();
                PlayChargeSound();
            }
            else if (!onFire && isCharging) {
                ReleaseAttack();
                hasReleasedAttack = true;
                isCharging = false;
            }
            else if (!onFire && !onFireR) {
                ResetCharge();
            }

            //右键逻辑
            if (onFireR && !hasReleasedAttackR) {
                isChargingR = true;
                chargeTimeR++;

                if (chargeTimeR > maxChargeTimeR) {
                    chargeTimeR = maxChargeTimeR;
                }

                chargeProgressR = MathHelper.Clamp(chargeTimeR / (float)maxChargeTimeR, 0f, 1f);
                UpdateChargeEffectsR();
                UpdateFlattenedDisk();
                PlayChargeSoundR();
            }
            else if (!onFireR && isChargingR) {
                ReleaseAttackR();
                hasReleasedAttackR = true;
                isChargingR = false;
            }
            else if (!onFireR && !onFire) {
                ResetChargeR();
            }
        }

        private void UpdateChargeEffects() {
            //根据蓄力阶段改变颜色
            if (chargeTime < Stage1) {
                //第一阶段 - 黄橙色
                currentGlowColor = Color.Lerp(Color.Orange, Color.Yellow, chargeProgress * 3f);
            }
            else if (chargeTime < Stage2) {
                //第二阶段 - 橙红色
                float stage2Progress = (chargeTime - Stage1) / (float)(Stage2 - Stage1);
                currentGlowColor = Color.Lerp(Color.Yellow, Color.OrangeRed, stage2Progress);
            }
            else {
                //第三阶段 - 深红紫色
                float stage3Progress = (chargeTime - Stage2) / (float)(Stage3 - Stage2);
                currentGlowColor = Color.Lerp(Color.OrangeRed, Color.Purple, stage3Progress);
            }

            //生成蓄力粒子
            particleTimer++;
            if (particleTimer >= (5 - chargeProgress * 3)) {
                SpawnChargeParticles();
                particleTimer = 0;
            }

            //屏幕效果
            if (chargeTime >= Stage2) {
                Owner.GetModPlayer<CWRPlayer>().GetScreenShake(chargeProgress * 2f);
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
            int segments = 32;
            float radius = 20 + chargeProgress * 40;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 particlePos = ShootPos + offset;
                Vector2 particleVel = Vector2.Zero;

                Dust dust = Dust.NewDustPerfect(particlePos, DustID.Sandnado, particleVel, 100,
                    currentGlowColor * 0.6f, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
                dust.velocity = offset.SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        private void UpdateAccretionDisk() {
            //如果吸积盘还没生成或已经死亡，创建新的
            if (accretionDiskIndex == -1 || !Main.projectile[accretionDiskIndex].active
                || Main.projectile[accretionDiskIndex].type != ModContent.ProjectileType<AccretionDisk>()) {

                accretionDiskIndex = Projectile.NewProjectile(
                    Source,
                    ShootPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<AccretionDisk>(),
                    Owner.GetShootState().WeaponDamage * 2,
                    Owner.GetShootState().WeaponKnockback,
                    Owner.whoAmI
                );
            }

            //更新吸积盘位置和参数
            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Projectile disk = Main.projectile[accretionDiskIndex];
                disk.Center = ShootPos;
                disk.timeLeft = 10; //保持存活

                if (disk.ModProjectile is AccretionDisk accretionDisk) {
                    //根据蓄力进度调整参数
                    float sizeScale = MathHelper.Lerp(0.3f, 2.5f, chargeProgress);
                    disk.scale = sizeScale;

                    //调整旋转速度
                    accretionDisk.RotationSpeed = MathHelper.Lerp(0.5f, 3f, chargeProgress);

                    //调整半径
                    accretionDisk.InnerRadius = MathHelper.Lerp(0.25f, 0.15f, chargeProgress);
                    accretionDisk.OuterRadius = MathHelper.Lerp(0.7f, 0.9f, chargeProgress);

                    //让吸积盘在蓄力时不透明
                    disk.alpha = 0;
                }
            }
        }

        private void PlayChargeSound() {
            //在特定阶段播放音效
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
            if (chargeTime < minChargeTime) {
                //蓄力不足，不发射
                ResetCharge();
                return;
            }

            //计算伤害倍率
            float damageMultiplier = MathHelper.Lerp(1f, 3.5f, chargeProgress);
            int finalDamage = (int)(WeaponDamage * damageMultiplier);

            //将吸积盘转换为攻击弹幕
            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Projectile disk = Main.projectile[accretionDiskIndex];

                if (disk.ModProjectile is AccretionDisk accretionDisk) {
                    //设置攻击参数
                    disk.damage = finalDamage;
                    disk.knockBack = WeaponKnockback * (1f + chargeProgress);
                    disk.friendly = true;

                    //设置生命时间
                    disk.timeLeft = (int)(120 + chargeProgress * 180); //2-5秒

                    //给予初始速度，朝向鼠标
                    Vector2 velocity = (InMousePos - disk.Center).SafeNormalize(Vector2.Zero) * (8f + chargeProgress * 12f);
                    disk.velocity = velocity;

                    //启用碰撞
                    disk.tileCollide = false;

                    //让吸积盘慢慢消失
                    disk.alpha = 50;
                }
            }

            //播放释放音效
            PlayReleaseSound();

            //生成释放特效
            SpawnReleaseEffect();

            //后坐力
            Owner.velocity -= ShootVelocity.SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);

            //屏幕震动
            Owner.GetModPlayer<CWRPlayer>().GetScreenShake(5f + chargeProgress * 10f);

            //消耗魔力
            int manaCost = (int)(Item.mana * (1f + chargeProgress));
            Owner.statMana -= manaCost;
            if (Owner.statMana < 0) {
                Owner.statMana = 0;
            }

            //重置状态
            chargeTime = 0;
            accretionDiskIndex = -1;
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

                BasePRT implosion = new PRT_Spark(
                    spawnPos,
                    velocity,
                    false,
                    Main.rand.Next(20, 30),
                    Main.rand.NextFloat(1f, 1.8f),
                    Color.White,
                    Owner
                );
                PRTLoader.AddParticle(implosion);
            }
        }

        private void ResetCharge() {
            chargeTime = 0;
            chargeProgress = 0;
            particleTimer = 0;
            hasReleasedAttack = false;

            //清理吸积盘
            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Main.projectile[accretionDiskIndex].Kill();
            }
            accretionDiskIndex = -1;
        }

        //右键蓄力相关方法
        private void UpdateChargeEffectsR() {
            //右键使用蓝色系
            if (chargeTimeR < Stage1) {
                currentGlowColorR = Color.Lerp(Color.Cyan, Color.DeepSkyBlue, chargeProgressR * 3f);
            }
            else if (chargeTimeR < Stage2) {
                float stage2Progress = (chargeTimeR - Stage1) / (float)(Stage2 - Stage1);
                currentGlowColorR = Color.Lerp(Color.DeepSkyBlue, Color.Blue, stage2Progress);
            }
            else {
                float stage3Progress = (chargeTimeR - Stage2) / (float)(Stage3 - Stage2);
                currentGlowColorR = Color.Lerp(Color.Blue, Color.Purple, stage3Progress);
            }

            particleTimerR++;
            if (particleTimerR >= (5 - chargeProgressR * 3)) {
                SpawnChargeParticlesR();
                particleTimerR = 0;
            }

            if (chargeTimeR >= Stage2) {
                Owner.GetModPlayer<CWRPlayer>().GetScreenShake(chargeProgressR * 1.5f);
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

                //使用高级粒子系统 - 蓝色系
                BasePRT particle = new PRT_AccretionDiskImpact(
                    particlePos,
                    particleVel,
                    currentGlowColorR * 0.9f,
                    Main.rand.NextFloat(0.4f, 0.8f),
                    Main.rand.Next(15, 25),
                    Main.rand.NextFloat(-0.15f, 0.15f),
                    false,
                    Main.rand.NextFloat(0.15f, 0.25f)
                );
                PRTLoader.AddParticle(particle);
            }

            if (chargeTimeR >= Stage2 && chargeTimeR % 10 == 0) {
                SpawnEnergyRingR();
            }
        }

        private void SpawnEnergyRingR() {
            int segments = 32;
            float radius = 20 + chargeProgressR * 40;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 particlePos = ShootPos + offset;
                Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 2f;

                //使用高级粒子创建能量环 - 蓝色系
                BasePRT particle = new PRT_AccretionDiskImpact(
                    particlePos,
                    particleVel,
                    currentGlowColorR * 0.7f,
                    Main.rand.NextFloat(0.5f, 0.9f),
                    Main.rand.Next(20, 30),
                    Main.rand.NextFloat(-0.2f, 0.2f),
                    false,
                    Main.rand.NextFloat(0.18f, 0.28f)
                );
                PRTLoader.AddParticle(particle);
            }
        }

        private void UpdateFlattenedDisk() {
            if (flattenedDiskIndex == -1 || !Main.projectile[flattenedDiskIndex].active
                || Main.projectile[flattenedDiskIndex].type != ModContent.ProjectileType<FlattenedAccretionDisk>()) {

                flattenedDiskIndex = Projectile.NewProjectile(
                    Source,
                    ShootPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<FlattenedAccretionDisk>(),
                    Owner.GetShootState().WeaponDamage,
                    Owner.GetShootState().WeaponKnockback,
                    Owner.whoAmI
                );
            }

            if (flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active) {
                Projectile disk = Main.projectile[flattenedDiskIndex];
                disk.Center = ShootPos;
                disk.timeLeft = 10;
                disk.rotation = ToMouseA;

                if (disk.ModProjectile is FlattenedAccretionDisk flattenedDisk) {
                    float sizeScale = MathHelper.Lerp(0.3f, 2.0f, chargeProgressR);
                    disk.scale = sizeScale;
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
            if (chargeTimeR < minChargeTimeR) {
                ResetChargeR();
                return;
            }

            //清理压扁吸积盘
            if (flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active) {
                Main.projectile[flattenedDiskIndex].Kill();
            }

            //播放释放音效
            PlayReleaseSoundR();

            //生成释放特效
            SpawnReleaseEffectR();

            //屏幕震动
            Owner.GetModPlayer<CWRPlayer>().GetScreenShake(3f + chargeProgressR * 8f);

            //消耗魔力
            int manaCost = (int)(Item.mana * 0.8f * (1f + chargeProgressR * 0.5f));
            Owner.statMana -= manaCost;
            if (Owner.statMana < 0) {
                Owner.statMana = 0;
            }

            chargeTimeR = 0;
            flattenedDiskIndex = -1;
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
                velocity.Y *= 0.6f; //保持压扁效果

                //使用高级粒子创建释放爆发效果
                BasePRT particle = new PRT_AccretionDiskImpact(
                    ShootPos,
                    velocity,
                    currentGlowColorR,
                    Main.rand.NextFloat(0.6f, 1.2f),
                    Main.rand.Next(25, 40),
                    Main.rand.NextFloat(-0.3f, 0.3f),
                    true,
                    Main.rand.NextFloat(0.2f, 0.35f)
                );
                PRTLoader.AddParticle(particle);
            }

            //生成扁平冲击波
            for (int i = 0; i < 2; i++) {
                int segments = 48;
                float radius = 30f + i * 40f + chargeProgressR * 40f;

                for (int j = 0; j < segments; j++) {
                    float angle = MathHelper.TwoPi * j / segments;
                    Vector2 offset = angle.ToRotationVector2() * radius;
                    offset.Y *= 0.6f;
                    Vector2 particlePos = ShootPos + offset;
                    Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 2.5f;

                    //扁平冲击波粒子
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        particlePos,
                        particleVel,
                        currentGlowColorR * 0.6f,
                        Main.rand.NextFloat(0.7f, 1.3f),
                        Main.rand.Next(30, 45),
                        Main.rand.NextFloat(-0.25f, 0.25f),
                        false,
                        Main.rand.NextFloat(0.22f, 0.32f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        private void ResetChargeR() {
            chargeTimeR = 0;
            chargeProgressR = 0;
            particleTimerR = 0;
            hasReleasedAttackR = false;

            if (flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active) {
                Main.projectile[flattenedDiskIndex].Kill();
            }
            flattenedDiskIndex = -1;
        }

        public override void FiringShoot() {
            //蓄力武器不使用默认射击
        }

        public override void FiringShootR() {
            //右键蓄力武器不使用默认射击
        }

        public override void PostGunDraw(Vector2 drawPos, ref Color lightColor) {
            base.PostGunDraw(drawPos, ref lightColor);
        }

        public override void OnKill(int timeLeft) {
            ResetCharge();
            ResetChargeR();
        }
    }
}
