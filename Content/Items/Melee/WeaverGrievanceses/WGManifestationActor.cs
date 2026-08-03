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
using WeaverGrievancesItem = CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses.WeaverGrievances;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    internal enum WeaverGrievancesManifestationPhase : byte
    {
        Gathering,
        Settling,
        Falling,
        Planted,
    }

    internal readonly record struct WGManifestationResumeState(
        Vector2 Position,
        Vector2 Velocity,
        WeaverGrievancesManifestationPhase Phase,
        int PhaseTimer);

    /// <summary>世界共享Actor，服务端推进阶段，各客户端独立拔刀</summary>
    internal sealed class WGManifestationActor : Actor
    {
        internal const int GatheringFrames = 82;
        internal const int SettlingFrames = 32;
        internal const int MaximumFallingFrames = 1800;
        internal const int ManifestAftermathFrames = 26;
        internal const int PullChargeFrames = 10;
        internal const int PullDrawFrames = 12;
        internal const int PullFrames = 44;
        internal const int PullCutsceneFrames = 64;
        internal const float InteractDistance = 220f;

        private const float SwordRotation = 2.42f;
        private const float SwordScale = 0.74f;
        internal const float SwordCenterHeight = 70f;
        private const float FallingGravity = 0.9f;
        private const float MaximumFallingSpeed = 36f;
        private const float FallingSubstep = 8f;
        private const float EmbeddedFallingSubstep = 1f;
        private const int FallingProbeWidth = 8;
        private const int FallingProbeHeight = 8;
        private const int EmergencySearchCooldown = 300;

        private static bool createWithResumeState;
        private static WGManifestationResumeState createResumeState;

        [SyncVar]
        private int phaseRaw = (int)WeaverGrievancesManifestationPhase.Gathering;

        private WeaverGrievancesManifestationPhase lastSeenPhase;
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

        private enum LocalPullState : byte
        {
            None,
            AwaitingPermit,
            Pulling,
            AwaitingResult,
            Hidden,
        }

        internal WeaverGrievancesManifestationPhase Phase
            => (WeaverGrievancesManifestationPhase)phaseRaw;

        internal bool IsPlanted => Phase == WeaverGrievancesManifestationPhase.Planted;

        internal int PhaseTimer => phaseTimer;

        internal Vector2 SwordAnchor => Position + new Vector2(0f, -SwordCenterHeight);

        internal Vector2 CameraFocusPoint => CurrentSwordCenter;

        internal bool IsLocalPullActive
            => localPullState is LocalPullState.Pulling or LocalPullState.AwaitingResult;

        public override Rectangle HitBox
            => new((int)Position.X - 90, (int)Position.Y - 220, 180, 220);

        public override Vector2 Center => SwordAnchor;

        internal static WGManifestationResumeState CreateInitialState(Vector2 manifestationOrigin)
            => new(manifestationOrigin + new Vector2(0f, SwordCenterHeight), Vector2.Zero,
                WeaverGrievancesManifestationPhase.Gathering, 0);

        internal static int CreateAt(Vector2 anchor, bool planted) {
            WGManifestationResumeState state = new(anchor, Vector2.Zero,
                planted ? WeaverGrievancesManifestationPhase.Planted
                    : WeaverGrievancesManifestationPhase.Gathering,
                planted ? 1 : 0);
            return CreateAt(state);
        }

        internal static int CreateAt(WGManifestationResumeState state) {
            if (VaultUtils.isClient) {
                return -1;
            }

            createWithResumeState = true;
            createResumeState = state;
            try {
                return ActorLoader.NewActor<WGManifestationActor>(state.Position, state.Velocity);
            } finally {
                createWithResumeState = false;
                createResumeState = default;
            }
        }

        public override void OnSpawn(params object[] args) {
            Width = 180;
            Height = 220;
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
            fallingCollisionArmed = Phase != WeaverGrievancesManifestationPhase.Falling
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
                WGManifestationSystem.CaptureResumeState(GetResumeState());
                UpdateAuthoritativeManifestation();
            }
            else if (Phase == WeaverGrievancesManifestationPhase.Falling && phaseTimer > 0) {
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
                WGMaterializationRenderer.UpdateAmbient(CurrentSwordCenter, progress, impact);
                float light = IsPlanted ? 0.48f : MathHelper.Lerp(0.18f, 0.72f, progress);
                Lighting.AddLight(CurrentSwordCenter, new Vector3(0.72f, 0.16f, 0.27f) * light);
            }
            if (impact) {
                phaseTimer = 1;
            }
            else if (VaultUtils.isClient && !IsPlanted) {
                phaseTimer++;
            }
        }

        private void ObservePhaseChange() {
            if (lastSeenPhase == Phase) {
                return;
            }

            lastSeenPhase = Phase;
            phaseTimer = 0;

            if (Phase == WeaverGrievancesManifestationPhase.Falling) {
                fallingCollisionArmed = !ProbeInsideSolid(Position);
            }

            if (!Main.dedServ && Phase == WeaverGrievancesManifestationPhase.Planted) {
                if (VaultUtils.isClient && HasNetTarget) {
                    Position = NetTargetPosition;
                    Velocity = Vector2.Zero;
                }
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
                case WeaverGrievancesManifestationPhase.Gathering:
                    if (phaseTimer >= GatheringFrames) {
                        SetPhase(WeaverGrievancesManifestationPhase.Settling);
                    }
                    break;
                case WeaverGrievancesManifestationPhase.Settling:
                    if (phaseTimer >= SettlingFrames) {
                        Velocity = Vector2.Zero;
                        fallingCollisionArmed = !ProbeInsideSolid(Position);
                        SetPhase(WeaverGrievancesManifestationPhase.Falling);
                    }
                    break;
                case WeaverGrievancesManifestationPhase.Falling:
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

            if (WeaverGrievancesManifestationLocationFinder.TryResolveEmergencyGround(
                Position, out Vector2 landingAnchor)) {
                CWRMod.Instance.Logger.Warn(
                    $"[WeaverGrievancesManifestation] Falling fallback at {landingAnchor}");
                PlantAt(landingAnchor);
                return;
            }

            CWRMod.Instance.Logger.Warn(
                $"[WeaverGrievancesManifestation] No validated landing near {Position}; retrying");
        }

        private void PlantAt(Vector2 landingAnchor) {
            Position = landingAnchor;
            Velocity = Vector2.Zero;
            SetPhase(WeaverGrievancesManifestationPhase.Planted);
            WGManifestationSystem.MarkManifestationCompleted(landingAnchor);
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

        private void SetPhase(WeaverGrievancesManifestationPhase phase) {
            if (Phase == phase) {
                return;
            }
            phaseRaw = (int)phase;
            lastSeenPhase = phase;
            phaseTimer = 0;
            NetUpdate = true;
            if (!Main.dedServ && phase == WeaverGrievancesManifestationPhase.Planted) {
                manifestationCutsceneEndTimer = ManifestAftermathFrames;
                PlayPlantImpactFeedback();
            }
        }

        private void PlayPlantImpactFeedback() {
            Player player = Main.LocalPlayer;
            if (player != null && player.active && player.Center.DistanceSQ(Position) < 1800f * 1800f) {
                player.CWR().GetScreenShake(6f);
            }
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.25f, Volume = 0.9f }, Position);
        }

        internal void ForcePlanted() {
            phaseRaw = (int)WeaverGrievancesManifestationPhase.Planted;
            lastSeenPhase = WeaverGrievancesManifestationPhase.Planted;
            phaseTimer = 1;
            Velocity = Vector2.Zero;
            NetUpdate = true;
        }

        internal WGManifestationResumeState GetResumeState()
            => new(Position, Velocity, Phase, phaseTimer);

        private float ManifestationProgress {
            get {
                return Phase switch {
                    WeaverGrievancesManifestationPhase.Gathering
                        => MathHelper.Lerp(0f, 0.86f, Smooth01(phaseTimer / (float)GatheringFrames)),
                    WeaverGrievancesManifestationPhase.Settling
                        => MathHelper.Lerp(0.86f, 1f, Smooth01(phaseTimer / (float)SettlingFrames)),
                    _ => 1f,
                };
            }
        }

        private Vector2 ManifestationSwordCenter => SwordAnchor;

        private Vector2 CurrentSwordCenter {
            get {
                if (localPullState is not (LocalPullState.Pulling or LocalPullState.AwaitingResult)) {
                    return ManifestationSwordCenter;
                }

                Player player = Main.LocalPlayer;
                Vector2 planted = SwordAnchor;
                Vector2 pullDirection = new(-0.62f, -0.78f);
                if (localPullTimer <= PullChargeFrames) {
                    return planted;
                }
                if (localPullTimer <= PullChargeFrames + PullDrawFrames) {
                    float t = (localPullTimer - PullChargeFrames) / (float)PullDrawFrames;
                    return planted + pullDirection * (Smooth01(t) * 42f);
                }

                float arcTime = MathHelper.Clamp((localPullTimer - PullChargeFrames - PullDrawFrames)
                    / (float)(PullFrames - PullChargeFrames - PullDrawFrames), 0f, 1f);
                float eased = Smooth01(arcTime);
                Vector2 start = planted + pullDirection * 42f;
                Vector2 end = player?.Center ?? start;
                Vector2 control = Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, -105f);
                return Vector2.Lerp(Vector2.Lerp(start, control, eased),
                    Vector2.Lerp(control, end, eased), eased);
            }
        }

        private float CurrentSwordRotation {
            get {
                if (localPullState is not (LocalPullState.Pulling or LocalPullState.AwaitingResult)
                    || localPullTimer <= PullChargeFrames + PullDrawFrames) {
                    return SwordRotation;
                }
                float t = MathHelper.Clamp((localPullTimer - PullChargeFrames - PullDrawFrames)
                    / (float)(PullFrames - PullChargeFrames - PullDrawFrames), 0f, 1f);
                return SwordRotation + Smooth01(t) * MathHelper.TwoPi * 0.7f;
            }
        }

        private float CurrentSwordScale {
            get {
                if (localPullState is not (LocalPullState.Pulling or LocalPullState.AwaitingResult)
                    || localPullTimer <= PullChargeFrames + PullDrawFrames) {
                    return SwordScale;
                }
                float t = MathHelper.Clamp((localPullTimer - PullChargeFrames - PullDrawFrames)
                    / (float)(PullFrames - PullChargeFrames - PullDrawFrames), 0f, 1f);
                return SwordScale * MathHelper.Lerp(1f, 0.55f, Smooth01(t));
            }
        }

        private void TryStartManifestationCutscene() {
            if (IsPlanted || !ShouldShowForLocalPlayer()
                || Phase == WeaverGrievancesManifestationPhase.Falling
                    && phaseTimer >= MaximumFallingFrames
                || IsManifestationCutsceneBoundToThisActor()) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead
                || player.Center.DistanceSQ(CameraFocusPoint) > 1800f * 1800f) {
                return;
            }

            WeaverGrievancesActorRef subject = new(WhoAmI, Generation);
            bool restartStaleClip = CutsceneDirector.CurrentClip
                is WeaverGrievancesManifestCutscene;
            bool started
                = CutsceneDirector.Play<WeaverGrievancesManifestCutscene, WeaverGrievancesActorRef>(
                    subject, player, restartSameClip: restartStaleClip);
            if (started && !manifestationCutsceneStarted) {
                manifestationCutsceneStarted = true;
                SoundEngine.PlaySound(CWRSound.SoulConnection);
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
            if (Phase == WeaverGrievancesManifestationPhase.Falling
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
            if (CutsceneDirector.CurrentClip is not WeaverGrievancesManifestCutscene
                || CutsceneDirector.CurrentContext == null
                || !CutsceneDirector.CurrentContext.TryGetSubject(
                    out WeaverGrievancesActorRef subject)) {
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
            bool nearby = player.Center.DistanceSQ(SwordAnchor) < InteractDistance * InteractDistance;
            bool canInteract = nearby && !Main.mapFullscreen && !player.mouseInterface
                && !CutsceneDirector.IsPlaying;
            promptAlpha = MathHelper.Clamp(promptAlpha + (canInteract ? 0.06f : -0.08f), 0f, 1f);

            if (canInteract && promptAlpha > 0.45f && Main.mouseRight && Main.mouseRightRelease) {
                TryRequestClaim(player);
            }
        }

        private void TryRequestClaim(Player player) {
            Item weapon = new(ModContent.ItemType<WeaverGrievancesItem>());
            if (!player.ItemSpace(weapon).CanTakeItemToPersonalInventory) {
                ShowInventoryFull(player);
                return;
            }

            localPullState = LocalPullState.AwaitingPermit;
            localRequestTimer = 0;
            promptAlpha = 0f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });
            WGManifestationNet.RequestClaim(this);
        }

        internal bool BeginLocalPull(int token) {
            if (!IsPlanted || token <= 0 || localPullState != LocalPullState.AwaitingPermit) {
                return false;
            }

            localClaimToken = token;
            localPullTimer = 0;
            localCommitSent = false;
            localPullState = LocalPullState.Pulling;
            CutsceneDirector.Play<WeaverGrievancesPullCutscene, WeaverGrievancesActorRef>(
                new WeaverGrievancesActorRef(WhoAmI, Generation), Main.LocalPlayer, restartSameClip: false);
            return true;
        }

        private void UpdateLocalPull() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                WGManifestationNet.CancelClaim(this, localClaimToken);
                ResetLocalPull();
                StopPullCutscene();
                return;
            }

            if (localPullState == LocalPullState.Pulling
                || CutsceneDirector.CurrentClip is WeaverGrievancesPullCutscene) {
                player.GivePlayerImmuneState(4);
            }
            if (Math.Abs(player.Center.X - Position.X) > 8f) {
                player.ChangeDir(player.Center.X < Position.X ? 1 : -1);
            }

            if (localPullState == LocalPullState.Pulling) {
                localPullTimer++;
                if (localPullTimer == PullChargeFrames + 2) {
                    player.CWR().GetScreenShake(7f);
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.28f, Volume = 0.9f }, SwordAnchor);
                }
                if (localPullTimer >= PullFrames && !localCommitSent) {
                    localCommitSent = true;
                    localPullState = LocalPullState.AwaitingResult;
                    localRequestTimer = 0;
                    WGManifestationNet.CommitClaim(this, localClaimToken);
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
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.35f, Volume = 0.75f }, player.Center);
        }

        private static void ShowInventoryFull(Player player) {
            if (player == null || !player.active) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.6f, Volume = 0.5f });
            string text = Language.GetTextValue(
                "Mods.CalamityOverhaul.Items.WeaverGrievances.RitualInventoryFullHint");
            CombatText.NewText(player.getRect(), new Color(204, 82, 112), text);
        }

        private void ResetLocalPull() {
            localPullState = LocalPullState.None;
            localPullTimer = 0;
            localRequestTimer = 0;
            localClaimToken = 0;
            localCommitSent = false;
        }

        private static void StopPullCutscene() {
            if (CutsceneDirector.CurrentClip is WeaverGrievancesPullCutscene) {
                CutsceneDirector.Stop();
            }
        }

        private static bool ShouldShowForLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return false;
            }
            WGAcquisitionPlayer acquisition
                = player.GetModPlayer<WGAcquisitionPlayer>();
            return !acquisition.Claimed
                && !WGAcquisitionPlayer.HasWeaponInPersonalStorage(player);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (Main.dedServ || !IsLocalPullActive && !ShouldShowForLocalPlayer()) {
                return false;
            }

            Texture2D sword = TextureAssets.Item[ModContent.ItemType<WeaverGrievancesItem>()].Value;
            float groundY = IsPlanted ? Position.Y : CurrentSwordCenter.Y + 2000f;
            WGMaterializationRenderer.Draw(spriteBatch, sword, CurrentSwordCenter,
                CurrentSwordRotation, CurrentSwordScale, ManifestationProgress, groundY);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            if (!IsPlanted || promptAlpha <= 0.01f || localPullState != LocalPullState.None
                || !ShouldShowForLocalPlayer()) {
                return;
            }

            string hint = Language.GetTextValue(
                "Mods.CalamityOverhaul.Items.WeaverGrievances.RitualInteractHint");
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(hint) * 0.9f;
            Vector2 position = SwordAnchor - Main.screenPosition + new Vector2(0f, -104f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;
            Color backing = new Color(120, 24, 60) with { A = 0 };
            spriteBatch.Draw(glow, position, null, backing * (promptAlpha * (0.35f + pulse * 0.12f)),
                0f, glow.Size() / 2f,
                new Vector2((size.X + 54f) / glow.Width, (size.Y + 30f) / glow.Height),
                SpriteEffects.None, 0f);
            Utils.DrawBorderString(spriteBatch, hint, position - size / 2f,
                new Color(242, 218, 226) * promptAlpha, 0.9f);
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
