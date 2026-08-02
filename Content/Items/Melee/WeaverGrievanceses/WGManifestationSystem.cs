using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    /// <summary>纠缠之怨显现的世界进度与权威Actor维护</summary>
    internal sealed class WGManifestationSystem : ModSystem
    {
        private const int SaveVersion = 1;
        private const float LegacyPrePlungeHeight = 118f;
        private const int EnsureInterval = 60;
        private const int SpawnFailureLogCooldown = 300;
        private const string SaveVersionKey = "WGManifestationVersion";
        private const string UnlockedKey = "WGManifestationUnlocked";
        private const string ManifestOriginKey = "WGManifestOrigin";
        private const string PlantedAnchorKey = "WGPlantedAnchor";
        private const string CompletedKey = "WGManifestationCompleted";
        private const string HasResumeStateKey = "WGHasResumeState";
        private const string ResumePositionKey = "WGResumePosition";
        private const string ResumeVelocityKey = "WGResumeVelocity";
        private const string ResumePhaseKey = "WGResumePhase";
        private const string ResumeTimerKey = "WGResumeTimer";

        internal static bool Unlocked { get; private set; }
        internal static Vector2 ManifestOrigin { get; private set; }
        internal static Vector2 PlantedAnchor { get; private set; }
        internal static bool ManifestationCompleted { get; private set; }

        private static bool pendingUnlock;
        private static Vector2 pendingOrigin;
        private static bool hasResumeState;
        private static WGManifestationResumeState resumeState;
        private static int ensureTimer;
        private static int spawnFailureLogTimer;

        public override void SaveWorldData(TagCompound tag) {
            RefreshResumeStateFromActor();

            tag[SaveVersionKey] = SaveVersion;
            tag[UnlockedKey] = Unlocked;
            tag[ManifestOriginKey] = ManifestOrigin;
            tag[PlantedAnchorKey] = PlantedAnchor;
            tag[CompletedKey] = ManifestationCompleted;
            tag[HasResumeStateKey] = hasResumeState;
            if (hasResumeState) {
                tag[ResumePositionKey] = resumeState.Position;
                tag[ResumeVelocityKey] = resumeState.Velocity;
                tag[ResumePhaseKey] = (int)resumeState.Phase;
                tag[ResumeTimerKey] = resumeState.PhaseTimer;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            ResetPersistentState();
            ResetTransientState();

            try {
                bool currentSave = tag != null && tag.TryGet(SaveVersionKey, out int version)
                    && version >= SaveVersion;
                string unlockedKey = currentSave ? UnlockedKey : nameof(Unlocked);
                Unlocked = tag != null && tag.TryGet(unlockedKey, out bool unlocked) && unlocked;
                if (Unlocked) {
                    LoadPersistentState(tag, currentSave);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error(
                    $"[WeaverGrievancesManifestation:LoadWorldData] Failed to load state: {ex.Message}");
                ResetPersistentState();
            }

            if (Unlocked && !HasValidPersistentPosition()) {
                CWRMod.Instance.Logger.Warn(
                    "[WeaverGrievancesManifestation] Invalid saved position; repairing near a player");
                ManifestOrigin = Vector2.Zero;
                PlantedAnchor = Vector2.Zero;
                hasResumeState = false;
            }
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(Unlocked);
            writer.WriteVector2(ManifestOrigin);
            writer.WriteVector2(PlantedAnchor);
            writer.Write(ManifestationCompleted);
        }

        public override void NetReceive(BinaryReader reader) {
            bool unlocked = reader.ReadBoolean();
            Vector2 manifestOrigin = reader.ReadVector2();
            Vector2 plantedAnchor = reader.ReadVector2();
            bool completed = reader.ReadBoolean();

            Vector2 position = completed ? plantedAnchor : manifestOrigin;
            if (unlocked && !WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(position)) {
                CWRMod.Instance.Logger.Warn(
                    $"[WeaverGrievancesManifestation:NetReceive] Ignoring invalid position {position}");
                unlocked = false;
                manifestOrigin = Vector2.Zero;
                plantedAnchor = Vector2.Zero;
                completed = false;
            }

            Unlocked = unlocked;
            ManifestOrigin = manifestOrigin;
            PlantedAnchor = plantedAnchor;
            ManifestationCompleted = unlocked && completed;
            hasResumeState = false;
        }

        public override void OnWorldLoad() => ResetTransientState();

        public override void OnWorldUnload() => ResetAllState();

        public override void Unload() => ResetAllState();

        public override void PostUpdateEverything() {
            if (VaultUtils.isClient || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            if (spawnFailureLogTimer > 0) {
                spawnFailureLogTimer--;
            }

            if (!Unlocked) {
                TryProcessUnlock();
                return;
            }

            if (!HasValidPersistentPosition()) {
                if (!TryRepairPersistentState()) {
                    return;
                }
            }

            if (ensureTimer > 0) {
                ensureTimer--;
                return;
            }

            bool actorReady = EnsureSingleActor();
            ensureTimer = actorReady ? EnsureInterval : 0;
        }

        internal static void RequestUnlock(Vector2 origin) {
            if (VaultUtils.isClient || Unlocked || CWRRef.GetBossRushActive()
                || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            pendingUnlock = true;
            pendingOrigin = origin;
            TryCommitPendingUnlock();
        }

        internal static void MarkManifestationCompleted(Vector2 landingAnchor) {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted) {
                return;
            }

            if (!WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(landingAnchor)) {
                CWRMod.Instance.Logger.Error(
                    $"[WeaverGrievancesManifestation] Invalid landing anchor {landingAnchor}");
                return;
            }

            PlantedAnchor = landingAnchor;
            ManifestationCompleted = true;
            hasResumeState = false;
            resumeState = default;
            SyncWorldState();
        }

        internal static void CaptureResumeState(WGManifestationResumeState state) {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted
                || !IsValidResumeState(state)) {
                return;
            }

            resumeState = state;
            hasResumeState = true;
        }

        internal static bool TryResolveActor(int slot, ushort generation,
            out WGManifestationActor actor) {
            actor = null;
            if (!Unlocked || slot < 0 || slot >= ActorLoader.MaxActorCount) {
                return false;
            }

            actor = ActorLoader.Actors[slot] as WGManifestationActor;
            return actor != null && actor.Active && actor.Generation == generation;
        }

        private static void TryProcessUnlock() {
            if (!pendingUnlock || !CWRRef.Has || CWRRef.GetBossRushActive()) {
                return;
            }

            TryCommitPendingUnlock();
        }

        private static bool TryCommitPendingUnlock() {
            if (!pendingUnlock || Unlocked
                || !WeaverGrievancesManifestationLocationFinder.TryCreateAppearanceAnchor(
                    pendingOrigin, out Vector2 appearanceAnchor)) {
                return false;
            }

            ManifestOrigin = pendingOrigin;
            PlantedAnchor = Vector2.Zero;
            Unlocked = true;
            ManifestationCompleted = false;
            resumeState = WGManifestationActor.CreateInitialState(ManifestOrigin);
            resumeState = resumeState with { Position = appearanceAnchor };
            hasResumeState = true;
            pendingUnlock = false;
            pendingOrigin = Vector2.Zero;
            ensureTimer = 0;

            SyncWorldState();
            bool actorReady = EnsureSingleActor();
            ensureTimer = actorReady ? EnsureInterval : 0;
            return true;
        }

        private static bool TryRepairPersistentState() {
            if (!TryGetFirstValidPlayer(out Player player)
                || !WeaverGrievancesManifestationLocationFinder.TryCreateAppearanceAnchor(
                    player.Center, out Vector2 appearanceAnchor)) {
                return false;
            }

            ManifestOrigin = player.Center;
            PlantedAnchor = Vector2.Zero;
            ManifestationCompleted = false;
            resumeState = WGManifestationActor.CreateInitialState(ManifestOrigin);
            resumeState = resumeState with { Position = appearanceAnchor };
            hasResumeState = true;
            SyncWorldState();
            return true;
        }

        private static bool EnsureSingleActor() {
            if (VaultUtils.isClient || !Unlocked || !HasValidPersistentPosition()) {
                return false;
            }

            List<WGManifestationActor> actors
                = ActorLoader.GetActiveActors<WGManifestationActor>();
            WGManifestationActor keeper = null;
            float nearestDistanceSq = float.MaxValue;
            Vector2 expectedPosition = ManifestationCompleted
                ? PlantedAnchor
                : hasResumeState ? resumeState.Position : WGManifestationActor
                    .CreateInitialState(ManifestOrigin).Position;

            foreach (WGManifestationActor actor in actors) {
                float distanceSq = Vector2.DistanceSquared(actor.Position, expectedPosition);
                bool fartherAlong = !ManifestationCompleted && keeper != null
                    && (actor.Phase > keeper.Phase
                        || actor.Phase == keeper.Phase && actor.PhaseTimer > keeper.PhaseTimer);
                if (keeper == null || fartherAlong
                    || ManifestationCompleted && distanceSq < nearestDistanceSq) {
                    keeper = actor;
                    nearestDistanceSq = distanceSq;
                }
            }

            if (actors.Count > 1) {
                CWRMod.Instance.Logger.Warn(
                    $"[WeaverGrievancesManifestation] Found {actors.Count} actors; removing duplicates");
                foreach (WGManifestationActor actor in actors) {
                    if (actor != keeper) {
                        ActorLoader.KillActor(actor.WhoAmI);
                    }
                }
            }

            if (keeper != null) {
                if (ManifestationCompleted && nearestDistanceSq > 0.25f) {
                    keeper.Position = PlantedAnchor;
                    keeper.Velocity = Vector2.Zero;
                    keeper.NetUpdate = true;
                }
                if (ManifestationCompleted && !keeper.IsPlanted) {
                    keeper.ForcePlanted();
                }
                if (!ManifestationCompleted) {
                    CaptureResumeState(keeper.GetResumeState());
                }
                return true;
            }

            int actorIndex = ManifestationCompleted
                ? WGManifestationActor.CreateAt(PlantedAnchor, planted: true)
                : WGManifestationActor.CreateAt(hasResumeState
                    ? resumeState
                    : WGManifestationActor.CreateInitialState(ManifestOrigin));
            if (actorIndex >= 0) {
                return true;
            }

            if (spawnFailureLogTimer <= 0) {
                CWRMod.Instance.Logger.Error(
                    $"[WeaverGrievancesManifestation] Actor spawn failed at {expectedPosition}; retrying");
                spawnFailureLogTimer = SpawnFailureLogCooldown;
            }
            return false;
        }

        private static void LoadPersistentState(TagCompound tag, bool currentSave) {
            if (currentSave) {
                ManifestOrigin = tag.TryGet(ManifestOriginKey, out Vector2 manifestOrigin)
                    ? manifestOrigin : Vector2.Zero;
                PlantedAnchor = tag.TryGet(PlantedAnchorKey, out Vector2 plantedAnchor)
                    ? plantedAnchor : Vector2.Zero;
                ManifestationCompleted = tag.TryGet(CompletedKey, out bool completed)
                    && completed;
                hasResumeState = !ManifestationCompleted
                    && tag.TryGet(HasResumeStateKey, out bool hasResume)
                    && hasResume
                    && TryReadResumeState(tag, out resumeState);
                if (!ManifestationCompleted && !hasResumeState
                    && WeaverGrievancesManifestationLocationFinder.TryCreateAppearanceAnchor(
                        ManifestOrigin, out Vector2 appearanceAnchor)) {
                    resumeState = WGManifestationActor.CreateInitialState(ManifestOrigin)
                        with { Position = appearanceAnchor };
                    hasResumeState = true;
                }
                return;
            }

            Vector2 legacyAnchor = tag.TryGet("Anchor", out Vector2 anchor)
                ? anchor : Vector2.Zero;
            ManifestationCompleted = tag.TryGet(nameof(ManifestationCompleted), out bool legacyCompleted)
                && legacyCompleted;
            if (ManifestationCompleted) {
                PlantedAnchor = legacyAnchor;
                ManifestOrigin = legacyAnchor - new Vector2(0f,
                    WGManifestationActor.SwordCenterHeight);
                return;
            }

            ManifestOrigin = legacyAnchor - new Vector2(0f,
                WGManifestationActor.SwordCenterHeight + LegacyPrePlungeHeight);
            PlantedAnchor = Vector2.Zero;
            resumeState = WGManifestationActor.CreateInitialState(ManifestOrigin);
            hasResumeState = IsValidResumeState(resumeState);
        }

        private static bool TryReadResumeState(TagCompound tag,
            out WGManifestationResumeState state) {
            state = default;
            if (!tag.TryGet(ResumePositionKey, out Vector2 position)
                || !tag.TryGet(ResumeVelocityKey, out Vector2 velocity)
                || !tag.TryGet(ResumePhaseKey, out int phaseRaw)
                || !tag.TryGet(ResumeTimerKey, out int phaseTimer)
                || phaseRaw < (int)WeaverGrievancesManifestationPhase.Gathering
                || phaseRaw >= (int)WeaverGrievancesManifestationPhase.Planted) {
                return false;
            }

            state = new WGManifestationResumeState(position, velocity,
                (WeaverGrievancesManifestationPhase)phaseRaw, phaseTimer);
            return IsValidResumeState(state);
        }

        private static bool IsValidResumeState(WGManifestationResumeState state)
            => state.Phase >= WeaverGrievancesManifestationPhase.Gathering
                && state.Phase < WeaverGrievancesManifestationPhase.Planted
                && state.PhaseTimer >= 0
                && WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(state.Position)
                && float.IsFinite(state.Velocity.X)
                && float.IsFinite(state.Velocity.Y);

        private static bool HasValidPersistentPosition() {
            if (!Unlocked) {
                return false;
            }
            if (ManifestationCompleted) {
                return WeaverGrievancesManifestationLocationFinder
                    .IsValidWorldPosition(PlantedAnchor);
            }
            return WeaverGrievancesManifestationLocationFinder
                .IsValidWorldPosition(ManifestOrigin)
                && (!hasResumeState || IsValidResumeState(resumeState));
        }

        private static void RefreshResumeStateFromActor() {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted) {
                return;
            }

            List<WGManifestationActor> actors
                = ActorLoader.GetActiveActors<WGManifestationActor>();
            WGManifestationActor keeper = null;
            foreach (WGManifestationActor actor in actors) {
                if (keeper == null || actor.Phase > keeper.Phase
                    || actor.Phase == keeper.Phase && actor.PhaseTimer > keeper.PhaseTimer) {
                    keeper = actor;
                }
            }
            if (keeper != null) {
                CaptureResumeState(keeper.GetResumeState());
            }
        }

        private static bool TryGetFirstValidPlayer(out Player result) {
            foreach (Player player in Main.ActivePlayers) {
                if (player != null && player.active && !player.dead
                    && float.IsFinite(player.Center.X) && float.IsFinite(player.Center.Y)) {
                    result = player;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static void SyncWorldState() {
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
            }
        }

        private static void ResetPersistentState() {
            Unlocked = false;
            ManifestOrigin = Vector2.Zero;
            PlantedAnchor = Vector2.Zero;
            ManifestationCompleted = false;
            hasResumeState = false;
            resumeState = default;
        }

        private static void ResetTransientState() {
            pendingUnlock = false;
            pendingOrigin = Vector2.Zero;
            ensureTimer = 0;
            spawnFailureLogTimer = 0;
        }

        private static void ResetAllState() {
            ResetPersistentState();
            ResetTransientState();
        }
    }

    /// <summary>噬魂幽花死亡时提交一次权威显现请求</summary>
    internal sealed class WeaverGrievancesPolterghastTrigger : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => CWRRef.Has && CWRID.NPC_Polterghast > NPCID.None
                && entity.type == CWRID.NPC_Polterghast;

        public override void OnNPCDeath(NPC npc) {
            if (VaultUtils.isClient || CWRRef.GetBossRushActive()) {
                return;
            }

            WGManifestationSystem.RequestUnlock(npc.Center);
        }
    }

    /// <summary>显现原点校验与异常落点兜底</summary>
    internal static class WeaverGrievancesManifestationLocationFinder
    {
        private const int WorldEdgeMargin = 4;
        private const int EmergencyHorizontalRadius = 24;

        private static bool WorldGeometryReady
            => Main.maxTilesX > WorldEdgeMargin * 2 && Main.maxTilesY > WorldEdgeMargin * 2;

        internal static bool TryCreateAppearanceAnchor(Vector2 origin, out Vector2 anchor) {
            anchor = origin + new Vector2(0f, WGManifestationActor.SwordCenterHeight);
            return IsValidWorldPosition(origin) && IsValidWorldPosition(anchor);
        }

        internal static bool IsValidWorldPosition(Vector2 position) {
            if (!WorldGeometryReady || !float.IsFinite(position.X) || !float.IsFinite(position.Y)) {
                return false;
            }

            float margin = WorldEdgeMargin * 16f;
            return position.X >= margin && position.X <= Main.maxTilesX * 16f - margin
                && position.Y >= margin && position.Y <= Main.maxTilesY * 16f - margin;
        }

        internal static bool TryResolveEmergencyGround(Vector2 origin, out Vector2 anchor) {
            anchor = Vector2.Zero;
            if (!WorldGeometryReady || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)) {
                return false;
            }

            int tileX = Math.Clamp((int)(origin.X / 16f), WorldEdgeMargin,
                Main.maxTilesX - WorldEdgeMargin - 1);
            int startY = Math.Clamp((int)MathF.Ceiling(origin.Y / 16f), WorldEdgeMargin,
                Main.maxTilesY - WorldEdgeMargin - 1);
            if (TryFindGroundColumn(tileX, startY, origin.X, out anchor)
                || TryFindGroundColumnUp(tileX, startY, origin.X, out anchor)) {
                return true;
            }

            for (int radius = 1; radius <= EmergencyHorizontalRadius; radius++) {
                int left = tileX - radius;
                if (left >= WorldEdgeMargin
                    && (TryFindGroundColumn(left, startY, left * 16f + 8f, out anchor)
                        || TryFindGroundColumnUp(left, startY,
                            left * 16f + 8f, out anchor))) {
                    return true;
                }

                int right = tileX + radius;
                if (right < Main.maxTilesX - WorldEdgeMargin
                    && (TryFindGroundColumn(right, startY, right * 16f + 8f, out anchor)
                        || TryFindGroundColumnUp(right, startY,
                            right * 16f + 8f, out anchor))) {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindGroundColumn(int tileX, int startY, float worldX,
            out Vector2 anchor) {
            anchor = Vector2.Zero;
            int endY = Main.maxTilesY - WorldEdgeMargin - 1;
            for (int tileY = startY; tileY <= endY; tileY++) {
                Tile ground = Main.tile[tileX, tileY];
                Tile above = Main.tile[tileX, tileY - 1];
                if (IsFlatGroundTile(ground) && !IsBlockingTile(above)) {
                    anchor = new Vector2(worldX, tileY * 16f);
                    return IsValidWorldPosition(anchor);
                }
            }
            return false;
        }

        private static bool TryFindGroundColumnUp(int tileX, int startY, float worldX,
            out Vector2 anchor) {
            anchor = Vector2.Zero;
            for (int tileY = startY; tileY >= WorldEdgeMargin; tileY--) {
                Tile ground = Main.tile[tileX, tileY];
                Tile above = Main.tile[tileX, tileY - 1];
                if (IsFlatGroundTile(ground) && !IsBlockingTile(above)) {
                    anchor = new Vector2(worldX, tileY * 16f);
                    return IsValidWorldPosition(anchor);
                }
            }
            return false;
        }

        private static bool IsFlatGroundTile(Tile tile)
            => IsBlockingTile(tile) && !tile.IsHalfBlock && tile.Slope == SlopeType.Solid;

        private static bool IsBlockingTile(Tile tile)
            => tile != null && tile.HasUnactuatedTile
                && tile.TileType < Main.tileSolid.Length
                && Main.tileSolid[tile.TileType]
                && !Main.tileSolidTop[tile.TileType];
    }
}
