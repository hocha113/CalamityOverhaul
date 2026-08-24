using CalamityOverhaul.Common;
using InnoVault.Actors;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Arbiters
{
    internal enum ArbiterManifestationPhase : byte
    {
        /// <summary>熔火聚形:斧身在肉山死处的空中锻成</summary>
        Forging,
        /// <summary>蓄势:锻成的斧悬停正身,热浪欲坠</summary>
        Poising,
        /// <summary>重坠:刃朝下加速砸向地面</summary>
        Falling,
        /// <summary>插地:斧嵌进地面燃烧,等待认领</summary>
        Planted,
    }

    internal readonly record struct ArbiterManifestationResumeState(
        Vector2 Position,
        Vector2 Velocity,
        ArbiterManifestationPhase Phase,
        int PhaseTimer);

    /// <summary>世界共享Actor,服务端推进阶段,各客户端独立拔斧(镜像 WGManifestationActor)</summary>
    internal sealed class ArbiterManifestationActor : Actor
    {
        internal const int ForgingFrames = 130;
        internal const int PoisingFrames = 30;
        internal const int MaximumFallingFrames = 1800;
        internal const int ManifestAftermathFrames = 34;
        internal const int PullChargeFrames = 12;
        internal const int PullDrawFrames = 14;
        internal const int PullFrames = 48;
        internal const int PullCutsceneFrames = 68;
        internal const float InteractDistance = 210f;

        /// <summary>插地姿态:刃朝下(纹理刃向 -π/4,朝下需 +3π/4)带一点斜倾</summary>
        private const float PlantRotation = MathHelper.Pi * 0.75f + 0.08f;
        private const float AxeScale = 1.0f;
        /// <summary>斧心相对 Position(刃尖底)的抬高</summary>
        internal const float AxeCenterHeight = 40f;
        //重斧坠落比魂剑更凶
        private const float FallingGravity = 1.5f;
        private const float MaximumFallingSpeed = 42f;
        private const float FallingSubstep = 8f;
        private const float EmbeddedFallingSubstep = 1f;
        private const int FallingProbeWidth = 8;
        private const int FallingProbeHeight = 8;
        private const int EmergencySearchCooldown = 300;
        /// <summary>落地火从撞击点扫向两侧的窗口(帧)</summary>
        private const int PlantFireSpreadFrames = 150;
        /// <summary>落地火蔓延半径(每侧 px)</summary>
        private const float PlantFireSpreadRadius = 240f;
        /// <summary>插地常驻闷烧半宽(px)</summary>
        private const float SmolderHalfWidth = 52f;

        private static bool createWithResumeState;
        private static ArbiterManifestationResumeState createResumeState;

        [SyncVar]
        private int phaseRaw = (int)ArbiterManifestationPhase.Forging;

        private ArbiterManifestationPhase lastSeenPhase;
        private int phaseTimer;
        private bool manifestationCutsceneStarted;
        private int manifestationCutsceneEndTimer;
        private float promptAlpha;
        private LocalPullState localPullState;
        private int localPullTimer;
        private int localRequestTimer;
        private int localClaimToken;
        private bool localCommitSent;
        private bool fallingCollisionArmed;
        private int emergencySearchCooldown;
        //本端亲眼看到 Falling→Planted 才播蔓延与余温(迟到端只见常驻闷烧)
        private bool plantWitnessed;
        private int plantedLocalTimer;

        private enum LocalPullState : byte
        {
            None,
            AwaitingPermit,
            Pulling,
            AwaitingResult,
            Hidden,
        }

        internal ArbiterManifestationPhase Phase
            => (ArbiterManifestationPhase)phaseRaw;

        internal bool IsPlanted => Phase == ArbiterManifestationPhase.Planted;

        internal int PhaseTimer => phaseTimer;

        internal Vector2 AxeAnchor => Position + new Vector2(0f, -AxeCenterHeight);

        internal Vector2 CameraFocusPoint => CurrentAxeCenter;

        internal bool IsLocalPullActive
            => localPullState is LocalPullState.Pulling or LocalPullState.AwaitingResult;

        public override Rectangle HitBox
            => new((int)Position.X - 70, (int)Position.Y - 160, 140, 160);

        public override Vector2 Center => AxeAnchor;

        internal static ArbiterManifestationResumeState CreateInitialState(Vector2 manifestationOrigin)
            => new(manifestationOrigin + new Vector2(0f, AxeCenterHeight), Vector2.Zero,
                ArbiterManifestationPhase.Forging, 0);

        internal static int CreateAt(Vector2 anchor, bool planted) {
            ArbiterManifestationResumeState state = new(anchor, Vector2.Zero,
                planted ? ArbiterManifestationPhase.Planted
                    : ArbiterManifestationPhase.Forging,
                planted ? 1 : 0);
            return CreateAt(state);
        }

        internal static int CreateAt(ArbiterManifestationResumeState state) {
            if (VaultUtils.isClient) {
                return -1;
            }

            createWithResumeState = true;
            createResumeState = state;
            try {
                return ActorLoader.NewActor<ArbiterManifestationActor>(state.Position, state.Velocity);
            } finally {
                createWithResumeState = false;
                createResumeState = default;
            }
        }

        public override void OnSpawn(params object[] args) {
            Width = 140;
            Height = 160;
            DrawExtendMode = 650;
            DrawLayer = ActorDrawLayer.AfterTiles;

            if (!VaultUtils.isClient && createWithResumeState) {
                phaseRaw = (int)createResumeState.Phase;
                phaseTimer = Math.Max(createResumeState.PhaseTimer, 0);
                Velocity = createResumeState.Velocity;
            }

            lastSeenPhase = Phase;
            if (!createWithResumeState && !VaultUtils.isClient) {
                phaseTimer = IsPlanted ? 1 : 0;
                Velocity = Vector2.Zero;
            }
            manifestationCutsceneStarted = false;
            manifestationCutsceneEndTimer = 0;
            promptAlpha = 0f;
            localPullState = LocalPullState.None;
            plantWitnessed = false;
            plantedLocalTimer = 0;
            fallingCollisionArmed = Phase != ArbiterManifestationPhase.Falling
                || !ProbeInsideSolid(Position);
        }

        public override void SendExtraData(BinaryWriter writer) {
            writer.Write(phaseTimer);
        }

        public override void ReceiveExtraData(BinaryReader reader) {
            phaseTimer = Math.Max(reader.ReadInt32(), 0);
            lastSeenPhase = Phase;
        }

        public override void AI() {
            ObservePhaseChange();
            if (!VaultUtils.isClient) {
                ArbiterManifestationSystem.CaptureResumeState(GetResumeState());
                UpdateAuthoritativeManifestation();
            }
            else if (Phase == ArbiterManifestationPhase.Falling && phaseTimer > 0) {
                UpdateClientFallingPrediction();
            }

            if (Main.dedServ) {
                return;
            }

            TryStartManifestationCutscene();
            UpdateManifestationCutscene();
            UpdateLocalInteraction();

            float progress = ManifestationProgress;
            bool impact = IsPlanted && phaseTimer == 0;
            bool visible = IsLocalPullActive || ShouldShowForLocalPlayer();
            if (visible) {
                ArbiterManifestationRenderer.UpdateAmbient(CurrentAxeCenter, Phase, progress, impact);
                UpdateGroundFireFeed();
                float light = IsPlanted ? 0.55f : MathHelper.Lerp(0.20f, 0.90f, progress);
                Lighting.AddLight(CurrentAxeCenter, new Vector3(1.00f, 0.48f, 0.16f) * light);
            }
            if (IsPlanted) {
                plantedLocalTimer++;
            }
            if (impact) {
                phaseTimer = 1;
            }
            else if (VaultUtils.isClient && !IsPlanted) {
                phaseTimer++;
            }
        }

        /// <summary>把落地蔓延火与常驻闷烧喂给狱火条带(纯视觉,无判定)</summary>
        private void UpdateGroundFireFeed() {
            if (!IsPlanted || !ArbiterFlameRenderer.ShaderReady) {
                return;
            }

            float groundY = Position.Y;
            //落地蔓延:亲历本端在窗口内从撞击点向两侧扫火,前锋压在燃沿上
            if (plantWitnessed && plantedLocalTimer <= PlantFireSpreadFrames) {
                float t = plantedLocalTimer / (float)PlantFireSpreadFrames;
                //EaseOutCubic 前快后缓
                float inv = 1f - t;
                float reach = (1f - inv * inv * inv) * PlantFireSpreadRadius;
                float decay = MathHelper.Lerp(1f, 0.38f, t);
                for (float dx = 0f; dx <= reach; dx += 20f) {
                    float local = reach <= 1f ? 0f : dx / PlantFireSpreadRadius;
                    float env = decay * (1f - local * 0.55f);
                    ArbiterFlameRenderer.PushPoint(Position.X + dx, groundY, env, 1.1f - local * 0.4f);
                    if (dx > 0f) {
                        ArbiterFlameRenderer.PushPoint(Position.X - dx, groundY, env, 1.1f - local * 0.4f);
                    }
                }
                float frontStrength = 1f - t;
                ArbiterFlameRenderer.PushFront(Position.X + reach, frontStrength);
                ArbiterFlameRenderer.PushFront(Position.X - reach, frontStrength);
                return;
            }

            //常驻闷烧:斧根一小片罪火,缓慢呼吸
            float pulse = 0.30f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Position.X * 0.01f);
            for (float dx = 0f; dx <= SmolderHalfWidth; dx += 18f) {
                float env = pulse * (1f - dx / (SmolderHalfWidth * 1.35f));
                ArbiterFlameRenderer.PushPoint(Position.X + dx, groundY, env, 0.85f);
                if (dx > 0f) {
                    ArbiterFlameRenderer.PushPoint(Position.X - dx, groundY, env, 0.85f);
                }
            }
        }

        private void ObservePhaseChange() {
            if (lastSeenPhase == Phase) {
                return;
            }

            ArbiterManifestationPhase previous = lastSeenPhase;
            lastSeenPhase = Phase;
            phaseTimer = 0;

            if (Phase == ArbiterManifestationPhase.Falling) {
                fallingCollisionArmed = !ProbeInsideSolid(Position);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.85f, Pitch = -0.35f }, Position);
                }
            }

            if (!Main.dedServ && Phase == ArbiterManifestationPhase.Planted) {
                if (VaultUtils.isClient && HasNetTarget) {
                    Position = NetTargetPosition;
                    Velocity = Vector2.Zero;
                }
                plantWitnessed = previous == ArbiterManifestationPhase.Falling;
                plantedLocalTimer = 0;
                manifestationCutsceneEndTimer = ManifestAftermathFrames;
                PlayPlantImpactFeedback();
            }
        }

        private void UpdateAuthoritativeManifestation() {
            if (IsPlanted) {
                return;
            }
            phaseTimer++;
            switch (Phase) {
                case ArbiterManifestationPhase.Forging:
                    if (phaseTimer >= ForgingFrames) {
                        SetPhase(ArbiterManifestationPhase.Poising);
                    }
                    break;
                case ArbiterManifestationPhase.Poising:
                    if (phaseTimer >= PoisingFrames) {
                        Velocity = Vector2.Zero;
                        fallingCollisionArmed = !ProbeInsideSolid(Position);
                        SetPhase(ArbiterManifestationPhase.Falling);
                    }
                    break;
                case ArbiterManifestationPhase.Falling:
                    UpdateAuthoritativeFalling();
                    break;
            }
        }

        private void UpdateAuthoritativeFalling() {
            float fallSpeed = Math.Min(Math.Max(Velocity.Y, 0f) + FallingGravity,
                MaximumFallingSpeed);

            if (TrySweepToGround(fallSpeed, out Vector2 landingAnchor)) {
                PlantAt(landingAnchor);
                return;
            }

            Velocity = new Vector2(0f, fallSpeed);
            TryFinishEmergencyFall();
        }

        private void UpdateClientFallingPrediction() {
            float fallSpeed = Math.Min(Math.Max(Velocity.Y, 0f) + FallingGravity,
                MaximumFallingSpeed);
            if (TrySweepToGround(fallSpeed, out Vector2 landingAnchor)) {
                FreezeClientPrediction(landingAnchor);
                return;
            }

            float worldBottom = Math.Max(Main.maxTilesY - 4, 1) * 16f;
            if (phaseTimer >= MaximumFallingFrames || Position.Y + fallSpeed >= worldBottom) {
                FreezeClientPrediction(new Vector2(Position.X,
                    Math.Min(Position.Y, worldBottom)));
                return;
            }

            Velocity = new Vector2(0f, fallSpeed);
        }

        private void FreezeClientPrediction(Vector2 position) {
            Position = position;
            Velocity = Vector2.Zero;
            NetTargetPosition = position;
            NetTargetVelocity = Vector2.Zero;
            NetTargetTick = (long)Main.GameUpdateCount;
        }

        private void TryFinishEmergencyFall() {
            float worldBottom = Math.Max(Main.maxTilesY - 4, 1) * 16f;
            if (phaseTimer < MaximumFallingFrames && Position.Y + Velocity.Y < worldBottom) {
                return;
            }

            Position = new Vector2(Position.X, Math.Min(Position.Y, worldBottom));
            Velocity = Vector2.Zero;
            if (emergencySearchCooldown > 0) {
                emergencySearchCooldown--;
                return;
            }
            emergencySearchCooldown = EmergencySearchCooldown;

            if (ArbiterManifestationLocationFinder.TryResolveEmergencyGround(
                Position, out Vector2 landingAnchor)) {
                CWRMod.Instance.Logger.Warn(
                    $"[ArbiterManifestation] Falling fallback at {landingAnchor}");
                PlantAt(landingAnchor);
                return;
            }

            CWRMod.Instance.Logger.Warn(
                $"[ArbiterManifestation] No validated landing near {Position}; retrying");
        }

        private void PlantAt(Vector2 landingAnchor) {
            Position = landingAnchor;
            Velocity = Vector2.Zero;
            SetPhase(ArbiterManifestationPhase.Planted);
            ArbiterManifestationSystem.MarkManifestationCompleted(landingAnchor);
        }

        private bool TrySweepToGround(float fallSpeed, out Vector2 landingAnchor) {
            landingAnchor = default;
            Vector2 probeOffset = new(-FallingProbeWidth * 0.5f, -FallingProbeHeight);
            Vector2 probe = Position + probeOffset;
            float remaining = Math.Max(fallSpeed, 0f);

            while (remaining > 0.001f) {
                float step = Math.Min(remaining, fallingCollisionArmed
                    ? FallingSubstep : EmbeddedFallingSubstep);
                Vector2 wanted = new(0f, step);
                if (!fallingCollisionArmed) {
                    probe += wanted;
                    remaining -= step;
                    fallingCollisionArmed = !ProbeInsideSolid(probe - probeOffset);
                    continue;
                }

                Vector2 allowed = Collision.TileCollision(probe, wanted,
                    FallingProbeWidth, FallingProbeHeight, fallThrough: true,
                    fall2: true, gravDir: 1);
                Vector2 moved = probe + allowed;
                Vector4 slope = Collision.SlopeCollision(moved, allowed,
                    FallingProbeWidth, FallingProbeHeight, gravity: 0f, fall: true);
                Vector2 resolved = new(slope.X, slope.Y);
                bool collided = allowed.Y < step - 0.01f
                    || resolved.Y < moved.Y - 0.01f;

                if (collided) {
                    landingAnchor = resolved - probeOffset;
                    return float.IsFinite(landingAnchor.X) && float.IsFinite(landingAnchor.Y);
                }

                probe = resolved;
                remaining -= step;
            }
            return false;
        }

        private static bool ProbeInsideSolid(Vector2 anchor) {
            Vector2 topLeft = anchor
                + new Vector2(-FallingProbeWidth * 0.5f, -FallingProbeHeight);
            for (int x = 0; x < FallingProbeWidth; x++) {
                for (int y = 0; y < FallingProbeHeight; y++) {
                    if (Collision.IsWorldPointSolid(
                        topLeft + new Vector2(x + 0.5f, y + 0.5f), true)) {
                        return true;
                    }
                }
            }
            return false;
        }

        private void SetPhase(ArbiterManifestationPhase phase) {
            if (Phase == phase) {
                return;
            }
            ArbiterManifestationPhase previous = Phase;
            phaseRaw = (int)phase;
            lastSeenPhase = phase;
            phaseTimer = 0;
            NetUpdate = true;
            if (!Main.dedServ && phase == ArbiterManifestationPhase.Planted) {
                plantWitnessed = previous == ArbiterManifestationPhase.Falling;
                plantedLocalTimer = 0;
                manifestationCutsceneEndTimer = ManifestAftermathFrames;
                PlayPlantImpactFeedback();
            }
        }

        private void PlayPlantImpactFeedback() {
            Player player = Main.LocalPlayer;
            if (player != null && player.active && player.Center.DistanceSQ(Position) < 1800f * 1800f) {
                player.CWR().GetScreenShake(9f);
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.5f }, Position);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.9f, Pitch = -0.35f }, Position);
        }

        internal void ForcePlanted() {
            phaseRaw = (int)ArbiterManifestationPhase.Planted;
            lastSeenPhase = ArbiterManifestationPhase.Planted;
            phaseTimer = 1;
            Velocity = Vector2.Zero;
            NetUpdate = true;
        }

        internal ArbiterManifestationResumeState GetResumeState()
            => new(Position, Velocity, Phase, phaseTimer);

        private float ManifestationProgress {
            get {
                return Phase switch {
                    ArbiterManifestationPhase.Forging
                        => MathHelper.Lerp(0f, 0.86f, Smooth01(phaseTimer / (float)ForgingFrames)),
                    ArbiterManifestationPhase.Poising
                        => MathHelper.Lerp(0.86f, 1f, Smooth01(phaseTimer / (float)PoisingFrames)),
                    _ => 1f,
                };
            }
        }

        /// <summary>成形余温:锻造期恒热,插地后随本地计时冷却;迟到端不见余温</summary>
        private float ForgeHeat {
            get {
                if (!IsPlanted) {
                    return 1f;
                }
                if (!plantWitnessed) {
                    return 0f;
                }
                return MathHelper.Clamp(1f - plantedLocalTimer / 300f, 0f, 1f);
            }
        }

        private Vector2 CurrentAxeCenter {
            get {
                if (localPullState is not (LocalPullState.Pulling or LocalPullState.AwaitingResult)) {
                    Vector2 baseCenter = AxeAnchor;
                    //蓄势期微微上提,坠落前吊一口气
                    if (Phase == ArbiterManifestationPhase.Poising) {
                        baseCenter.Y -= Smooth01(phaseTimer / (float)PoisingFrames) * 12f;
                    }
                    return baseCenter;
                }

                Player player = Main.LocalPlayer;
                Vector2 planted = AxeAnchor;
                //拔出方向:向上带一点朝玩家的倾斜
                float side = player != null && player.Center.X < Position.X ? -1f : 1f;
                Vector2 pullDirection = new Vector2(side * 0.20f, -0.98f);
                pullDirection.Normalize();
                if (localPullTimer <= PullChargeFrames) {
                    //攥柄发力:细碎挣动
                    float jitter = (float)Math.Sin(localPullTimer * 2.7f) * 1.6f;
                    return planted + new Vector2(jitter, 0f);
                }
                if (localPullTimer <= PullChargeFrames + PullDrawFrames) {
                    float t = (localPullTimer - PullChargeFrames) / (float)PullDrawFrames;
                    return planted + pullDirection * (Smooth01(t) * 46f);
                }

                float arcTime = MathHelper.Clamp((localPullTimer - PullChargeFrames - PullDrawFrames)
                    / (float)(PullFrames - PullChargeFrames - PullDrawFrames), 0f, 1f);
                float eased = Smooth01(arcTime);
                Vector2 start = planted + pullDirection * 46f;
                Vector2 end = player?.Center ?? start;
                Vector2 control = Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, -110f);
                return Vector2.Lerp(Vector2.Lerp(start, control, eased),
                    Vector2.Lerp(control, end, eased), eased);
            }
        }

        private float CurrentAxeRotation {
            get {
                //锻造期斜着成形,蓄势期正身为插地姿态
                if (localPullState is not (LocalPullState.Pulling or LocalPullState.AwaitingResult)) {
                    return Phase switch {
                        ArbiterManifestationPhase.Forging => PlantRotation - 0.35f,
                        ArbiterManifestationPhase.Poising
                            => MathHelper.Lerp(PlantRotation - 0.35f, PlantRotation
                                , Smooth01(phaseTimer / (float)PoisingFrames)),
                        _ => PlantRotation,
                    };
                }
                if (localPullTimer <= PullChargeFrames + PullDrawFrames) {
                    //攥柄期细碎挣动
                    if (localPullTimer <= PullChargeFrames) {
                        return PlantRotation + (float)Math.Sin(localPullTimer * 2.1f) * 0.035f;
                    }
                    return PlantRotation;
                }
                float t = MathHelper.Clamp((localPullTimer - PullChargeFrames - PullDrawFrames)
                    / (float)(PullFrames - PullChargeFrames - PullDrawFrames), 0f, 1f);
                Player player = Main.LocalPlayer;
                float side = player != null && player.Center.X < Position.X ? -1f : 1f;
                return PlantRotation + Smooth01(t) * MathHelper.TwoPi * 0.6f * side;
            }
        }

        private float CurrentAxeScale {
            get {
                if (localPullState is not (LocalPullState.Pulling or LocalPullState.AwaitingResult)
                    || localPullTimer <= PullChargeFrames + PullDrawFrames) {
                    return AxeScale;
                }
                float t = MathHelper.Clamp((localPullTimer - PullChargeFrames - PullDrawFrames)
                    / (float)(PullFrames - PullChargeFrames - PullDrawFrames), 0f, 1f);
                return AxeScale * MathHelper.Lerp(1f, 0.55f, Smooth01(t));
            }
        }

        private void TryStartManifestationCutscene() {
            if (IsPlanted || !ShouldShowForLocalPlayer()
                || Phase == ArbiterManifestationPhase.Falling
                    && phaseTimer >= MaximumFallingFrames
                || IsManifestationCutsceneBoundToThisActor()) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead
                || player.Center.DistanceSQ(CameraFocusPoint) > 1800f * 1800f) {
                return;
            }

            ArbiterActorRef subject = new(WhoAmI, Generation);
            bool restartStaleClip = CutsceneDirector.CurrentClip
                is ArbiterManifestCutscene;
            bool started
                = CutsceneDirector.Play<ArbiterManifestCutscene, ArbiterActorRef>(
                    subject, player, restartSameClip: restartStaleClip);
            if (started && !manifestationCutsceneStarted) {
                manifestationCutsceneStarted = true;
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Volume = 0.85f, Pitch = -0.55f });
            }
        }

        private void UpdateManifestationCutscene() {
            if (!IsManifestationCutsceneBoundToThisActor()) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                CutsceneDirector.Stop();
                return;
            }
            if (Phase == ArbiterManifestationPhase.Falling
                && phaseTimer >= MaximumFallingFrames) {
                CutsceneDirector.Stop();
                return;
            }

            //锁镜期间防残留伤害
            player.GivePlayerImmuneState(4);
            if (!IsPlanted) {
                return;
            }

            if (manifestationCutsceneEndTimer > 0) {
                manifestationCutsceneEndTimer--;
            }
            else {
                CutsceneDirector.Stop();
            }
        }

        private bool IsManifestationCutsceneBoundToThisActor() {
            if (CutsceneDirector.CurrentClip is not ArbiterManifestCutscene
                || CutsceneDirector.CurrentContext == null
                || !CutsceneDirector.CurrentContext.TryGetSubject(
                    out ArbiterActorRef subject)) {
                return false;
            }

            return subject.Slot == WhoAmI && subject.Generation == Generation;
        }

        private void UpdateLocalInteraction() {
            if (!IsPlanted) {
                promptAlpha = Math.Max(promptAlpha - 0.08f, 0f);
                return;
            }

            if (localPullState == LocalPullState.Hidden && ShouldShowForLocalPlayer()) {
                ResetLocalPull();
            }

            if (localPullState is LocalPullState.Pulling or LocalPullState.AwaitingResult) {
                UpdateLocalPull();
                return;
            }

            if (localPullState == LocalPullState.AwaitingPermit) {
                localRequestTimer++;
                if (localRequestTimer > 180) {
                    ResetLocalPull();
                }
                return;
            }

            if (!ShouldShowForLocalPlayer()) {
                localPullState = LocalPullState.Hidden;
                promptAlpha = 0f;
                return;
            }

            Player player = Main.LocalPlayer;
            bool nearby = player.Center.DistanceSQ(AxeAnchor) < InteractDistance * InteractDistance;
            bool canInteract = nearby && !Main.mapFullscreen && !player.mouseInterface
                && !CutsceneDirector.IsPlaying;
            promptAlpha = MathHelper.Clamp(promptAlpha + (canInteract ? 0.06f : -0.08f), 0f, 1f);

            if (canInteract && promptAlpha > 0.45f && Main.mouseRight && Main.mouseRightRelease) {
                TryRequestClaim(player);
            }
        }

        private void TryRequestClaim(Player player) {
            Item weapon = new(ModContent.ItemType<Arbiter>());
            if (!player.ItemSpace(weapon).CanTakeItemToPersonalInventory) {
                ShowInventoryFull(player);
                return;
            }

            localPullState = LocalPullState.AwaitingPermit;
            localRequestTimer = 0;
            promptAlpha = 0f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });
            ArbiterManifestationNet.RequestClaim(this);
        }

        internal bool BeginLocalPull(int token) {
            if (!IsPlanted || token <= 0 || localPullState != LocalPullState.AwaitingPermit) {
                return false;
            }

            localClaimToken = token;
            localPullTimer = 0;
            localCommitSent = false;
            localPullState = LocalPullState.Pulling;
            CutsceneDirector.Play<ArbiterPullCutscene, ArbiterActorRef>(
                new ArbiterActorRef(WhoAmI, Generation), Main.LocalPlayer, restartSameClip: false);
            return true;
        }

        private void UpdateLocalPull() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                ArbiterManifestationNet.CancelClaim(this, localClaimToken);
                ResetLocalPull();
                StopPullCutscene();
                return;
            }

            if (localPullState == LocalPullState.Pulling
                || CutsceneDirector.CurrentClip is ArbiterPullCutscene) {
                player.GivePlayerImmuneState(4);
            }
            if (Math.Abs(player.Center.X - Position.X) > 8f) {
                player.ChangeDir(player.Center.X < Position.X ? 1 : -1);
            }

            if (localPullState == LocalPullState.Pulling) {
                localPullTimer++;
                if (localPullTimer == PullChargeFrames + 2) {
                    //挣拔拍:土石迸裂+震屏
                    player.CWR().GetScreenShake(8f);
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.22f, Volume = 0.95f }, AxeAnchor);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Pitch = -0.1f, Volume = 0.5f }, AxeAnchor);
                    ArbiterManifestationRenderer.SpawnWrenchBurst(Position);
                }
                if (localPullTimer >= PullFrames && !localCommitSent) {
                    localCommitSent = true;
                    localPullState = LocalPullState.AwaitingResult;
                    localRequestTimer = 0;
                    ArbiterManifestationNet.CommitClaim(this, localClaimToken);
                }
            }
            else {
                localRequestTimer++;
                if (localRequestTimer > 300) {
                    ResetLocalPull();
                    StopPullCutscene();
                }
            }
        }

        internal void ApplyClaimResult(int token, bool success, bool inventoryFull) {
            if (localPullState == LocalPullState.AwaitingPermit && token == 0) {
                if (inventoryFull) {
                    ShowInventoryFull(Main.LocalPlayer);
                }
                ResetLocalPull();
                return;
            }
            if (token <= 0 || token != localClaimToken) {
                return;
            }

            if (!success) {
                if (inventoryFull) {
                    ShowInventoryFull(Main.LocalPlayer);
                }
                ResetLocalPull();
                StopPullCutscene();
                return;
            }

            localPullState = LocalPullState.Hidden;
            promptAlpha = 0f;
            StopPullCutscene();
            Player player = Main.LocalPlayer;
            player.CWR().GetScreenShake(4f);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 0.8f }, player.Center);
        }

        private static void ShowInventoryFull(Player player) {
            if (player == null || !player.active) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.6f, Volume = 0.5f });
            string text = Language.GetTextValue(
                "Mods.CalamityOverhaul.Items.Arbiter.ManifestInventoryFullHint");
            CombatText.NewText(player.getRect(), new Color(255, 120, 40), text);
        }

        private void ResetLocalPull() {
            localPullState = LocalPullState.None;
            localPullTimer = 0;
            localRequestTimer = 0;
            localClaimToken = 0;
            localCommitSent = false;
        }

        private static void StopPullCutscene() {
            if (CutsceneDirector.CurrentClip is ArbiterPullCutscene) {
                CutsceneDirector.Stop();
            }
        }

        private static bool ShouldShowForLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return false;
            }
            ArbiterAcquisitionPlayer acquisition
                = player.GetModPlayer<ArbiterAcquisitionPlayer>();
            return !acquisition.Claimed
                && !ArbiterAcquisitionPlayer.HasWeaponInPersonalStorage(player);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (Main.dedServ || !IsLocalPullActive && !ShouldShowForLocalPlayer()) {
                return false;
            }

            Texture2D axe = TextureAssets.Item[ModContent.ItemType<Arbiter>()].Value;
            float groundY = IsPlanted ? Position.Y : CurrentAxeCenter.Y + 2000f;
            ArbiterManifestationRenderer.Draw(spriteBatch, axe, CurrentAxeCenter,
                CurrentAxeRotation, CurrentAxeScale, ManifestationProgress, groundY, ForgeHeat);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            if (!IsPlanted || promptAlpha <= 0.01f || localPullState != LocalPullState.None
                || !ShouldShowForLocalPlayer()) {
                return;
            }

            string hint = Language.GetTextValue(
                "Mods.CalamityOverhaul.Items.Arbiter.ManifestInteractHint");
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(hint) * 0.9f;
            Vector2 position = AxeAnchor - Main.screenPosition + new Vector2(0f, -96f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;
            Color backing = new Color(150, 52, 12) with { A = 0 };
            spriteBatch.Draw(glow, position, null, backing * (promptAlpha * (0.35f + pulse * 0.12f)),
                0f, glow.Size() / 2f,
                new Vector2((size.X + 54f) / glow.Width, (size.Y + 30f) / glow.Height),
                SpriteEffects.None, 0f);
            Utils.DrawBorderString(spriteBatch, hint, position - size / 2f,
                new Color(255, 224, 190) * promptAlpha, 0.9f);
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
