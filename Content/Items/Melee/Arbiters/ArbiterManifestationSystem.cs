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

namespace CalamityOverhaul.Content.Items.Melee.Arbiters
{
    /// <summary>断罪师显现的世界进度与权威Actor维护(镜像 WGManifestationSystem)</summary>
    internal sealed class ArbiterManifestationSystem : ModSystem
    {
        private const int SaveVersion = 1;
        private const int EnsureInterval = 60;
        private const int SpawnFailureLogCooldown = 300;
        /// <summary>肉山死亡后延迟这么多帧再起演,给掉落散场留时间</summary>
        private const int UnlockDelayFrames = 90;
        private const string SaveVersionKey = "ArbiterManifestVersion";
        private const string UnlockedKey = "ArbiterManifestUnlocked";
        private const string ManifestOriginKey = "ArbiterManifestOrigin";
        private const string PlantedAnchorKey = "ArbiterPlantedAnchor";
        private const string CompletedKey = "ArbiterManifestCompleted";
        private const string HasResumeStateKey = "ArbiterHasResumeState";
        private const string ResumePositionKey = "ArbiterResumePosition";
        private const string ResumeVelocityKey = "ArbiterResumeVelocity";
        private const string ResumePhaseKey = "ArbiterResumePhase";
        private const string ResumeTimerKey = "ArbiterResumeTimer";

        internal static bool Unlocked { get; private set; }
        internal static Vector2 ManifestOrigin { get; private set; }
        internal static Vector2 PlantedAnchor { get; private set; }
        internal static bool ManifestationCompleted { get; private set; }

        private static bool pendingUnlock;
        private static Vector2 pendingOrigin;
        private static int pendingDelay;
        private static bool hasResumeState;
        private static ArbiterManifestationResumeState resumeState;
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
                bool valid = tag != null && tag.TryGet(SaveVersionKey, out int version)
                    && version >= SaveVersion;
                Unlocked = valid && tag.TryGet(UnlockedKey, out bool unlocked) && unlocked;
                if (Unlocked) {
                    LoadPersistentState(tag);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error(
                    $"[ArbiterManifestation:LoadWorldData] Failed to load state: {ex.Message}");
                ResetPersistentState();
            }

