using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>
    /// 老公爵营地里游荡的观赏性实体：位置/移动由服务端(或单人)权威决策，
    /// 客户端仅做视觉预测与外观绘制，避免多人下各端各走各的
    /// </summary>
    internal class OldDukeWanderingActor : Actor, ILocalizedModType
    {
        private enum BehaviorState
        {
            Idle,
            Wander,
            VisitPot,
            Dialogue
        }

        public string LocalizationCategory => "ADV.OldDukeCampsite";
        public static LocalizedText InteractHint;

        //朝向与动画相位，纯视觉，两端各自计算即可
        public bool FacingLeft;
        public float SwimPhase;
        /// <summary>
        /// 切磋开始时的淡出透明度，由 <see cref="OldDukeCampsite.WannaToFight"/>(已联机同步)驱动，两端各自计算保持一致
        /// </summary>
        public float Sengs = 1f;

        private float glowTimer;
        private int bubbleSpawnTimer;

        //以下字段只在服务端/单人下由权威行为逻辑读写，客户端不参与决策，只依赖Actor自带的位置同步+插值
        private Vector2 campsiteCenter;
        private Vector2 currentTarget;
        private int targetTimer;
        private BehaviorState currentState;
        private int idleTimer;
        private int potVisitCooldown;
        private int stuckInCaveTimer;
        private bool isEscapingCave;

        private const float WanderRadius = 420f;
        private const float MoveSpeed = 1.6f;
        private const float MaxSpeed = 3.2f;
        private const float PotApproachDistance = 80f;
        private const float IdleRadius = 280f;
        private const int MinIdleTime = 180;
        private const int MaxIdleTime = 420;
        private const int PotVisitInterval = 600;
        private const float TileDetectionRadius = 64f;
        private const float TileAvoidanceForce = 2.5f;
        private const int OpenSkyCheckHeight = 10;
        private const float OpenSkyPreference = 40f;
        private const int MaxStuckTime = 180;
        private const float EscapeUpwardForce = 0.8f;

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 对话");
        }

        public override void OnSpawn(params object[] args) {
            Width = 100;
            Height = 120;
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.Default;

            //生成点即营地中心，两端都能从已同步的Position直接推出，不需要额外联机字段
            campsiteCenter = Position;
            currentTarget = Position;
            FacingLeft = Main.rand.NextBool();
            currentState = BehaviorState.Idle;
            idleTimer = Main.rand.Next(MinIdleTime, MaxIdleTime);
            SwimPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            glowTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            Sengs = 1f;
            stuckInCaveTimer = 0;
            isEscapingCave = false;
        }

        public override void AI() {
            SwimPhase += 0.08f;
            if (SwimPhase > MathHelper.TwoPi) {
                SwimPhase -= MathHelper.TwoPi;
            }
            glowTimer += 0.03f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            //Sengs的渐隐渐显由已联机同步的WannaToFight驱动，两端跑同一份确定性逻辑即可保持画面一致
            UpdateWannaToFight();

            //真正"决定去哪/做什么"的逻辑只由权威端跑，客户端只吃Actor自带的Position/Velocity同步+插值
            if (VaultUtils.isServer || VaultUtils.isSinglePlayer) {
                RunAuthorityBehavior();
            }

            //朝向只是对当前速度的派生展示，两端各自算一份即可，不需要额外联机字段
            UpdateFacing();
        }

        private void RunAuthorityBehavior() {
            //等价于原先"先用速度推进位置、再对速度做阻尼"的顺序：Actor框架会在AI()返回后统一做Position += Velocity，
            //这里提前对上一tick末的速度做阻尼，效果与原实现逐帧对齐
            Velocity *= 0.96f;

            if (OldDukeEffect.IsActive) {
                UpdateDialogueBehavior();
            }
            else {
                UpdateNormalBehavior();
            }

            UpdateNearbyPots();
            ConstrainPosition();
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

        private void UpdateDialogueBehavior() {
            currentState = BehaviorState.Dialogue;

            //营地对话没有固定的"服务端视角"，取离营地最近的玩家作为面向目标，两端读到的都是已同步的玩家位置
            Player target = Position.FindClosestPlayer();
            currentTarget = target is not null ? target.Center + new Vector2(0, -200f) : Position;

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
                    currentState = BehaviorState.Idle;
                    break;
            }
        }

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

        private void ApplyEscapeForce() {
            float escapeIntensity = MathHelper.Clamp((stuckInCaveTimer - MaxStuckTime) / 120f, 0f, 1f);
            Vector2 upwardForce = new Vector2(0, -EscapeUpwardForce * escapeIntensity);

            Vector2 toCenter = campsiteCenter - Position;
            upwardForce.X += toCenter.X * 0.02f;

            Velocity += upwardForce;

            if (Velocity.Length() > MaxSpeed * 1.5f) {
                Velocity = Velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * 1.5f;
            }

            if (Main.rand.NextBool(30)) {
                FindOpenSkyTarget();
            }
        }

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

        private bool IsInCave() => !IsOpenSky(Position);

        private static bool IsOpenSky(Vector2 position) {
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
                List<CampsitePotActor> pots = ActorLoader.GetActiveActors<CampsitePotActor>();
                if (pots.Count > 0 && potVisitCooldown <= 0 && Main.rand.NextBool(3) && !isEscapingCave) {
                    currentState = BehaviorState.VisitPot;
                    SelectRandomPot(pots);
                    potVisitCooldown = PotVisitInterval;
                }
                else {
                    currentState = BehaviorState.Wander;
                    SelectNewTarget();
                }
                targetTimer = 0;
            }
        }

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

        private void ApplyTileAvoidance() {
            Vector2 avoidanceForce = GetTileAvoidanceForce();
            if (avoidanceForce.Length() > 0.1f) {
                Velocity += avoidanceForce;
                if (Velocity.Length() > MaxSpeed) {
                    Velocity = Velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
                }
            }
        }

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
            else if (preferOpenSky) {
                score -= 100f;
            }

            if (targetPos.Y < campsiteCenter.Y) {
                score += 15f;
            }
            else if (IsInCave()) {
                score -= 25f;
            }

            return score;
        }

        private void SelectRandomPot(List<CampsitePotActor> pots) {
            if (pots.Count == 0) {
                SelectNewTarget();
                currentState = BehaviorState.Wander;
                return;
            }

            CampsitePotActor selected = pots[Main.rand.Next(pots.Count)];
            currentTarget = selected.Position + new Vector2(0, -60f + Main.rand.NextFloat(-20f, 20f));
        }

        /// <summary>
        /// 更新附近锅的"被访问"表现状态；这两个字段标了[SyncVar]，只要权威端写入就会自动广播给客户端
        /// </summary>
        private void UpdateNearbyPots() {
            bool isVisiting = currentState == BehaviorState.VisitPot;
            List<CampsitePotActor> pots = ActorLoader.GetActiveActors<CampsitePotActor>();

            foreach (CampsitePotActor pot in pots) {
                float distance = Vector2.Distance(pot.Position, Position);
                float targetDistance = Vector2.Distance(pot.Position, currentTarget);

                if (isVisiting && targetDistance < 100f && distance < 150f) {
                    pot.IsBeingVisited = true;
                    float distanceFactor = 1f - MathHelper.Clamp(distance / 150f, 0f, 1f);
                    pot.InteractionIntensity = MathHelper.Lerp(pot.InteractionIntensity, distanceFactor, 0.1f);
                }
                else {
                    pot.IsBeingVisited = false;
                }
            }
        }

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

        private void UpdateFacing() {
            if (Math.Abs(Velocity.X) > 0.2f) {
                FacingLeft = Velocity.X < 0;
            }
        }

        private float GetSwimTilt() {
            if (Velocity.Length() > 0.5f) {
                return MathHelper.Clamp(Velocity.Y * 0.08f, -0.15f, 0.15f);
            }
            return 0f;
        }

        private Vector2 GetSwimBobOffset() {
            float swimBob = MathF.Sin(SwimPhase) * 3f;
            return new Vector2(0, swimBob);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (OldDukeCampsite.OldDuke == null) {
                return false;
            }

            Rectangle frame = OldDukeCampsite.GetCurrentFrame();
            Vector2 origin = frame.Size() / 2f;
            Vector2 screenPos = Position - Main.screenPosition;

            float breathScale = 1f + MathF.Sin(glowTimer * 1.5f) * 0.01f;
            Vector2 bobOffset = GetSwimBobOffset();
            float swimTilt = GetSwimTilt();
            SpriteEffects flip = FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //硫磺海风格底层发光
            float glowIntensity = (MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f) * 0.4f;
            Color glowColor = new Color(100, 200, 120) with { A = 0 };

            for (int i = 0; i < 3; i++) {
                float glowScale = breathScale * (1.2f + i * 0.1f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                spriteBatch.Draw(OldDukeCampsite.OldDuke, screenPos + bobOffset, frame,
                    glowColor * glowAlpha * Sengs, swimTilt, origin, glowScale, flip, 0f);
            }

            spriteBatch.Draw(OldDukeCampsite.OldDuke, screenPos + bobOffset, frame,
                Lighting.GetColor((Position / 16).ToPoint()) * Sengs, swimTilt, origin, breathScale, flip, 0f);

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            DrawInteractPrompt(spriteBatch);
        }

        /// <summary>
        /// 交互提示：用柔光衬底+描边文字取代实心方框，呼应项目"拒绝方框UI"的规范
        /// </summary>
        private void DrawInteractPrompt(SpriteBatch sb) {
            float alpha = OldDukeCampsite.GetInteractPromptAlpha();
            if (alpha <= 0.01f) {
                return;
            }

            Vector2 screenPos = Position - Main.screenPosition;
            Vector2 textPos = screenPos + new Vector2(0, -150);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hintText = InteractHint.Value;
            Vector2 textSize = font.MeasureString(hintText) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            //柔光椭圆衬底，取代实心矩形背景
            Vector2 backingScale = new Vector2((textSize.X + 50f) / glow.Width, (textSize.Y + 30f) / glow.Height);
            Color backingColor = new Color(90, 180, 130) with { A = 0 } * (alpha * (0.3f + pulse * 0.12f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            //文字
            Color textColor = new Color(200, 240, 220) * alpha;
            Utils.DrawBorderString(sb, hintText, textPos - textSize / 2, textColor, 0.9f);

            //脉动光带取代硬边框分隔线
            float lineWidth = textSize.X * (0.7f + pulse * 0.25f);
            Vector2 linePos = textPos + new Vector2(0, textSize.Y / 2f + 6f);
            Color lineColor = new Color(140, 220, 160) with { A = 0 } * (alpha * 0.6f);
            sb.Draw(glow, linePos, null, lineColor, 0f, glow.Size() / 2f, new Vector2(lineWidth / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);

            //脉动箭头图标
            string iconText = "▼";
            Vector2 iconSize = font.MeasureString(iconText) * 0.7f;
            Vector2 iconPos = textPos + new Vector2(0, textSize.Y / 2 + 16);
            Utils.DrawBorderString(sb, iconText, iconPos - iconSize / 2,
                new Color(150, 230, 180) * (alpha * pulse), 0.7f);
        }
    }
}
