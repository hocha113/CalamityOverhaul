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
        private const int EnsureInterval = 60;
        private const int SpawnFailureLogCooldown = 300;

        internal static bool Unlocked { get; private set; }
        internal static Vector2 Anchor { get; private set; }
        internal static bool ManifestationCompleted { get; private set; }

        private static bool pendingUnlock;
        private static Vector2 pendingOrigin;
        private static bool restorePlantedOnNextUpdate;
        private static int ensureTimer;
        private static int spawnFailureLogTimer;

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(Unlocked)] = Unlocked;
            tag[nameof(Anchor)] = Anchor;
            tag[nameof(ManifestationCompleted)] = ManifestationCompleted;
        }

        public override void LoadWorldData(TagCompound tag) {
            ResetPersistentState();
            ResetTransientState();

            try {
                Unlocked = tag != null && tag.TryGet(nameof(Unlocked), out bool unlocked) && unlocked;
                Anchor = tag != null && tag.TryGet(nameof(Anchor), out Vector2 anchor)
                    ? anchor : Vector2.Zero;
                ManifestationCompleted = Unlocked
                    && tag != null
                    && tag.TryGet(nameof(ManifestationCompleted), out bool completed)
                    && completed;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error(
                    $"[WeaverGrievancesManifestation:LoadWorldData] Failed to load state: {ex.Message}");
                ResetPersistentState();
            }

            if (Unlocked && !WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(Anchor)) {
                CWRMod.Instance.Logger.Warn(
                    $"[WeaverGrievancesManifestation] Invalid saved anchor {Anchor}; repairing near a player");
                Anchor = Vector2.Zero;
            }

            //Actor不存档，重载恢复插地态
            restorePlantedOnNextUpdate = Unlocked;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(Unlocked);
            writer.WriteVector2(Anchor);
            writer.Write(ManifestationCompleted);
        }

        public override void NetReceive(BinaryReader reader) {
            bool unlocked = reader.ReadBoolean();
            Vector2 anchor = reader.ReadVector2();
            bool completed = reader.ReadBoolean();

            if (unlocked && !WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(anchor)) {
                CWRMod.Instance.Logger.Warn(
                    $"[WeaverGrievancesManifestation:NetReceive] Ignoring invalid anchor {anchor}");
                unlocked = false;
                anchor = Vector2.Zero;
                completed = false;
            }

            Unlocked = unlocked;
            Anchor = anchor;
            ManifestationCompleted = unlocked && completed;
            restorePlantedOnNextUpdate = false;
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

            if (restorePlantedOnNextUpdate && Unlocked) {
                restorePlantedOnNextUpdate = false;
                if (!ManifestationCompleted) {
                    ManifestationCompleted = true;
                    SyncWorldState();
                }
            }

            if (!Unlocked) {
                TryProcessUnlock();
                return;
            }

            if (!WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(Anchor)) {
                if (!TryRepairAnchor()) {
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
            if (VaultUtils.isClient || Unlocked || !CWRRef.Has || CWRRef.GetBossRushActive()
                || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            pendingUnlock = true;
            pendingOrigin = origin;
            TryCommitPendingUnlock();
        }

        internal static void MarkManifestationCompleted() {
            if (VaultUtils.isClient || !Unlocked || ManifestationCompleted) {
                return;
            }

            ManifestationCompleted = true;
            SyncWorldState();
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
            if (!CWRRef.Has || CWRRef.GetBossRushActive()) {
                return;
            }

            if (pendingUnlock) {
                TryCommitPendingUnlock();
                return;
            }

            if (!CWRRef.GetDownedPolterghast() || !TryGetFirstValidPlayer(out Player player)) {
                return;
            }

            pendingUnlock = true;
            pendingOrigin = player.Center;
            TryCommitPendingUnlock();
        }

        private static bool TryCommitPendingUnlock() {
            if (!pendingUnlock || Unlocked
                || !WeaverGrievancesManifestationLocationFinder.TryResolveNear(pendingOrigin, out Vector2 anchor)) {
                return false;
            }

            Anchor = anchor;
            Unlocked = true;
            ManifestationCompleted = false;
            pendingUnlock = false;
            pendingOrigin = Vector2.Zero;
            ensureTimer = 0;

            SyncWorldState();
            bool actorReady = EnsureSingleActor();
            ensureTimer = actorReady ? EnsureInterval : 0;
            return true;
        }

        private static bool TryRepairAnchor() {
            if (!TryGetFirstValidPlayer(out Player player)
                || !WeaverGrievancesManifestationLocationFinder.TryResolveNear(player.Center, out Vector2 anchor)) {
                return false;
            }

            Anchor = anchor;
            ManifestationCompleted = true;
            restorePlantedOnNextUpdate = false;
            SyncWorldState();
            return true;
        }

        private static bool EnsureSingleActor() {
            if (VaultUtils.isClient || !Unlocked
                || !WeaverGrievancesManifestationLocationFinder.IsValidWorldPosition(Anchor)) {
                return false;
            }

            List<WGManifestationActor> actors
                = ActorLoader.GetActiveActors<WGManifestationActor>();
            WGManifestationActor keeper = null;
            float nearestDistanceSq = float.MaxValue;

            foreach (WGManifestationActor actor in actors) {
                float distanceSq = Vector2.DistanceSquared(actor.Position, Anchor);
                if (keeper == null || distanceSq < nearestDistanceSq) {
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
                if (nearestDistanceSq > 0.25f) {
                    keeper.Position = Anchor;
                    keeper.NetUpdate = true;
                }
                if (ManifestationCompleted && !keeper.IsPlanted) {
                    keeper.ForcePlanted();
                }
                return true;
            }

            int actorIndex = WGManifestationActor.CreateAt(Anchor, ManifestationCompleted);
            if (actorIndex >= 0) {
                return true;
            }

            if (spawnFailureLogTimer <= 0) {
                CWRMod.Instance.Logger.Error(
                    $"[WeaverGrievancesManifestation] Actor spawn failed at {Anchor}; retrying");
                spawnFailureLogTimer = SpawnFailureLogCooldown;
            }
            return false;
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
            Anchor = Vector2.Zero;
            ManifestationCompleted = false;
        }

        private static void ResetTransientState() {
            pendingUnlock = false;
            pendingOrigin = Vector2.Zero;
            restorePlantedOnNextUpdate = false;
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

    /// <summary>在触发点下方寻找可插刀地面，并提供出生区兜底</summary>
    internal static class WeaverGrievancesManifestationLocationFinder
    {
        private const int WorldEdgeMargin = 40;
        private const int LocalSearchRadius = 48;
        private const int LocalSearchDepth = 180;
        private const int RequiredClearance = 18;
        private const int RequiredHalfWidth = 6;

        private static bool WorldGeometryReady
            => Main.maxTilesX > WorldEdgeMargin * 2 && Main.maxTilesY > WorldEdgeMargin * 2;

        internal static bool TryResolveNear(Vector2 origin, out Vector2 anchor) {
            anchor = Vector2.Zero;
            if (!WorldGeometryReady || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)) {
                return false;
            }

            if (TryFindGround(origin, LocalSearchRadius, LocalSearchDepth, RequiredClearance, out anchor)
                || TryFindGround(origin, LocalSearchRadius, LocalSearchDepth, 2, out anchor)) {
                return true;
            }

            Vector2 spawn = new(Main.spawnTileX * 16f + 8f, Main.spawnTileY * 16f);
            int remainingDepth = Math.Max(Main.maxTilesY - Main.spawnTileY - WorldEdgeMargin, 1);
            if (TryFindGround(spawn, LocalSearchRadius * 2, remainingDepth, RequiredClearance, out anchor)
                || TryFindGround(spawn, LocalSearchRadius * 2, remainingDepth, 1, out anchor)) {
                return true;
            }

            int centerX = Math.Clamp(Main.maxTilesX / 2, WorldEdgeMargin,
                Main.maxTilesX - WorldEdgeMargin);
            int surfaceY = double.IsFinite(Main.worldSurface)
                ? (int)Math.Round(Main.worldSurface)
                : Main.maxTilesY / 3;
            Vector2 surface = new(centerX * 16f + 8f,
                Math.Clamp(surfaceY, WorldEdgeMargin, Main.maxTilesY - WorldEdgeMargin) * 16f);
            return TryFindGround(surface, LocalSearchRadius * 2, Main.maxTilesY,
                1, out anchor);
        }

        internal static bool IsValidWorldPosition(Vector2 position) {
            if (!WorldGeometryReady || !float.IsFinite(position.X) || !float.IsFinite(position.Y)) {
                return false;
            }

            float margin = WorldEdgeMargin * 16f;
            return position.X >= margin && position.X <= Main.maxTilesX * 16f - margin
                && position.Y >= margin && position.Y <= Main.maxTilesY * 16f - margin;
        }

        private static bool TryFindGround(Vector2 origin, int horizontalRadius, int maxDrop,
            int clearance, out Vector2 anchor) {
            anchor = Vector2.Zero;
            int originX = Math.Clamp((int)(origin.X / 16f), WorldEdgeMargin,
                Main.maxTilesX - WorldEdgeMargin);
            int originY = Math.Clamp((int)(origin.Y / 16f), WorldEdgeMargin,
                Main.maxTilesY - WorldEdgeMargin);
            int endY = Math.Min(originY + Math.Max(maxDrop, 1),
                Main.maxTilesY - WorldEdgeMargin);

            int bestScore = int.MaxValue;
            int bestX = -1;
            int bestY = -1;
            for (int dx = -horizontalRadius; dx <= horizontalRadius; dx++) {
                int x = originX + dx;
                if (x < WorldEdgeMargin || x >= Main.maxTilesX - WorldEdgeMargin) {
                    continue;
                }

                for (int y = originY; y <= endY; y++) {
                    if (!IsUsableGround(x, y, clearance)) {
                        continue;
                    }

                    int score = Math.Abs(dx) * 3 + y - originY;
                    if (score < bestScore) {
                        bestScore = score;
                        bestX = x;
                        bestY = y;
                    }
                    break;
                }
            }

            if (bestX < 0) {
                return false;
            }

            anchor = new Vector2(bestX * 16f + 8f, bestY * 16f);
            return IsValidWorldPosition(anchor);
        }

        private static bool IsUsableGround(int tileX, int tileY, int clearance) {
            if (!WorldGen.InWorld(tileX, tileY, WorldEdgeMargin)) {
                return false;
            }

            Tile ground = Main.tile[tileX, tileY];
            if (ground == null || !ground.HasSolidTile()) {
                return false;
            }

            for (int x = tileX - RequiredHalfWidth; x <= tileX + RequiredHalfWidth; x++) {
                for (int y = tileY - clearance; y < tileY; y++) {
                    if (!WorldGen.InWorld(x, y, WorldEdgeMargin)) {
                        return false;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile != null && tile.HasSolidTile()) {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