            if (Unlocked && !HasValidPersistentPosition()) {
                CWRMod.Instance.Logger.Warn(
                    "[ArbiterManifestation] Invalid saved position; repairing near a player");
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
            if (unlocked && !ArbiterManifestationLocationFinder.IsValidWorldPosition(position)) {
                CWRMod.Instance.Logger.Warn(
                    $"[ArbiterManifestation:NetReceive] Ignoring invalid position {position}");
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

        /// <summary>肉山死亡处提交显现请求(仅服务端/单人;BossRush 与子世界不受理)</summary>
        internal static void RequestUnlock(Vector2 origin) {
            if (VaultUtils.isClient || Unlocked || CWRRef.GetBossRushActive()
                || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            pendingUnlock = true;
            pendingOrigin = origin;
            pendingDelay = UnlockDelayFrames;
        }

        internal static void MarkManifestationCompleted(Vector2 landingAnchor) {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted) {
                return;
            }

            if (!ArbiterManifestationLocationFinder.IsValidWorldPosition(landingAnchor)) {
                CWRMod.Instance.Logger.Error(
                    $"[ArbiterManifestation] Invalid landing anchor {landingAnchor}");
                return;
            }

            PlantedAnchor = landingAnchor;
            ManifestationCompleted = true;
            hasResumeState = false;
            resumeState = default;
            SyncWorldState();
        }

        internal static void CaptureResumeState(ArbiterManifestationResumeState state) {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted
                || !IsValidResumeState(state)) {
                return;
            }

            resumeState = state;
            hasResumeState = true;
        }

        internal static bool TryResolveActor(int slot, ushort generation,
            out ArbiterManifestationActor actor) {
            actor = null;
            if (!Unlocked || slot < 0 || slot >= ActorLoader.MaxActorCount) {
                return false;
            }

            actor = ActorLoader.Actors[slot] as ArbiterManifestationActor;
            return actor != null && actor.Active && actor.Generation == generation;
        }

        private static void TryProcessUnlock() {
            if (!pendingUnlock || CWRRef.GetBossRushActive()) {
                return;
            }
            //起演延迟:肉山碎块与掉落先散场
            if (pendingDelay > 0) {
                pendingDelay--;
                return;
            }

            TryCommitPendingUnlock();
        }

        private static bool TryCommitPendingUnlock() {
            if (!pendingUnlock || Unlocked
                || !ArbiterManifestationLocationFinder.TryCreateAppearanceAnchor(
                    pendingOrigin, out Vector2 appearanceAnchor)) {
                return false;
            }

            ManifestOrigin = pendingOrigin;
            PlantedAnchor = Vector2.Zero;
            Unlocked = true;
            ManifestationCompleted = false;
            resumeState = ArbiterManifestationActor.CreateInitialState(ManifestOrigin);
            resumeState = resumeState with { Position = appearanceAnchor };
            hasResumeState = true;
            pendingUnlock = false;
            pendingOrigin = Vector2.Zero;
            pendingDelay = 0;
            ensureTimer = 0;

            SyncWorldState();
            bool actorReady = EnsureSingleActor();
            ensureTimer = actorReady ? EnsureInterval : 0;
            return true;
        }

        private static bool TryRepairPersistentState() {
            if (!TryGetFirstValidPlayer(out Player player)
                || !ArbiterManifestationLocationFinder.TryCreateAppearanceAnchor(
                    player.Center, out Vector2 appearanceAnchor)) {
                return false;
            }

            ManifestOrigin = player.Center;
            PlantedAnchor = Vector2.Zero;
            ManifestationCompleted = false;
            resumeState = ArbiterManifestationActor.CreateInitialState(ManifestOrigin);
            resumeState = resumeState with { Position = appearanceAnchor };
            hasResumeState = true;
            SyncWorldState();
            return true;
        }

        private static bool EnsureSingleActor() {
            if (VaultUtils.isClient || !Unlocked || !HasValidPersistentPosition()) {
                return false;
            }

            List<ArbiterManifestationActor> actors
                = ActorLoader.GetActiveActors<ArbiterManifestationActor>();
            ArbiterManifestationActor keeper = null;
            float nearestDistanceSq = float.MaxValue;
            Vector2 expectedPosition = ManifestationCompleted
                ? PlantedAnchor
                : hasResumeState ? resumeState.Position : ArbiterManifestationActor
                    .CreateInitialState(ManifestOrigin).Position;

            foreach (ArbiterManifestationActor actor in actors) {
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
                    $"[ArbiterManifestation] Found {actors.Count} actors; removing duplicates");
                foreach (ArbiterManifestationActor actor in actors) {
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
                ? ArbiterManifestationActor.CreateAt(PlantedAnchor, planted: true)
                : ArbiterManifestationActor.CreateAt(hasResumeState
                    ? resumeState
                    : ArbiterManifestationActor.CreateInitialState(ManifestOrigin));
            if (actorIndex >= 0) {
                return true;
            }

            if (spawnFailureLogTimer <= 0) {
                CWRMod.Instance.Logger.Error(
                    $"[ArbiterManifestation] Actor spawn failed at {expectedPosition}; retrying");
                spawnFailureLogTimer = SpawnFailureLogCooldown;
            }
            return false;
        }

        private static void LoadPersistentState(TagCompound tag) {
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
                && ArbiterManifestationLocationFinder.TryCreateAppearanceAnchor(
                    ManifestOrigin, out Vector2 appearanceAnchor)) {
                resumeState = ArbiterManifestationActor.CreateInitialState(ManifestOrigin)
                    with { Position = appearanceAnchor };
                hasResumeState = true;
            }
        }

        private static bool TryReadResumeState(TagCompound tag,
            out ArbiterManifestationResumeState state) {
            state = default;
            if (!tag.TryGet(ResumePositionKey, out Vector2 position)
                || !tag.TryGet(ResumeVelocityKey, out Vector2 velocity)
                || !tag.TryGet(ResumePhaseKey, out int phaseRaw)
                || !tag.TryGet(ResumeTimerKey, out int phaseTimer)
                || phaseRaw < (int)ArbiterManifestationPhase.Forging
                || phaseRaw >= (int)ArbiterManifestationPhase.Planted) {
                return false;
            }

            state = new ArbiterManifestationResumeState(position, velocity,
                (ArbiterManifestationPhase)phaseRaw, phaseTimer);
            return IsValidResumeState(state);
        }

        private static bool IsValidResumeState(ArbiterManifestationResumeState state)
            => state.Phase >= ArbiterManifestationPhase.Forging
                && state.Phase < ArbiterManifestationPhase.Planted
                && state.PhaseTimer >= 0
                && ArbiterManifestationLocationFinder.IsValidWorldPosition(state.Position)
                && float.IsFinite(state.Velocity.X)
                && float.IsFinite(state.Velocity.Y);

        private static bool HasValidPersistentPosition() {
            if (!Unlocked) {
                return false;
            }
            if (ManifestationCompleted) {
                return ArbiterManifestationLocationFinder
                    .IsValidWorldPosition(PlantedAnchor);
            }
            return ArbiterManifestationLocationFinder
                .IsValidWorldPosition(ManifestOrigin)
                && (!hasResumeState || IsValidResumeState(resumeState));
        }

        private static void RefreshResumeStateFromActor() {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted) {
                return;
            }

            List<ArbiterManifestationActor> actors
                = ActorLoader.GetActiveActors<ArbiterManifestationActor>();
            ArbiterManifestationActor keeper = null;
            foreach (ArbiterManifestationActor actor in actors) {
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
            pendingDelay = 0;
            ensureTimer = 0;
            spawnFailureLogTimer = 0;
        }

        private static void ResetAllState() {
            ResetPersistentState();
            ResetTransientState();
        }

#if DEBUG
        /// <summary>调试(单人用):清世界显现状态与Actor,重置本机玩家认领标记,重看完整演出</summary>
        internal static void DebugReset() {
            foreach (ArbiterManifestationActor actor
                in ActorLoader.GetActiveActors<ArbiterManifestationActor>()) {
                ActorLoader.KillActor(actor.WhoAmI);
            }
            ResetAllState();
            if (!Main.dedServ && Main.LocalPlayer?.active == true) {
                Main.LocalPlayer.GetModPlayer<ArbiterAcquisitionPlayer>().DebugResetClaim();
            }
        }
#endif
    }

