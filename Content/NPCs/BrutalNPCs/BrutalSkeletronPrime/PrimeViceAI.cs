    using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class PrimeViceAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeVice;
        public override bool CanLoad() => true;
        public override bool? CheckDead() => true;

        #region 状态枚举
        private enum AttackState
        {
            Idle = 0,           //待机
            WindUp = 1,         //蓄力
            Strike = 2,         //打击
            Recovery = 3,       //恢复
            Combo = 4,          //连击
            SpecialCharge = 5   //特殊冲锋
        }
        #endregion

        #region 物理模拟参数
        private Vector2 velocity = Vector2.Zero;
        private Vector2 targetPosition = Vector2.Zero;
        private const float SpringStiffness = 0.18f;
        private const float Damping = 0.82f;
        private const float MaxSpeed = 28f;
        private const float WindUpDistance = 280f;
        private int stateTimer = 0;
        private int comboCount = 0;
        private float impactIntensity = 0f;
        private bool hasImpacted = false;
        #endregion

        #region AI主循环
        public override bool ArmBehavior() {
            AttackState currentState = (AttackState)(int)npc.ai[2];

            //更新物理效果
            UpdatePhysicsEffects();

            //距离检测
            Vector2 viceArmPosition = npc.Center;
            float viceArmIdleXPos = head.Center.X - 200f * npc.ai[0] - viceArmPosition.X;
            float viceArmIdleYPos = head.position.Y + 230f - viceArmPosition.Y;
            float viceArmIdleDistance = MathF.Sqrt(viceArmIdleXPos * viceArmIdleXPos + viceArmIdleYPos * viceArmIdleYPos);

            //距离过远则返回
            if (currentState != AttackState.SpecialCharge && viceArmIdleDistance > 800f) {
                npc.ai[2] = (float)AttackState.SpecialCharge;
                stateTimer = 0;
                npc.netUpdate = true;
            }
            else if (currentState == AttackState.SpecialCharge && viceArmIdleDistance < 400f) {
                npc.ai[2] = (float)AttackState.Idle;
                stateTimer = 0;
                npc.netUpdate = true;
            }

            //状态机
            switch (currentState) {
                case AttackState.Idle:
                    State_Idle();
                    break;
                case AttackState.WindUp:
                    State_WindUp();
                    break;
                case AttackState.Strike:
                    State_Strike();
                    break;
                case AttackState.Recovery:
                    State_Recovery();
                    break;
                case AttackState.Combo:
                    State_Combo();
                    break;
                case AttackState.SpecialCharge:
                    State_SpecialCharge();
                    break;
            }

            //更新动画
            UpdateAnimation();

            return false;
        }
        #endregion

        #region 状态行为
        private void State_Idle() {
            stateTimer++;
            npc.damage = 0;
            hasImpacted = false;

            //计算理想待机位置
            Vector2 idleOffset = new Vector2(-150f * npc.ai[0], 250f);
            targetPosition = head.Center + idleOffset;

            //弹簧物理移动
            SpringPhysicsMove(targetPosition, 0.7f);

            //计算旋转
            Vector2 toTarget = targetPosition - npc.Center;
            npc.rotation = toTarget.ToRotation() + MathHelper.PiOver2;

            //检查是否应该攻击
            if (head.ai[1] == 3f && npc.timeLeft > 10) {
                npc.timeLeft = 10;
                return;
            }

            //充能计时
            float chargeRate = CalculateChargeRate();
            npc.ai[3] += chargeRate;

            int chargeThreshold = masterMode ? 120 : 180;
            if (death) chargeThreshold = 60;

            if (npc.ai[3] >= chargeThreshold) {
                //选择攻击类型
                if (Main.rand.NextBool(3) && !death) {
                    TransitionToState(AttackState.Combo);
                    comboCount = 0;
                }
                else {
                    TransitionToState(AttackState.WindUp);
                }
            }
        }

        private void State_WindUp() {
            stateTimer++;
            npc.damage = 0;

            //后撤蓄力
            Vector2 directionToPlayer = npc.Center.DirectionTo(player.Center);
            Vector2 windUpPos = player.Center - directionToPlayer * WindUpDistance;
            targetPosition = windUpPos;

            //更快的蓄力移动
            SpringPhysicsMove(targetPosition, 1.3f);

            //朝向玩家
            npc.rotation = directionToPlayer.ToRotation() + MathHelper.PiOver2;

            //蓄力粒子效果
            if (stateTimer % 5 == 0) {
                SpawnWindUpParticles();
            }

            //蓄力音效
            if (stateTimer == 10) {
                SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.6f, Pitch = -0.3f }, npc.Center);
            }

            //蓄力完成，发起攻击
            int windUpDuration = masterMode ? 20 : 25;
            if (death) windUpDuration = 15;

            if (stateTimer >= windUpDuration) {
                TransitionToState(AttackState.Strike);

                //计算冲锋速度
                float chargeVelocity = CalculateChargeVelocity(bossRush, 20f, 16f);
                velocity = directionToPlayer * chargeVelocity;

                //攻击音效
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.2f }, npc.Center);
            }
        }

        private void State_Strike() {
            stateTimer++;
            npc.damage = npc.defDamage;

            //保持高速移动
            npc.velocity = velocity * 0.95f;
            velocity = npc.velocity;

            //朝向移动方向
            npc.rotation = velocity.ToRotation() + MathHelper.PiOver2;

            //生成运动轨迹
            if (stateTimer % 2 == 0) {
                SpawnStrikeTrail();
            }

            //检测是否击中或超时
            float distanceToPlayer = npc.Distance(player.Center);

            if (distanceToPlayer < 80f && !hasImpacted) {
                //击中效果
                OnImpact();
                hasImpacted = true;
            }

            if (stateTimer >= 45 || distanceToPlayer > 1200f || npc.justHit) {
                TransitionToState(AttackState.Recovery);
            }
        }

        private void State_Recovery() {
            stateTimer++;
            npc.damage = 0;

            //减速
            velocity *= 0.92f;
            npc.velocity = velocity;

            //逐渐返回待机位置
            Vector2 returnOffset = new Vector2(-180f * npc.ai[0], 240f);
            targetPosition = head.Center + returnOffset;

            SpringPhysicsMove(targetPosition, 0.9f);

            //朝向目标
            Vector2 toTarget = targetPosition - npc.Center;
            float targetRot = toTarget.ToRotation() + MathHelper.PiOver2;
            npc.rotation = MathHelper.Lerp(npc.rotation, targetRot, 0.15f);

            int recoveryDuration = masterMode ? 30 : 40;
            if (stateTimer >= recoveryDuration) {
                TransitionToState(AttackState.Idle);
                npc.ai[3] = 0f;
            }
        }

        private void State_Combo() {
            stateTimer++;

            if (comboCount == 0) {
                //第一击：快速刺击
                ExecuteJabAttack();
            }
            else if (comboCount == 1) {
                //第二击：横扫
                ExecuteSweepAttack();
            }
            else if (comboCount == 2) {
                //第三击：重击
                ExecuteHeavyAttack();
            }

            //连击间隔
            int comboInterval = masterMode ? 25 : 35;
            if (stateTimer >= comboInterval) {
                comboCount++;
                stateTimer = 0;

                if (comboCount >= 3) {
                    TransitionToState(AttackState.Recovery);
                    comboCount = 0;
                }
                else {
                    //重新定位
                    Vector2 dirToPlayer = npc.Center.DirectionTo(player.Center);
                    targetPosition = player.Center - dirToPlayer * 200f;
                }
            }
        }

        private void State_SpecialCharge() {
            stateTimer++;

            float acceleration = CalculateAcceleration(bossRush, death, masterMode, cannonAlive, laserAlive, sawAlive);
            float accelerationMult = CalculateAccelerationMult(cannonAlive, laserAlive);
            if (masterMode) acceleration *= accelerationMult;

            float topVelocity = acceleration * 100f;
            float deceleration = masterMode ? 0.6f : 0.8f;

            //快速返回头部
            AdjustVelocityY(topVelocity, deceleration, acceleration, head.position.Y + 20f, head.position.Y - 20f);
            AdjustVelocityX(topVelocity, deceleration, acceleration, head.Center.X + 20f, head.Center.X - 20f, 2f);

            //更新旋转
            Vector2 toHead = head.Center - npc.Center;
            npc.rotation = toHead.ToRotation() + MathHelper.PiOver2;
        }
        #endregion

        #region 连击子攻击
        private void ExecuteJabAttack() {
            npc.damage = (int)(npc.defDamage * 0.8f);

            Vector2 dirToPlayer = npc.Center.DirectionTo(player.Center);
            float speed = 18f + (death ? 6f : 0f);

            if (stateTimer < 10) {
                //蓄力
                targetPosition = player.Center - dirToPlayer * 180f;
                SpringPhysicsMove(targetPosition, 1.2f);
            }
            else {
                //刺击
                velocity = dirToPlayer * speed;
                npc.velocity = velocity;

                if (stateTimer % 3 == 0) {
                    SpawnStrikeTrail();
                }
            }

            npc.rotation = dirToPlayer.ToRotation() + MathHelper.PiOver2;
        }

        private void ExecuteSweepAttack() {
            npc.damage = npc.defDamage;

            Vector2 dirToPlayer = npc.Center.DirectionTo(player.Center);
            float sweepAngle = MathHelper.Pi * 0.6f;
            float progress = stateTimer / 30f;

            Vector2 sweepDir = dirToPlayer.RotatedBy(-sweepAngle / 2f + sweepAngle * progress);
            targetPosition = player.Center + sweepDir * 150f;

            SpringPhysicsMove(targetPosition, 1.5f);
            npc.rotation = sweepDir.ToRotation() + MathHelper.PiOver2;

            if (stateTimer % 2 == 0) {
                SpawnSweepParticles();
            }
        }

        private void ExecuteHeavyAttack() {
            npc.damage = (int)(npc.defDamage * 1.5f);

            Vector2 dirToPlayer = npc.Center.DirectionTo(player.Center);

            if (stateTimer < 15) {
                //大幅后撤
                targetPosition = player.Center - dirToPlayer * 320f;
                SpringPhysicsMove(targetPosition, 1.0f);
            }
            else if (stateTimer == 15) {
                //重击冲锋
                float chargeSpeed = 28f + (death ? 8f : 0f);
                velocity = dirToPlayer * chargeSpeed;
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.0f, Pitch = -0.2f }, npc.Center);
            }
            else {
                npc.velocity = velocity * 0.96f;
                velocity = npc.velocity;

                if (stateTimer % 2 == 0) {
                    SpawnHeavyTrail();
                }
            }

            npc.rotation = dirToPlayer.ToRotation() + MathHelper.PiOver2;
        }
        #endregion

        #region 辅助函数
        private void TransitionToState(AttackState newState) {
            npc.ai[2] = (float)newState;
            stateTimer = 0;
            npc.netUpdate = true;
        }

        private void SpringPhysicsMove(Vector2 target, float speedMultiplier = 1f) {
            Vector2 toTarget = target - npc.Center;

            //弹簧力
            Vector2 springForce = toTarget * SpringStiffness * speedMultiplier;
            velocity += springForce;

            //阻尼
            velocity *= Damping;

            //限速
            if (velocity.LengthSquared() > MaxSpeed * MaxSpeed) {
                velocity = Vector2.Normalize(velocity) * MaxSpeed;
            }

            npc.velocity = velocity;
        }

        private void UpdatePhysicsEffects() {
            //冲击强度衰减
            impactIntensity *= 0.88f;
        }

        private void OnImpact() {
            impactIntensity = 8f;

            //击中音效
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.8f }, npc.Center);

            //击中粒子
            for (int i = 0; i < 30; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(npc.Center - Vector2.One * 30, 60, 60,
                    Main.rand.Next(new int[] { DustID.Electric, DustID.SteampunkSteam, DustID.Smoke }),
                    particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(1.2f, 2.0f));
                dust.noGravity = true;
            }

            //冲击波
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 shockwaveVel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                Dust dust = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Electric,
                    shockwaveVel.X, shockwaveVel.Y, 100, Color.Cyan, 1.5f);
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }
        }

        private void SpawnWindUpParticles() {
            Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(40, 40);
            Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.Electric,
                0, 0, 100, Color.Yellow, Main.rand.NextFloat(0.8f, 1.4f));
            dust.velocity = (npc.Center - particlePos) * 0.1f;
            dust.noGravity = true;
        }

        private void SpawnStrikeTrail() {
            Vector2 trailPos = npc.Center + Main.rand.NextVector2Circular(30, 30);
            Dust dust = Dust.NewDustDirect(trailPos, 1, 1, DustID.Electric,
                -velocity.X * 0.3f, -velocity.Y * 0.3f, 100, Color.Cyan, Main.rand.NextFloat(1.0f, 1.8f));
            dust.noGravity = true;
        }

        private void SpawnSweepParticles() {
            Dust dust = Dust.NewDustDirect(npc.Center, npc.width, npc.height, DustID.SteampunkSteam,
                velocity.X * 0.2f, velocity.Y * 0.2f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
            dust.noGravity = true;
        }

        private void SpawnHeavyTrail() {
            for (int i = 0; i < 2; i++) {
                Dust dust = Dust.NewDustDirect(npc.Center - velocity * 2f, 1, 1, DustID.Torch,
                    -velocity.X * 0.4f, -velocity.Y * 0.4f, 100, Color.OrangeRed, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
            }
        }

        private void UpdateAnimation() {
            if (Main.GameUpdateCount % 10 == 0) {
                if (++frame > 1) {
                    frame = 0;
                }
            }
        }

        private float CalculateChargeRate() {
            float baseRate = masterMode ? 2f : 1f;
            if (death) baseRate *= 2f;
            if (!cannonAlive) baseRate += 0.5f;
            if (!laserAlive) baseRate += 0.5f;
            if (!sawAlive) baseRate += 0.5f;
            return baseRate;
        }

        private float CalculateChargeVelocity(bool bossRush, float bossRushVelocity, float defaultVelocity) {
            float chargeVelocity = bossRush ? bossRushVelocity : defaultVelocity;
            if (!cannonAlive) chargeVelocity += 1.5f;
            if (!laserAlive) chargeVelocity += 1.5f;
            if (!sawAlive) chargeVelocity += 1.5f;
            if (death) chargeVelocity *= 1.2f;
            return chargeVelocity;
        }

        private float CalculateAcceleration(bool bossRush, bool death, bool masterMode, bool cannonAlive, bool laserAlive, bool sawAlive) {
            float baseAcceleration = bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f);
            if (!cannonAlive) baseAcceleration += 0.025f;
            if (!laserAlive) baseAcceleration += 0.025f;
            if (!sawAlive) baseAcceleration += 0.025f;
            return baseAcceleration;
        }

        private float CalculateAccelerationMult(bool cannonAlive, bool laserAlive) {
            float mult = 1f;
            if (!cannonAlive) mult += 0.4f;
            if (!laserAlive) mult += 0.4f;
            return mult;
        }

        private void AdjustVelocityY(float topVelocity, float deceleration, float acceleration, float upperBound, float lowerBound) {
            if (npc.position.Y > upperBound) {
                if (npc.velocity.Y > 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < lowerBound) {
                if (npc.velocity.Y < 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) npc.velocity.Y = -topVelocity;
            }
        }

        private void AdjustVelocityX(float topVelocity, float deceleration, float acceleration, float upperBound, float lowerBound, float factor = 1f) {
            if (npc.Center.X > upperBound) {
                if (npc.velocity.X > 0f) npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration * factor;
                if (npc.velocity.X > topVelocity) npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < lowerBound) {
                if (npc.velocity.X < 0f) npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration * factor;
                if (npc.velocity.X < -topVelocity) npc.velocity.X = -topVelocity;
            }
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }
            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPPliers.Value;
            Texture2D mainValue2 = HeadPrimeAI.BSPPliersGlow.Value;

            //添加抖动效果
            Vector2 drawOffset = Vector2.Zero;
            if (impactIntensity > 0.5f) {
                drawOffset = Main.rand.NextVector2Circular(impactIntensity, impactIntensity);
            }

            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition + drawOffset, mainValue.GetRectangle(frame, 2),
                drawColor, npc.rotation, VaultUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition + drawOffset, mainValue.GetRectangle(frame, 2),
                Color.White, npc.rotation, VaultUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
        #endregion
    }
}
