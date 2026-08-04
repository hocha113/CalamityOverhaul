using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using InnoVault.GameSystem;
using InnoVault.TileProcessors;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>所有 NPC 时停来源的统一 AI 入口</summary>
    internal class WorldFreezeOverNPC : NPCOverride
    {
        public override int TargetID => -1;

        public override bool AI() {
            if (TimeFreezeSystem.FreezeNPCPreAI(npc)) {
                DyeGlobalNPC.ClearUpdateContext();
                return false;
            }

            if (npc.Alives()) {
                bool? result = npc.GetGlobalNPC<HackEffectNPC>().PreAIByOverNPC(npc);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return true;
        }
    }

    /// <summary>NPC 逐实体冻结快照</summary>
    internal class TimeFreezeNPC : GlobalNPC
    {
        private EntityFreezeState freezeState = new();
        private ulong entityGeneration;
        private int frozenDirection;
        private int frozenAIAction;
        private double frozenFrameCounter;

        public override bool InstancePerEntity => true;

        internal bool IsFrozen => freezeState.IsFrozen;
        internal bool HasTimeControl => freezeState.HasTimeControl;
        internal Vector2 ResumeVelocity => freezeState.ResumeVelocity;
        internal Vector2 EffectiveResumeVelocity => freezeState.EffectiveResumeVelocity;

        public override GlobalNPC Clone(NPC from, NPC to) {
            TimeFreezeNPC clone = (TimeFreezeNPC)base.Clone(from, to);
            clone.freezeState = new EntityFreezeState();
            clone.ResetFreezeState();
            return clone;
        }

        public override void SetDefaults(NPC npc) => ResetFreezeState();

        public override void OnSpawn(NPC npc, IEntitySource source) {
            if (WorldFreezeSystem.IsActive && WorldFreezeSystem.ShouldFreezeNPC(npc)) {
                BeginWorldFreeze(npc);
            }
        }

        internal void SyncTransientSources(NPC npc, TimeFreezeSource sources) {
            bool wasFrozen = IsFrozen;
            bool restored = freezeState.SyncTransientSources(npc, sources, npc.oldPosition);
            CapturePoseOnEnter(npc, wasFrozen);
            MarkNetUpdateOnRestore(npc, restored);
        }

        internal void AddTransientSource(NPC npc, TimeFreezeSource source) {
            bool wasFrozen = IsFrozen;
            freezeState.AddTransientSource(npc, source, npc.oldPosition);
            CapturePoseOnEnter(npc, wasFrozen);
        }

        internal void RefreshTimedSource(NPC npc, Type source, int ticks) {
            bool wasFrozen = IsFrozen;
            freezeState.RefreshTimedSource(npc, source, ticks, npc.oldPosition);
            CapturePoseOnEnter(npc, wasFrozen);
        }

        internal void BeginWorldFreeze(NPC npc) => AddTransientSource(npc, TimeFreezeSource.World);

        internal void EndWorldFreeze(NPC npc) {
            bool restored = freezeState.RemoveTransientSource(npc,
                TimeFreezeSource.World, npc.oldPosition);
            MarkNetUpdateOnRestore(npc, restored);
        }

        internal TimeFreezeLease Acquire(NPC npc, FreezeSourceKey source,
            Vector2? anchorCenter, int anchorPriority) {
            bool wasFrozen = IsFrozen;
            Vector2? anchorPosition = anchorCenter.HasValue
                ? anchorCenter.Value - new Vector2(npc.width, npc.height) * 0.5f
                : null;
            TimeFreezeLease lease = freezeState.Acquire(npc, source, entityGeneration,
                npc.oldPosition, anchorPosition, anchorPriority);
            CapturePoseOnEnter(npc, wasFrozen);
            return lease;
        }

        internal void Release(NPC npc, TimeFreezeLease lease, Vector2? releaseVelocity,
            int resumePriority) {
            if (lease.EntityGeneration != entityGeneration) {
                return;
            }
            bool restored = freezeState.Release(npc, lease.Source, npc.oldPosition,
                releaseVelocity, resumePriority);
            MarkNetUpdateOnRestore(npc, restored);
        }

        internal bool IsLeaseActive(TimeFreezeLease lease)
            => lease.EntityGeneration == entityGeneration
            && freezeState.IsHeld(lease.Source);

        internal void AcquireVelocityScale(NPC npc, FreezeSourceKey source, float scale)
            => freezeState.AcquireVelocityScale(npc, source, scale, npc.oldPosition);

        internal void ReleaseVelocityScale(NPC npc, FreezeSourceKey source) {
            if (freezeState.ReleaseVelocityScale(npc, source, npc.oldPosition)
                && Main.netMode != NetmodeID.MultiplayerClient) {
                npc.netUpdate = true;
            }
        }

        internal void FreezeFrame(NPC npc) {
            freezeState.HoldPosition(npc);
            npc.direction = frozenDirection;
            npc.aiAction = frozenAIAction;
            npc.frameCounter = frozenFrameCounter;
            if (npc.timeLeft < int.MaxValue) {
                npc.timeLeft++;
            }
        }

        internal void FreezeImmediately(NPC npc) {
            freezeState.HoldPosition(npc);
            npc.direction = frozenDirection;
            npc.aiAction = frozenAIAction;
            npc.frameCounter = frozenFrameCounter;
        }

        internal void ResetFreezeState() {
            freezeState.Reset();
            entityGeneration = TimeFreezeSystem.AllocateEntityGeneration();
            frozenDirection = 0;
            frozenAIAction = 0;
            frozenFrameCounter = 0d;
        }

        private void CapturePoseOnEnter(NPC npc, bool wasFrozen) {
            if (wasFrozen || !IsFrozen) {
                return;
            }
            frozenDirection = npc.direction;
            frozenAIAction = npc.aiAction;
            frozenFrameCounter = npc.frameCounter;
        }

        private static void MarkNetUpdateOnRestore(NPC npc, bool restored) {
            if (restored && Main.netMode != NetmodeID.MultiplayerClient) {
                npc.netUpdate = true;
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
            => !IsFrozen;

        public override bool CanHitNPC(NPC npc, NPC target)
            => !IsFrozen;
    }

    /// <summary>弹幕逐实体冻结快照与 AI 拦截</summary>
    internal class TimeFreezeProjectile : GlobalProjectile
    {
        private EntityFreezeState freezeState = new();
        private ulong entityGeneration;
        private bool spawnedDuringWorldFreeze;

        public override bool InstancePerEntity => true;

        internal bool IsFrozen => freezeState.IsFrozen;
        internal bool HasTimeControl => freezeState.HasTimeControl;
        internal Vector2 ResumeVelocity => freezeState.ResumeVelocity;
        internal Vector2 EffectiveResumeVelocity => freezeState.EffectiveResumeVelocity;

        public override GlobalProjectile Clone(Projectile from, Projectile to) {
            TimeFreezeProjectile clone = (TimeFreezeProjectile)base.Clone(from, to);
            clone.freezeState = new EntityFreezeState();
            clone.ResetFreezeState();
            return clone;
        }

        public override void SetDefaults(Projectile projectile) => ResetFreezeState();

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if ((WorldFreezeSystem.IsActive || WorldFreezeSystem.IsThawing)
                && WorldFreezeSystem.ShouldFreezeProjectile(projectile)) {
                BeginWorldFreeze(projectile, spawnedDuringFreeze: true);
            }
        }

        public override bool PreAI(Projectile projectile)
            => !TimeFreezeSystem.FreezeProjectilePreAI(projectile);

        public override bool ShouldUpdatePosition(Projectile projectile) => !IsFrozen;

        internal void SyncTransientSources(Projectile projectile, TimeFreezeSource sources) {
            bool restored = freezeState.SyncTransientSources(projectile, sources,
                projectile.oldPosition);
            MarkNetUpdateOnRestore(projectile, restored);
        }

        internal void RefreshTimedSource(Projectile projectile, Type source, int ticks) {
            freezeState.RefreshTimedSource(projectile, source, ticks, projectile.oldPosition);
        }

        internal void BeginWorldFreeze(Projectile projectile, bool spawnedDuringFreeze) {
            freezeState.AddTransientSource(projectile, TimeFreezeSource.World,
                projectile.oldPosition);
            spawnedDuringWorldFreeze |= spawnedDuringFreeze;
        }

        internal bool PrepareWorldThaw(Projectile projectile) {
            if (spawnedDuringWorldFreeze) {
                ResetFreezeState();
                return true;
            }
            bool restored = freezeState.RemoveTransientSource(projectile,
                TimeFreezeSource.World, projectile.oldPosition);
            MarkNetUpdateOnRestore(projectile, restored);
            return false;
        }

        internal TimeFreezeLease Acquire(Projectile projectile, FreezeSourceKey source,
            Vector2? anchorCenter, int anchorPriority) {
            Vector2? anchorPosition = anchorCenter.HasValue
                ? anchorCenter.Value - new Vector2(projectile.width, projectile.height) * 0.5f
                : null;
            return freezeState.Acquire(projectile, source, entityGeneration,
                projectile.oldPosition, anchorPosition, anchorPriority);
        }

        internal void Release(Projectile projectile, TimeFreezeLease lease,
            Vector2? releaseVelocity, int resumePriority) {
            if (lease.EntityGeneration != entityGeneration) {
                return;
            }
            bool restored = freezeState.Release(projectile, lease.Source,
                projectile.oldPosition, releaseVelocity, resumePriority);
            MarkNetUpdateOnRestore(projectile, restored);
        }

        internal bool IsLeaseActive(TimeFreezeLease lease)
            => lease.EntityGeneration == entityGeneration
            && freezeState.IsHeld(lease.Source);

        internal void AcquireVelocityScale(Projectile projectile, FreezeSourceKey source,
            float scale)
            => freezeState.AcquireVelocityScale(projectile, source, scale,
                projectile.oldPosition);

        internal void ReleaseVelocityScale(Projectile projectile, FreezeSourceKey source) {
            bool restored = freezeState.ReleaseVelocityScale(projectile, source,
                projectile.oldPosition);
            MarkNetUpdateOnRestore(projectile, restored);
        }

        internal void FreezeFrame(Projectile projectile) {
            freezeState.HoldPosition(projectile);
            if (projectile.timeLeft < int.MaxValue) {
                projectile.timeLeft++;
            }
        }

        internal void FreezeImmediately(Projectile projectile)
            => freezeState.HoldPosition(projectile);

        internal void ResetFreezeState() {
            freezeState.Reset();
            entityGeneration = TimeFreezeSystem.AllocateEntityGeneration();
            spawnedDuringWorldFreeze = false;
        }

        private static void MarkNetUpdateOnRestore(Projectile projectile, bool restored) {
            if (restored && (Main.netMode != NetmodeID.MultiplayerClient
                || projectile.owner == Main.myPlayer)) {
                projectile.netUpdate = true;
            }
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
            => IsFrozen ? false : null;

        public override bool CanHitPlayer(Projectile projectile, Player target)
            => !IsFrozen;

        public override bool CanHitPvp(Projectile projectile, Player target)
            => !IsFrozen;
    }

    internal class WorldFreezeTileProcessor : GlobalTileProcessor
    {
        public override bool PreSingleInstanceUpdate(TileProcessor tileProcessor) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return base.PreSingleInstanceUpdate(tileProcessor);
        }

        public override bool PreUpdate(TileProcessor tileProcessor) {
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return base.PreUpdate(tileProcessor);
        }
    }
}