    /// <summary>血肉之墙死亡时提交一次权威显现请求;旧世界补打一次肉山同样触发</summary>
    internal sealed class ArbiterWofTrigger : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => entity.type == NPCID.WallofFlesh;

        public override void OnNPCDeath(NPC npc) {
            if (VaultUtils.isClient || CWRRef.GetBossRushActive()) {
                return;
            }

            ArbiterManifestationSystem.RequestUnlock(npc.Center);
        }
    }

    /// <summary>显现原点校验与异常落点兜底(地狱特化:应急找地优先避开熔岩)</summary>
    internal static class ArbiterManifestationLocationFinder
    {
        private const int WorldEdgeMargin = 4;
        private const int EmergencyHorizontalRadius = 24;

        private static bool WorldGeometryReady
            => Main.maxTilesX > WorldEdgeMargin * 2 && Main.maxTilesY > WorldEdgeMargin * 2;

        internal static bool TryCreateAppearanceAnchor(Vector2 origin, out Vector2 anchor) {
            anchor = origin + new Vector2(0f, ArbiterManifestationActor.AxeCenterHeight);
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

        /// <summary>应急找地:先只收无熔岩列,找不到再放宽收任意实心地</summary>
        internal static bool TryResolveEmergencyGround(Vector2 origin, out Vector2 anchor) {
            if (TryResolveEmergencyGround(origin, avoidLava: true, out anchor)) {
                return true;
            }
            return TryResolveEmergencyGround(origin, avoidLava: false, out anchor);
        }

        private static bool TryResolveEmergencyGround(Vector2 origin, bool avoidLava, out Vector2 anchor) {
            anchor = Vector2.Zero;
            if (!WorldGeometryReady || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)) {
                return false;
            }

            int tileX = Math.Clamp((int)(origin.X / 16f), WorldEdgeMargin,
                Main.maxTilesX - WorldEdgeMargin - 1);
            int startY = Math.Clamp((int)MathF.Ceiling(origin.Y / 16f), WorldEdgeMargin,
                Main.maxTilesY - WorldEdgeMargin - 1);
            if (TryFindGroundColumn(tileX, startY, origin.X, avoidLava, out anchor)
                || TryFindGroundColumnUp(tileX, startY, origin.X, avoidLava, out anchor)) {
                return true;
            }

            for (int radius = 1; radius <= EmergencyHorizontalRadius; radius++) {
                int left = tileX - radius;
                if (left >= WorldEdgeMargin
                    && (TryFindGroundColumn(left, startY, left * 16f + 8f, avoidLava, out anchor)
                        || TryFindGroundColumnUp(left, startY,
                            left * 16f + 8f, avoidLava, out anchor))) {
                    return true;
                }

                int right = tileX + radius;
                if (right < Main.maxTilesX - WorldEdgeMargin
                    && (TryFindGroundColumn(right, startY, right * 16f + 8f, avoidLava, out anchor)
                        || TryFindGroundColumnUp(right, startY,
                            right * 16f + 8f, avoidLava, out anchor))) {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindGroundColumn(int tileX, int startY, float worldX,
            bool avoidLava, out Vector2 anchor) {
            anchor = Vector2.Zero;
            int endY = Main.maxTilesY - WorldEdgeMargin - 1;
            for (int tileY = startY; tileY <= endY; tileY++) {
                Tile ground = Main.tile[tileX, tileY];
                Tile above = Main.tile[tileX, tileY - 1];
                if (IsFlatGroundTile(ground) && !IsBlockingTile(above)
                    && (!avoidLava || !HasLava(above))) {
                    anchor = new Vector2(worldX, tileY * 16f);
                    return IsValidWorldPosition(anchor);
                }
            }
            return false;
        }

        private static bool TryFindGroundColumnUp(int tileX, int startY, float worldX,
            bool avoidLava, out Vector2 anchor) {
            anchor = Vector2.Zero;
            for (int tileY = startY; tileY >= WorldEdgeMargin; tileY--) {
                Tile ground = Main.tile[tileX, tileY];
                Tile above = Main.tile[tileX, tileY - 1];
                if (IsFlatGroundTile(ground) && !IsBlockingTile(above)
                    && (!avoidLava || !HasLava(above))) {
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

        private static bool HasLava(Tile tile)
            => tile != null && tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava;
    }
}
