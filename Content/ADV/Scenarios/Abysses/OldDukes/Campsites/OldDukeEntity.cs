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

        public float Sengs;

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

        //避障相关
        private const float TileDetectionRadius = 64f;
        private const float TileAvoidanceForce = 2.5f;

        //露天倾向相关
        private const int OpenSkyCheckHeight = 10;
        private const float OpenSkyPreference = 40f;
        private int stuckInCaveTimer;
        private const int MaxStuckTime = 180;
        private const float EscapeUpwardForce = 0.8f;
        private bool isEscapingCave;

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
            stuckInCaveTimer = 0;
            isEscapingCave = false;
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

            UpdateWannaToFight();
            ApplyVelocity();
            ConstrainPosition();
            UpdateFacing();
        }

        private void UpdateWannaToFight() {
            if (OldDukeCampsite.WannaToFight) {
                if (Sengs > 0f) {
                    Sengs -= 0.1f;
                    bool playerNearby = false;
                    Player player = Position.FindClosestPlayer();
                    if (player is not null && player.To(Position).Length() < 1200) {
                        playerNearby = true;
                    }
                    if (playerNearby && NPC.FindFirstNPC(CWRID.NPC_OldDuke).TryGetNPC(out var npc)) {
                        npc.Center = Position;
                    }
                }
            }
            else {
                if (Sengs < 1f) {
                    Sengs += 0.1f;
                }
            }
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

            stuckInCaveTimer = 0;
            isEscapingCave = false;
        }

        /// <summary>
        /// 正常模式行为
        /// </summary>
        private void UpdateNormalBehavior() {
            targetTimer++;
            potVisitCooldown--;

            CheckAndHandleCaveStuck();

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
        /// 检查并处理洞穴卡住情况
        /// </summary>
        private void CheckAndHandleCaveStuck() {
            bool isInCave = IsInCave();
            bool isBelowCenter = Position.Y > campsiteCenter.Y;

            if (isInCave && isBelowCenter) {
                stuckInCaveTimer++;
                if (stuckInCaveTimer > MaxStuckTime) {
                    isEscapingCave = true;
                }
            }
            else {
                stuckInCaveTimer = Math.Max(0, stuckInCaveTimer - 2);
                if (stuckInCaveTimer == 0) {
                    isEscapingCave = false;
                }
            }

            if (isEscapingCave) {
                ApplyEscapeForce();
            }
        }

        /// <summary>
        /// 应用逃离洞穴的上浮力
        /// </summary>
        private void ApplyEscapeForce() {
            float escapeIntensity = MathHelper.Clamp((stuckInCaveTimer - MaxStuckTime) / 120f, 0f, 1f);
            Vector2 upwardForce = new Vector2(0, -EscapeUpwardForce * escapeIntensity);

            Vector2 toCenter = campsiteCenter - Position;
            float horizontalForce = toCenter.X * 0.02f;
            upwardForce.X += horizontalForce;

            Velocity += upwardForce;

            if (Velocity.Length() > MaxSpeed * 1.5f) {
                Velocity = Velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * 1.5f;
            }

            if (Main.rand.NextBool(30)) {
                FindOpenSkyTarget();
            }
        }

        /// <summary>
        /// 寻找露天目标点
        /// </summary>
        private void FindOpenSkyTarget() {
            for (int attempt = 0; attempt < 15; attempt++) {
                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.5f, 0.5f);
                float distance = Main.rand.NextFloat(100f, 300f);
                Vector2 candidateTarget = Position + angle.ToRotationVector2() * distance;

                if (IsOpenSky(candidateTarget) && Vector2.Distance(candidateTarget, campsiteCenter) < WanderRadius) {
                    currentTarget = candidateTarget;
                    currentState = BehaviorState.Wander;
                    targetTimer = 0;
                    break;
                }
            }
        }

        /// <summary>
        /// 检测位置是否在洞穴中
        /// </summary>
        private bool IsInCave() {
            return !IsOpenSky(Position);
        }

        /// <summary>
        /// 检测位置上方是否露天
        /// </summary>
        private bool IsOpenSky(Vector2 position) {
            Point tilePos = position.ToTileCoordinates();

            for (int y = 0; y < OpenSkyCheckHeight; y++) {
                int checkY = tilePos.Y - y;
                if (!WorldGen.InWorld(tilePos.X, checkY)) {
                    continue;
                }

                Tile tile = Framing.GetTileSafely(tilePos.X, checkY);
                if (tile.HasSolidTile()) {
                    return false;
                }
            }

            return true;
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

            if (!isEscapingCave) {
                Vector2 driftOffset = new Vector2(
                    MathF.Sin(SwimPhase * 0.6f) * 0.5f,
                    MathF.Cos(SwimPhase * 0.4f) * 0.4f
                );
                Velocity += driftOffset * 0.1f;
            }

            if (idleTimer <= 0) {
                if (potPositions.Count > 0 && potVisitCooldown <= 0 && Main.rand.NextBool(3) && !isEscapingCave) {
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
            if (isEscapingCave) {
                desiredSpeed *= 1.3f;
            }

            Vector2 desiredVelocity = direction * desiredSpeed;
            Velocity = Vector2.Lerp(Velocity, desiredVelocity, 0.08f);

            if (Velocity.Length() > MaxSpeed) {
                Velocity = Velocity.UnitVector() * MaxSpeed;
            }

            if (!isEscapingCave) {
                Vector2 swimWave = new Vector2(
                    MathF.Sin(SwimPhase * 1.2f) * 0.01f,
                    MathF.Cos(SwimPhase * 0.8f) * 0.02f
                );
                Velocity += swimWave;
            }

            ApplyTileAvoidance();
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

                ApplyTileAvoidance();
            }
        }

        /// <summary>
        /// 应用图块躲避力
        /// </summary>
        private void ApplyTileAvoidance() {
            Vector2 avoidanceForce = GetTileAvoidanceForce();
            if (avoidanceForce.Length() > 0.1f) {
                Velocity += avoidanceForce;
                if (Velocity.Length() > MaxSpeed) {
                    Velocity = Velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
                }
            }
        }

        /// <summary>
        /// 计算图块躲避力
        /// </summary>
        private Vector2 GetTileAvoidanceForce() {
            Vector2 totalForce = Vector2.Zero;
            int checkRadius = (int)(TileDetectionRadius / 16f);
            Point tileCenter = Position.ToTileCoordinates();

            for (int x = -checkRadius; x <= checkRadius; x++) {
                for (int y = -checkRadius; y <= checkRadius; y++) {
                    int checkX = tileCenter.X + x;
                    int checkY = tileCenter.Y + y;

                    if (!WorldGen.InWorld(checkX, checkY)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (!tile.HasSolidTile()) {
                        continue;
                    }

                    Vector2 tileWorldPos = new Vector2(checkX * 16 + 8, checkY * 16 + 8);
                    Vector2 toTile = tileWorldPos - Position;
                    float distance = toTile.Length();

                    if (distance < TileDetectionRadius && distance > 0) {
                        float forceMagnitude = (1f - distance / TileDetectionRadius) * TileAvoidanceForce;
                        Vector2 repelForce = -toTile.SafeNormalize(Vector2.Zero) * forceMagnitude;
                        totalForce += repelForce;
                    }
                }
            }

            return totalForce;
        }

        /// <summary>
        /// 选择新的游走目标
        /// </summary>
        private void SelectNewTarget() {
            const int maxAttempts = 15;
            Vector2 bestTarget = Position;
            float bestScore = float.MinValue;

            bool needOpenSky = isEscapingCave || (IsInCave() && Position.Y > campsiteCenter.Y);

            for (int attempt = 0; attempt < maxAttempts; attempt++) {
                Vector2 candidateTarget;

                if (needOpenSky) {
                    float angle = Main.rand.NextFloat(-MathHelper.PiOver2 - 0.8f, -MathHelper.PiOver2 + 0.8f);
                    float distance = Main.rand.NextFloat(WanderRadius * 0.5f, WanderRadius * 0.9f);
                    candidateTarget = Position + angle.ToRotationVector2() * distance;
                }
                else {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(WanderRadius * 0.4f, WanderRadius * 0.9f);
                    candidateTarget = campsiteCenter + angle.ToRotationVector2() * distance;
                    candidateTarget.Y -= Main.rand.NextFloat(40f, 100f);
                }

                float score = EvaluateTargetPosition(candidateTarget, needOpenSky);
                if (score > bestScore) {
                    bestScore = score;
                    bestTarget = candidateTarget;
                }
            }

            currentTarget = bestTarget;
        }

        /// <summary>
        /// 评估目标位置的适合度
        /// </summary>
        private float EvaluateTargetPosition(Vector2 targetPos, bool preferOpenSky = false) {
            float score = 100f;
            Point tilePosi = targetPos.ToTileCoordinates();
            int checkRadius = 3;

            int solidTileCount = 0;
            for (int x = -checkRadius; x <= checkRadius; x++) {
                for (int y = -checkRadius; y <= checkRadius; y++) {
                    int checkX = tilePosi.X + x;
                    int checkY = tilePosi.Y + y;

                    if (!WorldGen.InWorld(checkX, checkY)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasSolidTile()) {
                        solidTileCount++;
                    }
                }
            }

            int totalTiles = (checkRadius * 2 + 1) * (checkRadius * 2 + 1);
            float solidRatio = solidTileCount / (float)totalTiles;
            score -= solidRatio * 80f;

            float distanceToCenter = Vector2.Distance(targetPos, campsiteCenter);
            float distanceScore = MathHelper.Clamp(1f - distanceToCenter / WanderRadius, 0f, 1f) * 20f;
            score += distanceScore;

            if (IsOpenSky(targetPos)) {
                score += OpenSkyPreference;
                if (preferOpenSky) {
                    score += 60f;
                }
            }
            else {
                if (preferOpenSky) {
                    score -= 100f;
                }
            }

            if (targetPos.Y < campsiteCenter.Y) {
                score += 15f;
            }
            else if (IsInCave()) {
                score -= 25f;
            }

            return score;
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

        /// <summary>
        /// 判断是否正在访问锅
        /// </summary>
        public bool IsVisitingPot() => currentState == BehaviorState.VisitPot;

        /// <summary>
        /// 获取当前目标位置
        /// </summary>
        public Vector2 GetCurrentTarget() => currentTarget;
    }
}
