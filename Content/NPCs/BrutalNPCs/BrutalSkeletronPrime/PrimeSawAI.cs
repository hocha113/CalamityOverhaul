using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class PrimeSawAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeSaw;
        public override bool CanLoad() => true;
        public override bool? CheckDead() => true;

        #region 状态枚举
        private enum AttackState
        {
            Idle = 0,           //待机
            SpinUp = 1,         //加速旋转
            Dash = 2,           //冲刺
            Orbit = 3,          //环绕
            DrillChase = 4,     //追击钻击
            Recovery = 99       //返回
        }
        #endregion

        #region 物理模拟参数
        private Vector2 velocity = Vector2.Zero;
        private Vector2 targetPosition = Vector2.Zero;
        private const float SpringStiffness = 0.16f;
        private const float Damping = 0.84f;
        private const float MaxSpeed = 30f;
        private float spinSpeed = 0f;
        private float targetSpinSpeed = 0f;
        private int stateTimer = 0;
        private int dashCount = 0;
        private float orbitAngle = 0f;
        private float orbitRadius = 200f;
        private bool hasPlayedSpinSound = false;
        #endregion

        #region AI主循环
        public override bool ArmBehavior() {
            AttackState currentState = (AttackState)(int)npc.ai[2];

            //更新旋转动画
            UpdateSpinAnimation();

            //距离检测
            Vector2 sawArmLocation = npc.Center;
            float sawArmIdleXPos = head.Center.X - 200f * npc.ai[0] - sawArmLocation.X;
            float sawArmIdleYPos = head.position.Y + 230f - sawArmLocation.Y;
            float sawArmIdleDistance = (float)Math.Sqrt(sawArmIdleXPos * sawArmIdleXPos + sawArmIdleYPos * sawArmIdleYPos);

            //距离检测
            if (currentState != AttackState.Recovery) {
                if (sawArmIdleDistance > 800f) {
                    TransitionToState(AttackState.Recovery);
                }
            }
            else if (sawArmIdleDistance < 400f) {
                TransitionToState(AttackState.Idle);
            }

            //状态机
            switch (currentState) {
                case AttackState.Idle:
                    State_Idle();
                    break;
                case AttackState.SpinUp:
                    State_SpinUp();
                    break;
                case AttackState.Dash:
                    State_Dash();
                    break;
                case AttackState.Orbit:
                    State_Orbit();
                    break;
                case AttackState.DrillChase:
                    State_DrillChase();
                    break;
                case AttackState.Recovery:
                    State_Recovery();
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

            //减速旋转
            targetSpinSpeed = 0.05f;

            //理想待机位置
            Vector2 idleOffset = new Vector2(-125f * npc.ai[0], 290f);
            targetPosition = head.Center + idleOffset;

            //平滑移动
            SpringPhysicsMove(targetPosition, 0.65f);

            //计算旋转朝向
            Vector2 toTarget = targetPosition - npc.Center;
            npc.rotation = MathHelper.Lerp(npc.rotation, toTarget.ToRotation() + MathHelper.PiOver2, 0.1f);

            //检查头部状态
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
                //随机选择攻击模式
                int attackType = Main.rand.Next(3);

                if (attackType == 0 || death) {
                    TransitionToState(AttackState.SpinUp);
                    dashCount = 0;
                }
                else if (attackType == 1) {
                    TransitionToState(AttackState.Orbit);
                    orbitAngle = npc.Center.AngleTo(player.Center);
                }
                else {
                    TransitionToState(AttackState.DrillChase);
                }

                npc.ai[3] = 0f;
                npc.TargetClosest();
            }
        }

        private void State_SpinUp() {
            stateTimer++;
            npc.damage = 0;

            //快速加速旋转
            targetSpinSpeed = 0.8f;

            //移动到玩家上方
            Vector2 dirToPlayer = npc.Center.DirectionTo(player.Center);
            targetPosition = player.Center - dirToPlayer * 200f;

            SpringPhysicsMove(targetPosition, 1.1f);

            //朝向玩家
            npc.rotation += spinSpeed * 2f;

            //加速粒子
            if (stateTimer % 3 == 0) {
                SpawnSpinUpParticles();
            }

            //音效
            if (stateTimer == 15 && !hasPlayedSpinSound) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.7f, Pitch = -0.2f }, npc.Center);
                hasPlayedSpinSound = true;
            }

            //加速完成，开始冲刺
            int spinUpDuration = masterMode ? 25 : 35;
            if (death) spinUpDuration = 20;

            if (stateTimer >= spinUpDuration) {
                TransitionToState(AttackState.Dash);

                //计算冲刺速度
                float dashSpeed = CalculateDashSpeed();
                velocity = npc.Center.DirectionTo(player.Center) * dashSpeed;

                //冲刺音效
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = 0.4f }, npc.Center);
            }
        }

        private void State_Dash() {
            stateTimer++;
            npc.damage = npc.defDamage;

            //保持高速旋转
            targetSpinSpeed = 1.2f;

            //持续冲刺
            npc.velocity = velocity;

            //快速旋转
            npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;

            //冲刺轨迹
            if (stateTimer % 2 == 0) {
                SpawnDashTrail();
            }

            //检查是否应该结束冲刺
            bool shouldEndDash = false;

            if (npc.justHit) {
                shouldEndDash = true;
            }
            else if (stateTimer >= 50) {
                shouldEndDash = true;
            }
            else if (npc.Distance(player.Center) > 1400f) {
                shouldEndDash = true;
            }

            if (shouldEndDash) {
                dashCount++;

                int maxDashes = CalculateMaxDashes();

                if (dashCount >= maxDashes || death) {
                    TransitionToState(AttackState.Recovery);
                    dashCount = 0;
                }
                else {
                    //继续下一次冲刺
                    TransitionToState(AttackState.SpinUp);
                }
            }
        }

        private void State_Orbit() {
            stateTimer++;
            npc.damage = npc.defDamage;

            //中速旋转
            targetSpinSpeed = 0.5f;

            //环绕玩家
            orbitAngle += (masterMode ? 0.12f : 0.09f) * (death ? 1.5f : 1f);
            orbitRadius = MathHelper.Lerp(orbitRadius, 180f, 0.05f);

            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * orbitRadius;
            targetPosition = player.Center + orbitOffset;

            //快速环绕移动
            SpringPhysicsMove(targetPosition, 1.4f);

            //旋转朝向运动方向
            npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;

            //环绕粒子
            if (stateTimer % 4 == 0) {
                SpawnOrbitParticles();
            }

            //持续音效
            if (stateTimer % 60 == 0) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = 0.1f }, npc.Center);
            }

            //结束环绕
            int orbitDuration = masterMode ? 180 : 240;
            if (death) orbitDuration = 120;

            if (stateTimer >= orbitDuration || npc.justHit) {
                TransitionToState(AttackState.Recovery);
            }
        }

        private void State_DrillChase() {
            stateTimer++;
            npc.damage = npc.defDamage;

            //高速旋转
            targetSpinSpeed = 1.0f;

            //持续追击玩家
            Vector2 dirToPlayer = npc.Center.DirectionTo(player.Center);
            Vector2 predictedPos = player.Center + player.velocity * 10f;

            targetPosition = predictedPos;

            //加速度追击
            float acceleration = bossRush ? 0.3f : (death ? 0.1f : 0.08f);
            if (masterMode) acceleration *= 1.25f;

            Vector2 toTarget = targetPosition - npc.Center;
            Vector2 targetVel = Vector2.Normalize(toTarget) * CalculateDrillSpeed();

            //平滑加速
            velocity = Vector2.Lerp(velocity, targetVel, acceleration);
            npc.velocity = velocity;

            //快速旋转
            npc.rotation = velocity.ToRotation() - MathHelper.PiOver2;

            //钻击粒子
            if (stateTimer % 2 == 0) {
                SpawnDrillParticles();
            }

            //被击中时加快计时
            if (npc.justHit) {
                npc.ai[3] += 3f;
            }

            npc.ai[3] += 1f;

            //结束追击
            if (npc.ai[3] >= 480f || npc.Distance(player.Center) > 1600f) {
                TransitionToState(AttackState.Recovery);
                npc.ai[3] = 0f;
            }
        }

        private void State_Recovery() {
            stateTimer++;
            npc.damage = 0;

            //减速旋转
            targetSpinSpeed = 0.1f;

            //快速返回
            float acceleration = CalculateAcceleration(bossRush, death, masterMode, cannonAlive, laserAlive, viceAlive);
            float accelerationMult = 1f;

            if (!cannonAlive) {
                acceleration += 0.025f;
                accelerationMult += 0.5f;
            }
            if (!laserAlive) {
                acceleration += 0.025f;
                accelerationMult += 0.5f;
            }
            if (!viceAlive) {
                acceleration += 0.025f;
            }

            if (masterMode) {
                acceleration *= accelerationMult;
            }

            float topVelocity = acceleration * 100f;
            float deceleration = masterMode ? 0.6f : 0.8f;

            AdjustVelocityY(head, acceleration, topVelocity, deceleration);
            AdjustVelocityX(head, acceleration, topVelocity, deceleration);

            //更新旋转
            Vector2 toHead = head.Center - npc.Center;
            npc.rotation = MathHelper.Lerp(npc.rotation, toHead.ToRotation() + MathHelper.PiOver2, 0.08f);
        }
        #endregion

        #region 辅助函数
        private void TransitionToState(AttackState newState) {
            npc.ai[2] = (float)newState;
            stateTimer = 0;
            hasPlayedSpinSound = false;
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

        private void UpdateSpinAnimation() {
            //平滑插值到目标旋转速度
            spinSpeed = MathHelper.Lerp(spinSpeed, targetSpinSpeed, 0.08f);

            //旋转音效
            if (spinSpeed > 0.6f && stateTimer % 40 == 0) {
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.4f, Pitch = spinSpeed * 0.5f }, npc.Center);
            }
        }

        private void UpdateAnimation() {
            if (Main.GameUpdateCount % 5 == 0) {
                if (++frame > 1) {
                    frame = 0;
                }
            }
        }

        private void SpawnSpinUpParticles() {
            Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(35, 35);
            Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                0, 0, 100, Color.Yellow * 0.8f, Main.rand.NextFloat(0.8f, 1.3f));
            dust.velocity = (npc.Center - particlePos).RotatedBy(MathHelper.PiOver2) * 0.15f;
            dust.noGravity = true;
        }

        private void SpawnDashTrail() {
            Vector2 trailPos = npc.Center - velocity.SafeNormalize(Vector2.Zero) * 40f;
            Dust dust = Dust.NewDustDirect(trailPos, 1, 1, DustID.FireworkFountain_Red,
                -velocity.X * 0.2f, -velocity.Y * 0.2f, 100, Color.Cyan, Main.rand.NextFloat(1.2f, 2.0f));
            dust.noGravity = true;
            dust.fadeIn = 1.1f;
        }

        private void SpawnOrbitParticles() {
            Vector2 particleVel = velocity.RotatedBy(MathHelper.PiOver2) * 0.3f;
            Dust dust = Dust.NewDustDirect(npc.Center, npc.width, npc.height, DustID.SteampunkSteam,
                particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(1.0f, 1.6f));
            dust.noGravity = true;
        }

        private void SpawnDrillParticles() {
            for (int i = 0; i < 2; i++) {
                Vector2 particlePos = npc.Center + Main.rand.NextVector2Circular(25, 25);
                Vector2 particleVel = velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, DustID.FireworkFountain_Red,
                    particleVel.X, particleVel.Y, 100, Color.OrangeRed, Main.rand.NextFloat(1.1f, 1.7f));
                dust.noGravity = true;
            }
        }

        private float CalculateChargeRate() {
            float baseRate = masterMode ? 2f : 1f;
            if (death) baseRate *= 2f;
            if (!cannonAlive) baseRate += 0.5f;
            if (!laserAlive) baseRate += 0.5f;
            if (!viceAlive) baseRate += 0.5f;
            return baseRate;
        }

        private float CalculateDashSpeed() {
            float baseSpeed = bossRush ? 27.5f : 22f;
            if (!cannonAlive) baseSpeed += 2f;
            if (!laserAlive) baseSpeed += 2f;
            if (!viceAlive) baseSpeed += 2f;
            if (death) baseSpeed *= 1.2f;
            return baseSpeed;
        }

        private float CalculateDrillSpeed() {
            float baseSpeed = bossRush ? 13.5f : 11f;
            if (!cannonAlive) baseSpeed += 1.5f;
            if (!laserAlive) baseSpeed += 1.5f;
            if (!viceAlive) baseSpeed += 1.5f;
            if (masterMode) baseSpeed *= 1.25f;
            return baseSpeed;
        }

        private int CalculateMaxDashes() {
            int maxDashes = 3;
            if (!cannonAlive) maxDashes++;
            if (!laserAlive) maxDashes++;
            if (!viceAlive) maxDashes++;
            if (death) maxDashes += 2;
            return maxDashes;
        }

        private float CalculateAcceleration(bool bossRush, bool death, bool masterMode, bool cannonAlive, bool laserAlive, bool viceAlive) {
            float acceleration = bossRush ? 0.6f : (death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f));
            if (!cannonAlive) acceleration += 0.02f;
            if (!laserAlive) acceleration += 0.02f;
            if (!viceAlive) acceleration += 0.02f;
            return acceleration;
        }

        private void AdjustVelocityY(NPC head, float acceleration, float topVelocity, float deceleration) {
            if (npc.position.Y > head.position.Y + 20f) {
                if (npc.velocity.Y > 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < head.position.Y - 20f) {
                if (npc.velocity.Y < 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) npc.velocity.Y = -topVelocity;
            }
        }

        private void AdjustVelocityX(NPC head, float acceleration, float topVelocity, float deceleration) {
            if (npc.Center.X > head.Center.X + 20f) {
                if (npc.velocity.X > 0f) npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration * 2f;
                if (npc.velocity.X > topVelocity) npc.velocity.X = topVelocity;
            }
            else if (npc.Center.X < head.Center.X - 20f) {
                if (npc.velocity.X < 0f) npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration * 2f;
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
            Texture2D mainValue = HeadPrimeAI.BSPSAW.Value;
            Texture2D mainValue2 = HeadPrimeAI.BSPSAWGlow.Value;
            float drawRot = npc.rotation;
            //添加旋转拖尾效果
            if (spinSpeed > 0.4f) {
                for (int i = 0; i < 3; i++) {
                    float trailRot = drawRot - (i + 1) * spinSpeed * 0.3f;
                    Color trailColor = drawColor * (0.3f - i * 0.1f);

                    Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, mainValue.GetRectangle(frame, 2),
                        trailColor, trailRot, VaultUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, mainValue.GetRectangle(frame, 2),
                drawColor, drawRot, VaultUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, mainValue.GetRectangle(frame, 2),
                Color.White * (0.8f + spinSpeed * 0.2f), drawRot, VaultUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
        #endregion
    }
}
