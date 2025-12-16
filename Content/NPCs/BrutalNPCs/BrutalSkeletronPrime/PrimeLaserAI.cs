using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using CalamityOverhaul.OtherMods.InfernumMode;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class PrimeLaserAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeLaser;
        public override bool CanLoad() => true;

        #region 状态枚举
        private enum AttackState
        {
            Idle = 0,           //待机瞄准
            RapidFire = 1,      //快速射击
            ChargedShot = 2,    //蓄力射击
            LaserBarrage = 3    //激光弹幕
        }
        #endregion

        #region 常量与变量
        private const float TimeToNotAttack = 180f;
        private const float LaserChargeTime = 45f;
        private const float MaxLaserIntensity = 2.5f;

        private bool normalLaserRotation;
        private int lerterFireIndex;
        private int stateTimer = 0;
        private float laserChargeProgress = 0f;
        private float laserIntensity = 0f;
        private Vector2 aimDirection = Vector2.Zero;
        private float rotationSpeed = 0f;
        #endregion

        #region AI主循环
        public override bool ArmBehavior() {
            dontAttack = npc.RefNPCNewAI()[2] < TimeToNotAttack;
            normalLaserRotation = npc.localAI[1] % 2f == 0f;

            if (dontAttack) {
                npc.RefNPCNewAI()[2]++;
                if (npc.RefNPCNewAI()[2] >= TimeToNotAttack) {
                    HeadPrimeAI.SendExtraAI(npc);
                }
            }

            //更新视觉效果
            UpdateLaserEffects();

            //移动控制
            Movement();

            AttackState currentState = (AttackState)(int)npc.ai[2];

            //状态机
            switch (currentState) {
                case AttackState.Idle:
                    State_Idle();
                    break;
                case AttackState.RapidFire:
                    State_RapidFire();
                    break;
                case AttackState.ChargedShot:
                    State_ChargedShot();
                    break;
                case AttackState.LaserBarrage:
                    State_LaserBarrage();
                    break;
            }

            return false;
        }
        #endregion

        #region 状态行为
        private void State_Idle() {
            stateTimer++;
            laserChargeProgress = 0f;

            //检查头部状态
            if (head.ai[1] == 3f && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            //平滑瞄准玩家
            SmoothAimAtPlayer(0.08f);

            //充能计时
            float chargeRate = CalculateChargeRate();
            npc.ai[3] += chargeRate;

            int chargeThreshold = masterMode ? 120 : 300;

            if (npc.ai[3] >= chargeThreshold) {
                //选择攻击模式
                if (Main.rand.NextBool(3) || death) {
                    TransitionToState(AttackState.ChargedShot);
                }
                else if (Main.rand.NextBool()) {
                    TransitionToState(AttackState.LaserBarrage);
                }
                else {
                    TransitionToState(AttackState.RapidFire);
                }

                npc.ai[3] = 0f;
                npc.TargetClosest();
            }
        }

        private void State_RapidFire() {
            stateTimer++;

            //快速瞄准
            SmoothAimAtPlayer(0.15f);

            if (!VaultUtils.isClient && !dontAttack) {
                npc.localAI[0] += 1f;
                if (!cannonAlive) npc.localAI[0] += 1f;
                if (!viceAlive) npc.localAI[0] += 1f;
                if (!sawAlive) npc.localAI[0] += 1f;

                int fireCooldown = masterMode ? 28 : 38;
                if (death) fireCooldown -= 10;
                if (bossRush) fireCooldown = 10;

                bool canFire = true;
                if (CWRRef.GetDeathMode()) {
                    canFire = bossRush;
                }

                if (npc.localAI[0] >= fireCooldown && canFire) {
                    FireLaser(false);
                    npc.localAI[0] = 0f;
                    lerterFireIndex++;

                    //射击后坐力
                    npc.velocity -= aimDirection * 2f;
                }
            }

            //快速射击持续时间
            int rapidFireDuration = masterMode ? 180 : 240;
            if (death) rapidFireDuration = 120;

            if (stateTimer >= rapidFireDuration) {
                TransitionToState(AttackState.Idle);
                lerterFireIndex = 0;
            }
        }

        private void State_ChargedShot() {
            stateTimer++;

            //蓄力阶段
            if (laserChargeProgress < LaserChargeTime) {
                laserChargeProgress += 1f + (death ? 0.5f : 0f);

                //蓄力瞄准（锁定玩家)
                if (laserChargeProgress < LaserChargeTime * 0.7f) {
                    SmoothAimAtPlayer(0.06f);
                }

                //蓄力粒子效果
                if (stateTimer % 3 == 0) {
                    SpawnChargeParticles();
                }

                //蓄力音效
                if (stateTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.7f, Pitch = -0.4f }, npc.Center);
                }
                if ((int)laserChargeProgress == (int)(LaserChargeTime * 0.7f)) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f }, npc.Center);
                }
            }
            else {
                //发射蓄力激光
                if (!VaultUtils.isClient && laserChargeProgress == LaserChargeTime) {
                    FireChargedLaser();

                    //巨大后坐力
                    npc.velocity -= aimDirection * 12f;

                    //发射音效
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 1.2f, Pitch = -0.3f }, npc.Center);
                }

                laserChargeProgress++;

                //发射后冷却
                if (laserChargeProgress >= LaserChargeTime + 30) {
                    TransitionToState(AttackState.Idle);
                    laserChargeProgress = 0f;
                }
            }
        }

        private void State_LaserBarrage() {
            stateTimer++;

            //旋转扫射
            float scanSpeed = (masterMode ? 0.04f : 0.03f) * (death ? 1.5f : 1f);
            rotationSpeed = MathHelper.Lerp(rotationSpeed, scanSpeed, 0.1f);

            npc.rotation += rotationSpeed * npc.ai[0];

            int typeSetPosingStarm = ModContent.ProjectileType<SetPosingStarm>();
            int existingCount = 0;
            foreach (var proj in Main.projectile) {
                if (proj.active && proj.type == typeSetPosingStarm) {
                    existingCount++;
                }
            }

            if (!VaultUtils.isClient && !dontAttack && existingCount == 0) {
                npc.localAI[0] += 1f;
                if (!cannonAlive) npc.localAI[0] += 0.5f;
                if (!viceAlive) npc.localAI[0] += 0.5f;
                if (!sawAlive) npc.localAI[0] += 0.5f;

                if (npc.localAI[0] >= 90f) {
                    npc.TargetClosest();
                    FireLaserRing();
                    npc.localAI[1] += 1f;
                    npc.localAI[0] = 0f;

                    //发射音效
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.9f, Pitch = 0.2f }, npc.Center);
                }
            }

            //弹幕攻击持续时间
            float timeLimit = 135f;
            float timeMult = 1.882075f;
            if (!cannonAlive) timeLimit *= timeMult;
            if (!viceAlive) timeLimit *= timeMult;
            if (!sawAlive) timeLimit *= timeMult;

            if (stateTimer >= timeLimit) {
                TransitionToState(AttackState.Idle);
                rotationSpeed = 0f;
            }
        }
        #endregion

        #region 攻击函数
        private void FireLaser(bool charged = false) {
            npc.TargetClosest();
            float laserSpeed = bossRush ? 5f : 4f;
            int type = ProjectileID.DeathLaser;
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(npc, type));

            HeadPrimeAI.SpanFireLerterDustEffect(npc, 3);

            Vector2 laserVelocity = aimDirection * laserSpeed;
            Vector2 spawnPos = npc.Center + aimDirection * 100f;
            laserVelocity *= 1 + (lerterFireIndex * 0.1f);

            if (death) {
                type = ModContent.ProjectileType<DeadLaser>();
                laserVelocity *= 0.65f;
            }

            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, laserVelocity, type, damage, 0f, Main.myPlayer, 1f, 0f);
        }

        private void FireChargedLaser() {
            int type = death ? ModContent.ProjectileType<DeadLaser>() : ProjectileID.DeathLaser;
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(npc, type));
            damage = (int)(damage * 2.5f);

            float laserSpeed = death ? 12f : 15f;
            Vector2 laserVelocity = aimDirection * laserSpeed;
            Vector2 spawnPos = npc.Center + aimDirection * 100f;

            //发射主激光
            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, laserVelocity, type, damage, 0f, Main.myPlayer, 1f, 0f);

            //额外的扩散激光
            if (masterMode || death) {
                for (int i = -2; i <= 2; i++) {
                    if (i == 0) continue;
                    float spread = i * 0.12f;
                    Vector2 spreadVel = laserVelocity.RotatedBy(spread) * 0.8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, spreadVel, type, (int)(damage * 0.6f), 0f, Main.myPlayer, 1f, 0f);
                }
            }

            //超大充能特效
            HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);

            //充能爆炸粒子
            for (int i = 0; i < 50; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(10f, 10f);
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Electric, particleVel.X, particleVel.Y, 100, Color.Cyan, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }
        }

        private void FireLaserRing() {
            int totalProjectiles = bossRush ? 22 : (masterMode ? 13 : 10);
            float radians = MathHelper.TwoPi / totalProjectiles;
            int type = ProjectileID.DeathLaser;
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(npc, type));

            float velocity = 3f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float laserVelocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            Vector2 spinningPoint = normalLaserRotation ? new Vector2(0f, -velocity) : new Vector2(-laserVelocityX, -velocity);

            bool spanLerter = HeadPrimeAI.setPosingStarmCount <= 0;
            if (spanLerter) {
                if (death) {
                    totalProjectiles = bossRush ? 12 : 6;
                    radians = MathHelper.TwoPi / totalProjectiles;

                    if (InfernumRef.InfernumModeOpenState || CWRWorld.MachineRebellion) {
                        for (int j = 0; j < 5; j++) {
                            for (int k = 0; k < totalProjectiles; k++) {
                                float speedMode = 1.55f + j * 0.3f;
                                if (bossRush) speedMode = 1.7f + j * 0.35f;

                                Vector2 laserFireDirection = spinningPoint.RotatedBy(radians * k);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + laserFireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                                    laserFireDirection * speedMode, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                            }
                        }
                    }
                    else {
                        Vector2 toTarget = npc.Center.To(player.Center).UnitVector();
                        for (int i = 0; i < 3; i++) {
                            int index = i - 1;
                            Vector2 laserFireDirection = spinningPoint.RotatedBy(index * 0.12f);
                            Vector2 ver = toTarget.RotatedBy(index * 0.12f) * 3;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + laserFireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                                ver, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                        }
                    }
                    HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);
                }
                else {
                    for (int k = 0; k < totalProjectiles; k++) {
                        Vector2 laserFireDirection = spinningPoint.RotatedBy(radians * k);
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + laserFireDirection.SafeNormalize(Vector2.UnitY) * 100f,
                            laserFireDirection, type, damage, 0f, Main.myPlayer, 1f, 0f);
                        Main.projectile[proj].timeLeft = 900;
                    }
                }
            }
        }
        #endregion

        #region 辅助函数
        private void Movement() {
            float acceleration = bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f);
            float accelerationMult = 1f;

            if (!cannonAlive) {
                acceleration += 0.025f;
                accelerationMult += 0.5f;
            }
            if (!viceAlive) acceleration += 0.025f;
            if (!sawAlive) acceleration += 0.025f;
            if (masterMode) acceleration *= accelerationMult;

            float topVelocity = acceleration * 100f;
            float deceleration = masterMode ? 0.6f : 0.8f;

            //Y轴控制
            if (npc.position.Y > head.position.Y - 80f) {
                if (npc.velocity.Y > 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < head.position.Y - 120f) {
                if (npc.velocity.Y < 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) npc.velocity.Y = -topVelocity;
            }

            //X轴控制
            if (npc.Center.X > head.Center.X - 160f * npc.ai[0]) {
                if (npc.velocity.X > 0f) npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration;
                if (npc.velocity.X > topVelocity) npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < head.Center.X - 200f * npc.ai[0]) {
                if (npc.velocity.X < 0f) npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration;
                if (npc.velocity.X < -topVelocity) npc.velocity.X = -topVelocity;
            }
        }

        private void SmoothAimAtPlayer(float smoothness) {
            Vector2 toPlayer = player.Center - npc.Center;
            aimDirection = Vector2.Lerp(aimDirection, Vector2.Normalize(toPlayer), smoothness);
            if (aimDirection == Vector2.Zero) aimDirection = Vector2.UnitX;

            float targetRotation = aimDirection.ToRotation() - MathHelper.PiOver2;
            npc.rotation = MathHelper.Lerp(npc.rotation, targetRotation, smoothness);
        }

        private void UpdateLaserEffects() {
            //充能强度衰减
            laserIntensity = MathHelper.Lerp(laserIntensity, laserChargeProgress / LaserChargeTime * MaxLaserIntensity, 0.15f);

            //激光蓄力粒子
            if (laserChargeProgress > 0 && laserChargeProgress < LaserChargeTime) {
                float intensity = laserChargeProgress / LaserChargeTime;
                if (Main.rand.NextFloat() < intensity * 0.3f) {
                    Vector2 particlePos = npc.Center + aimDirection * 60f + Main.rand.NextVector2Circular(20, 20);
                    Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.Electric, 0, 0, 100, Color.Cyan, Main.rand.NextFloat(0.8f, 1.5f));
                    dust.velocity = (npc.Center + aimDirection * 80f - particlePos) * 0.1f;
                    dust.noGravity = true;
                }
            }
        }

        private void SpawnChargeParticles() {
            float intensity = laserChargeProgress / LaserChargeTime;
            int particleCount = (int)(intensity * 3f) + 1;

            for (int i = 0; i < particleCount; i++) {
                Vector2 particlePos = npc.Center + aimDirection * 80f + Main.rand.NextVector2Circular(30 * intensity, 30 * intensity);
                Vector2 particleVel = (npc.Center + aimDirection * 80f - particlePos) * 0.15f;

                Color particleColor = Color.Lerp(Color.Yellow, Color.Cyan, intensity);
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.Electric, particleVel.X, particleVel.Y, 100, particleColor, Main.rand.NextFloat(1.0f, 1.8f) * intensity);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        private void TransitionToState(AttackState newState) {
            npc.ai[2] = (float)newState;
            stateTimer = 0;
            npc.netUpdate = true;
        }

        private float CalculateChargeRate() {
            float baseRate = 1f;
            if (!cannonAlive) baseRate += 1f;
            if (!viceAlive) baseRate += 1f;
            if (!sawAlive) baseRate += 1f;
            return baseRate;
        }
        #endregion

        #region 绘制
        public override bool? CheckDead() => true;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            bool dir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2().X > 0;
            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPlaser.Value;
            Texture2D mainValue2 = HeadPrimeAI.BSPlaserGlow.Value;

            //绘制主体
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, null, drawColor,
                npc.rotation, mainValue.Size() / 2, npc.scale,
                dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            //绘制发光层（根据充能强度）
            float glowIntensity = MathHelper.Clamp(0.8f + laserIntensity * 0.2f, 0.8f, 1.5f);
            Color glowColor = Color.White * glowIntensity;
            if (laserChargeProgress > 0 && laserChargeProgress < LaserChargeTime) {
                float chargeProgress = laserChargeProgress / LaserChargeTime;
                glowColor = Color.Lerp(Color.White, Color.Cyan, chargeProgress) * (0.8f + chargeProgress * 0.7f);
            }

            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, null, glowColor,
                npc.rotation, mainValue2.Size() / 2, npc.scale,
                dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
        #endregion
    }
}
