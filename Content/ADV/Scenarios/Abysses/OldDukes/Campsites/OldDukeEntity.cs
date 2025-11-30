using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵实体类
    /// 负责管理老公爵的AI行为和状态
    /// </summary>
    internal class OldDukeEntity
    {
        //位置与速度
        public Vector2 Position;
        public Vector2 Velocity;

        //朝向
        public bool FacingLeft;

        //动画相位
        public float SwimPhase;

        //移动目标
        private Vector2 currentTarget;
        private int targetTimer;
        private BehaviorState currentState;

        //行为常量
        private const float WanderRadius = 420f;
        private const float MoveSpeed = 1.6f;
        private const float MaxSpeed = 3.2f;
        private const float PotApproachDistance = 80f;
        private const float IdleRadius = 280f;

        //行为权重
        private int idleTimer;
        private int potVisitCooldown;
        private const int MinIdleTime = 180;
        private const int MaxIdleTime = 420;
        private const int PotVisitInterval = 600;

        //营地中心缓存
        private Vector2 campsiteCenter;

        //可访问的锅列表
        private List<Vector2> potPositions = [];

        public enum BehaviorState
        {
            Idle,
            Wander,
            VisitPot,
            Dialogue
        }

        public OldDukeEntity(Vector2 startPosition) {
            Position = startPosition;
            Velocity = Vector2.Zero;
            campsiteCenter = startPosition;
            SwimPhase = 0f;
            FacingLeft = Main.rand.NextBool();
            currentState = BehaviorState.Idle;
            idleTimer = Main.rand.Next(MinIdleTime, MaxIdleTime);
            SelectNewTarget();
        }

        /// <summary>
        /// 设置可访问的锅位置列表
        /// </summary>
        public void SetPotPositions(List<Vector2> positions) {
            potPositions.Clear();
            if (positions != null) {
                potPositions.AddRange(positions);
            }
        }

        /// <summary>
        /// 更新老公爵AI
        /// </summary>
        public void Update(bool inDialogue, Vector2 dialogueTarget) {
            SwimPhase += 0.08f;
            if (SwimPhase > MathHelper.TwoPi) {
                SwimPhase -= MathHelper.TwoPi;
            }

            if (inDialogue) {
                UpdateDialogueBehavior(dialogueTarget);
            }
            else {
                UpdateNormalBehavior();
            }

            ApplyVelocity();
            ConstrainPosition();
            UpdateFacing();
        }

        /// <summary>
        /// 对话模式行为
        /// </summary>
        private void UpdateDialogueBehavior(Vector2 dialogueTarget) {
            currentState = BehaviorState.Dialogue;
            currentTarget = dialogueTarget;

            Vector2 toTarget = currentTarget - Position;
            float distance = toTarget.Length();

            if (distance > 5f) {
                Vector2 direction = toTarget.SafeNormalize(Vector2.Zero);
                float approachSpeed = MathHelper.Clamp(distance * 0.08f, 0.5f, 3.5f);
                Velocity = Vector2.Lerp(Velocity, direction * approachSpeed, 0.15f);

                if (Velocity.Length() > MaxSpeed * 1.2f) {
                    Velocity = Velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * 1.2f;
                }
            }
            else {
                Velocity *= 0.88f;
                Vector2 floatOffset = new Vector2(
                    MathF.Sin(SwimPhase) * 0.3f,
                    MathF.Cos(SwimPhase * 0.7f) * 0.2f
                );
                Position += floatOffset;
            }
        }

        /// <summary>
        /// 正常模式行为
        /// </summary>
        private void UpdateNormalBehavior() {
            targetTimer++;
            potVisitCooldown--;
            switch (currentState) {
                case BehaviorState.Idle:
                    UpdateIdleBehavior();
                    break;
                case BehaviorState.Wander:
                    UpdateWanderBehavior();
                    break;
                case BehaviorState.VisitPot:
                    UpdateVisitPotBehavior();
                    break;
                case BehaviorState.Dialogue:
                    if (!OldDukeEffect.IsActive) {
                        currentState = BehaviorState.Idle;
                    }
                    break;
            }
        }

        /// <summary>
        /// 闲置行为
        /// </summary>
        private void UpdateIdleBehavior() {
            idleTimer--;

            Vector2 toCenter = campsiteCenter - Position;
            float distanceToCenter = toCenter.Length();

            if (distanceToCenter > IdleRadius) {
                Vector2 direction = toCenter.SafeNormalize(Vector2.Zero);
                Velocity += direction * 0.15f;
            }
            else {
                Velocity *= 0.94f;
            }

            Vector2 driftOffset = new Vector2(
                MathF.Sin(SwimPhase * 0.6f) * 0.5f,
                MathF.Cos(SwimPhase * 0.4f) * 0.4f
            );
            Velocity += driftOffset * 0.1f;

            if (idleTimer <= 0) {
                if (potPositions.Count > 0 && potVisitCooldown <= 0 && Main.rand.NextBool(3)) {
                    currentState = BehaviorState.VisitPot;
                    SelectRandomPot();
                    potVisitCooldown = PotVisitInterval;
                }
                else {
                    currentState = BehaviorState.Wander;
                    SelectNewTarget();
                }
                targetTimer = 0;
            }
        }

        /// <summary>
        /// 游走行为
        /// </summary>
        private void UpdateWanderBehavior() {
            Vector2 toTarget = currentTarget - Position;
            float distanceToTarget = toTarget.Length();

            if (distanceToTarget < 40f || targetTimer > 240) {
                currentState = BehaviorState.Idle;
                idleTimer = Main.rand.Next(MinIdleTime, MaxIdleTime);
                targetTimer = 0;
                return;
            }

            Vector2 direction = toTarget.UnitVector();
            float desiredSpeed = MoveSpeed;
            Vector2 desiredVelocity = direction * desiredSpeed;

            Velocity = Vector2.Lerp(Velocity, desiredVelocity, 0.08f);

            if (Velocity.Length() > MaxSpeed) {
                Velocity = Velocity.UnitVector() * MaxSpeed;
            }

            Vector2 swimWave = new Vector2(
                MathF.Sin(SwimPhase * 1.2f) * 0.01f,
                MathF.Cos(SwimPhase * 0.8f) * 0.02f
            );
            Velocity += swimWave;
        }

        /// <summary>
        /// 访问锅的行为
        /// </summary>
        private void UpdateVisitPotBehavior() {
            Vector2 toTarget = currentTarget - Position;
            float distanceToTarget = toTarget.Length();

            if (distanceToTarget < PotApproachDistance) {
                Velocity *= 0.92f;

                Vector2 hoverOffset = new Vector2(
                    MathF.Sin(SwimPhase * 0.8f) * 1.2f,
                    MathF.Cos(SwimPhase * 0.6f) * 0.8f
                );
                Position += hoverOffset * 0.15f;

                if (targetTimer > 180) {
                    currentState = BehaviorState.Idle;
                    idleTimer = Main.rand.Next(MinIdleTime / 2, MaxIdleTime / 2);
                    targetTimer = 0;
                }
            }
            else if (targetTimer > 300) {
                currentState = BehaviorState.Wander;
                SelectNewTarget();
                targetTimer = 0;
            }
            else {
                Vector2 direction = toTarget.SafeNormalize(Vector2.Zero);
                float approachSpeed = MathHelper.Lerp(MoveSpeed * 1.3f, MoveSpeed * 0.6f,
                    MathHelper.Clamp(distanceToTarget / 200f, 0f, 1f));

                Vector2 desiredVelocity = direction * approachSpeed;
                Velocity = Vector2.Lerp(Velocity, desiredVelocity, 0.12f);

                if (Velocity.Length() > MaxSpeed * 1.1f) {
                    Velocity = Velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * 1.1f;
                }

                Vector2 swimWave = new Vector2(
                    MathF.Sin(SwimPhase * 1.5f) * 0.03f,
                    MathF.Cos(SwimPhase) * 0.02f
                );
                Velocity += swimWave;
            }
        }

        /// <summary>
        /// 选择新的游走目标
        /// </summary>
        private void SelectNewTarget() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(WanderRadius * 0.4f, WanderRadius * 0.9f);

            currentTarget = campsiteCenter + angle.ToRotationVector2() * distance;
            currentTarget.Y -= Main.rand.NextFloat(40f, 100f);
        }

        /// <summary>
        /// 选择随机锅作为目标
        /// </summary>
        private void SelectRandomPot() {
            if (potPositions.Count == 0) {
                SelectNewTarget();
                currentState = BehaviorState.Wander;
                return;
            }

            Vector2 selectedPot = potPositions[Main.rand.Next(potPositions.Count)];
            currentTarget = selectedPot + new Vector2(0, -60f + Main.rand.NextFloat(-20f, 20f));
        }

        /// <summary>
        /// 应用速度并添加阻力
        /// </summary>
        private void ApplyVelocity() {
            Position += Velocity;
            Velocity *= 0.96f;
        }

        /// <summary>
        /// 限制位置在营地范围内
        /// </summary>
        private void ConstrainPosition() {
            Vector2 toCampsite = Position - campsiteCenter;
            float distanceFromCenter = toCampsite.Length();

            if (distanceFromCenter > WanderRadius) {
                Vector2 pushBack = toCampsite.SafeNormalize(Vector2.Zero) * (distanceFromCenter - WanderRadius);
                Position -= pushBack * 0.2f;

                if (currentState != BehaviorState.Dialogue && distanceFromCenter > WanderRadius * 1.2f) {
                    currentTarget = campsiteCenter + Main.rand.NextVector2Circular(WanderRadius * 0.5f, WanderRadius * 0.5f);
                    currentState = BehaviorState.Wander;
                }
            }
        }

        /// <summary>
        /// 更新朝向
        /// </summary>
        private void UpdateFacing() {
            if (Math.Abs(Velocity.X) > 0.2f) {
                FacingLeft = Velocity.X < 0;
            }
        }

        /// <summary>
        /// 获取当前行为状态
        /// </summary>
        public BehaviorState GetCurrentState() => currentState;

        /// <summary>
        /// 获取游泳倾斜角度
        /// </summary>
        public float GetSwimTilt() {
            if (currentState == BehaviorState.Dialogue) {
                return 0f;
            }

            if (Velocity.Length() > 0.5f) {
                return MathHelper.Clamp(Velocity.Y * 0.08f, -0.15f, 0.15f);
            }

            return 0f;
        }

        /// <summary>
        /// 获取游泳波动偏移
        /// </summary>
        public Vector2 GetSwimBobOffset() {
            float swimBob = MathF.Sin(SwimPhase) * 3f;
            return new Vector2(0, swimBob);
        }
    }
}
