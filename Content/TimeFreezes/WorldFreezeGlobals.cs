using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using InnoVault.GameSystem;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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
        private ulong networkGeneration;
        private int frozenDirection;
        private int frozenSpriteDirection;
        private int frozenAIAction;
        private double frozenFrameCounter;
        private Rectangle frozenFrame;
        private float frozenRotation;
        private int logicalDirection;
        private int logicalSpriteDirection;
        private int logicalAIAction;
        private double logicalFrameCounter;
        private Rectangle logicalFrame;
        private float logicalRotation;
        private Vector2 renderPosition;
        private bool renderPositionApplied;

        public override bool InstancePerEntity => true;

        internal bool IsFrozen => freezeState.IsFrozen;
        internal bool HasTimeScale => freezeState.HasTimeScale;
        internal bool HasTimeControl => freezeState.HasTimeControl;
        internal Vector2 ResumeVelocity => freezeState.ResumeVelocity;
        internal Vector2 EffectiveResumeVelocity => freezeState.EffectiveResumeVelocity;
        internal float EffectiveTimeScale => freezeState.EffectiveTimeScale;
        internal ulong NetworkGeneration => networkGeneration;

        public override GlobalNPC Clone(NPC from, NPC to) {
            TimeFreezeNPC clone = (TimeFreezeNPC)base.Clone(from, to);
            clone.freezeState = new EntityFreezeState();
            clone.ResetFreezeState();
            return clone;
        }

        public override void SetDefaults(NPC npc) => ResetFreezeState();

        public override void OnSpawn(NPC npc, IEntitySource source) {
            EnsureNetworkGeneration(npc);
            if (WorldFreezeSystem.IsActive && WorldFreezeSystem.ShouldFreezeNPC(npc)) {
                BeginWorldFreeze(npc);
            }
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter,
            BinaryWriter binaryWriter) {
            EnsureNetworkGeneration(npc);
            binaryWriter.Write(networkGeneration);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader,
            BinaryReader binaryReader) {
            ulong receivedGeneration = binaryReader.ReadUInt64();
            if (Main.netMode != NetmodeID.MultiplayerClient
                || receivedGeneration == 0
                || receivedGeneration == networkGeneration) {
                return;
            }

            ResetLocalControlState();
            networkGeneration = receivedGeneration;
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
            bool restored = freezeState.Release(npc, lease, npc.oldPosition,
                releaseVelocity, resumePriority);
            MarkNetUpdateOnRestore(npc, restored);
        }

        internal bool IsLeaseActive(TimeFreezeLease lease)
            => lease.EntityGeneration == entityGeneration
            && freezeState.IsHeld(lease);

        internal bool SetTimeScale(NPC npc, FreezeSourceKey source, float scale) {
            bool hadTimeScale = HasTimeScale;
            bool changed = freezeState.SetTimeScale(npc, source, scale, npc.oldPosition);
            if (!hadTimeScale && HasTimeScale) {
                CaptureLogicalPose(npc);
            }
            return changed;
        }

        internal bool ClearTimeScale(NPC npc, FreezeSourceKey source) {
            bool cleared = freezeState.ClearTimeScale(npc, source, npc.oldPosition);
            if (cleared
                && Main.netMode != NetmodeID.MultiplayerClient) {
                npc.netUpdate = true;
            }
            return cleared;
        }

        internal bool TryGetRenderPosition(NPC npc, out Vector2 position)
            => freezeState.TryGetRenderPosition(npc, out position);

        internal void FreezeFrame(NPC npc) {
            freezeState.BlockHardFrame(npc);
            ApplyFrozenPose(npc);
            if (freezeState.TryCompensateLifetime() && npc.timeLeft < int.MaxValue) {
                npc.timeLeft++;
                //增益计时与寿命同口径冻住：原版在 AI 前已把 buffTime 递减，这里补回一帧，
                //烧伤等 DoT 不在停格里白白到期，解冻后照常烧完（与 UpdateLifeRegen 归零配对）
                for (int i = 0; i < NPC.maxBuffs; i++) {
                    if (npc.buffType[i] > 0 && npc.buffTime[i] > 0 && npc.buffTime[i] < int.MaxValue) {
                        npc.buffTime[i]++;
                    }
                }
            }
        }

        /// <summary>冻结期 DoT 不结算：原版在 AI 之前就走 lifeRegen，AI 拦截拦不到，
        /// 时停里 Boss 会被灼烧类减益持续烧血（反馈六 #77，鬼切终斩/点鬼簿冻结与鬼伞重启同病）；
        /// 与玩家侧 WorldFreezePlayer 冻住自身回血/增益计时的口径对称，总伤不变只是顺延</summary>
        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!freezeState.IsFrozen) {
                return;
            }
            npc.lifeRegen = 0;
            damage = 0;
        }

        internal void FreezeImmediately(NPC npc) {
            freezeState.HoldPosition(npc);
            ApplyFrozenPose(npc);
        }

        internal bool ApplyTimeScaleFrame(NPC npc) {
            if (IsFrozen || freezeState.ShouldAdvanceTimeStep(npc)) {
                return false;
            }
            bool holdFrozenPose = freezeState.BlockTimeStepFrame(npc);
            if (holdFrozenPose) {
                ApplyFrozenPose(npc);
            }
            else {
                ApplyLogicalPose(npc);
            }
            if (freezeState.TryCompensateLifetime() && npc.timeLeft < int.MaxValue) {
                npc.timeLeft++;
            }
            return true;
        }

        internal void CompleteFrame(NPC npc) {
            switch (freezeState.CompleteFrame(npc)) {
                case TimeControlFrameAction.CaptureLogicalPose:
                    CaptureLogicalPose(npc);
                    break;
                case TimeControlFrameAction.HoldLogicalPose:
                    ApplyLogicalPose(npc);
                    npc.oldPosition = npc.position;
                    break;
                case TimeControlFrameAction.HoldFrozenPose:
                    ApplyFrozenPose(npc);
                    npc.oldPosition = npc.position;
                    break;
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch,
            Vector2 screenPos, Color drawColor) {
            if (renderPositionApplied) {
                npc.position = renderPosition;
                renderPositionApplied = false;
            }
            if (TimeFreezeSystem.TryGetRenderPosition(npc,
                out Vector2 interpolatedPosition)) {
                renderPosition = npc.position;
                npc.position = interpolatedPosition;
                renderPositionApplied = true;
            }
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch,
            Vector2 screenPos, Color drawColor) {
            if (!renderPositionApplied) {
                return;
            }
            npc.position = renderPosition;
            renderPositionApplied = false;
        }

        internal void ResetFreezeState() {
            ResetLocalControlState();
            networkGeneration = 0;
        }

        private void ResetLocalControlState() {
            freezeState.Reset();
            entityGeneration = TimeFreezeSystem.AllocateEntityGeneration();
            frozenDirection = 0;
            frozenSpriteDirection = 0;
            frozenAIAction = 0;
            frozenFrameCounter = 0d;
            frozenFrame = Rectangle.Empty;
            frozenRotation = 0f;
            logicalDirection = 0;
            logicalSpriteDirection = 0;
            logicalAIAction = 0;
            logicalFrameCounter = 0d;
            logicalFrame = Rectangle.Empty;
            logicalRotation = 0f;
        }

        private void CapturePoseOnEnter(NPC npc, bool wasFrozen) {
            if (wasFrozen || !IsFrozen) {
                return;
            }
            frozenDirection = npc.direction;
            frozenSpriteDirection = npc.spriteDirection;
            frozenAIAction = npc.aiAction;
            frozenFrameCounter = double.IsFinite(npc.frameCounter)
                ? npc.frameCounter
                : 0d;
            frozenFrame = npc.frame;
            frozenRotation = float.IsFinite(npc.rotation) ? npc.rotation : 0f;
        }

        private void ApplyFrozenPose(NPC npc) {
            npc.direction = frozenDirection;
            npc.spriteDirection = frozenSpriteDirection;
            npc.aiAction = frozenAIAction;
            npc.frameCounter = frozenFrameCounter;
            npc.frame = frozenFrame;
            npc.rotation = frozenRotation;
        }

        private void CaptureLogicalPose(NPC npc) {
            logicalDirection = npc.direction;
            logicalSpriteDirection = npc.spriteDirection;
            logicalAIAction = npc.aiAction;
            logicalFrameCounter = double.IsFinite(npc.frameCounter)
                ? npc.frameCounter
                : 0d;
            logicalFrame = npc.frame;
            logicalRotation = float.IsFinite(npc.rotation) ? npc.rotation : 0f;
        }

        private void ApplyLogicalPose(NPC npc) {
            npc.direction = logicalDirection;
            npc.spriteDirection = logicalSpriteDirection;
            npc.aiAction = logicalAIAction;
            npc.frameCounter = logicalFrameCounter;
            npc.frame = logicalFrame;
            npc.rotation = logicalRotation;
        }

        internal ulong EnsureNetworkGeneration(NPC npc = null) {
            if (networkGeneration == 0
                && Main.netMode != NetmodeID.MultiplayerClient) {
                networkGeneration = TimeFreezeSystem.AllocateNetworkGeneration();
                if (Main.netMode == NetmodeID.Server && npc?.active == true) {
                    npc.netUpdate = true;
                }
            }
            return networkGeneration;
        }

        private static void MarkNetUpdateOnRestore(NPC npc, bool restored) {
            if (restored && Main.netMode != NetmodeID.MultiplayerClient) {
                npc.netUpdate = true;
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
            => !freezeState.BlocksInteraction;

        public override bool CanHitNPC(NPC npc, NPC target)
            => !freezeState.BlocksInteraction;
    }

    /// <summary>弹幕逐实体冻结快照与 AI 拦截</summary>
    internal class TimeFreezeProjectile : GlobalProjectile
    {
        private EntityFreezeState freezeState = new();
        private ulong entityGeneration;
        private bool spawnedDuringWorldFreeze;
        private bool beingKilledDuringWorldThaw;
        private int frozenFrame;
        private int frozenSpriteDirection;
        private float frozenRotation;
        private int logicalFrame;
        private int logicalSpriteDirection;
        private float logicalRotation;
        private Vector2 renderPosition;
        private bool renderPositionApplied;

        public override bool InstancePerEntity => true;

        internal bool IsFrozen => beingKilledDuringWorldThaw || freezeState.IsFrozen;
        internal bool HasTimeScale => freezeState.HasTimeScale;
        internal bool HasTimeControl => freezeState.HasTimeControl;
        internal Vector2 ResumeVelocity => freezeState.ResumeVelocity;
        internal Vector2 EffectiveResumeVelocity => freezeState.EffectiveResumeVelocity;
        internal float EffectiveTimeScale => freezeState.EffectiveTimeScale;
        internal ulong EntityGeneration => entityGeneration;
        private bool BlocksInteraction => beingKilledDuringWorldThaw
            || freezeState.BlocksInteraction;

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

        public override bool ShouldUpdatePosition(Projectile projectile)
            => !BlocksInteraction;

        internal void SyncTransientSources(Projectile projectile, TimeFreezeSource sources) {
            bool wasFrozen = IsFrozen;
            bool restored = freezeState.SyncTransientSources(projectile, sources,
                projectile.oldPosition);
            CapturePoseOnEnter(projectile, wasFrozen);
            MarkNetUpdateOnRestore(projectile, restored);
        }

        internal void RefreshTimedSource(Projectile projectile, Type source, int ticks) {
            bool wasFrozen = IsFrozen;
            freezeState.RefreshTimedSource(projectile, source, ticks, projectile.oldPosition);
            CapturePoseOnEnter(projectile, wasFrozen);
        }

        internal void BeginWorldFreeze(Projectile projectile, bool spawnedDuringFreeze) {
            bool wasFrozen = IsFrozen;
            freezeState.AddTransientSource(projectile, TimeFreezeSource.World,
                projectile.oldPosition);
            CapturePoseOnEnter(projectile, wasFrozen);
            spawnedDuringWorldFreeze |= spawnedDuringFreeze;
        }

        internal bool PrepareWorldThaw(Projectile projectile, bool spawnedAfterFreezeStart) {
            if (spawnedDuringWorldFreeze || spawnedAfterFreezeStart) {
                beingKilledDuringWorldThaw = true;
                freezeState.AddTransientSource(projectile, TimeFreezeSource.World,
                    projectile.oldPosition);
                freezeState.HoldPosition(projectile);
                return true;
            }
            bool restored = freezeState.RemoveTransientSource(projectile,
                TimeFreezeSource.World, projectile.oldPosition);
            MarkNetUpdateOnRestore(projectile, restored);
            return false;
        }

        internal void CancelWorldFreeze(Projectile projectile) {
            bool restored = freezeState.RemoveTransientSource(projectile,
                TimeFreezeSource.World, projectile.oldPosition);
            spawnedDuringWorldFreeze = false;
            beingKilledDuringWorldThaw = false;
            MarkNetUpdateOnRestore(projectile, restored);
        }

        internal TimeFreezeLease Acquire(Projectile projectile, FreezeSourceKey source,
            Vector2? anchorCenter, int anchorPriority) {
            bool wasFrozen = IsFrozen;
            Vector2? anchorPosition = anchorCenter.HasValue
                ? anchorCenter.Value - new Vector2(projectile.width, projectile.height) * 0.5f
                : null;
            TimeFreezeLease lease = freezeState.Acquire(projectile, source, entityGeneration,
                projectile.oldPosition, anchorPosition, anchorPriority);
            CapturePoseOnEnter(projectile, wasFrozen);
            return lease;
        }

        internal void Release(Projectile projectile, TimeFreezeLease lease,
            Vector2? releaseVelocity, int resumePriority) {
            if (lease.EntityGeneration != entityGeneration) {
                return;
            }
            bool restored = freezeState.Release(projectile, lease,
                projectile.oldPosition, releaseVelocity, resumePriority);
            MarkNetUpdateOnRestore(projectile, restored);
        }

        internal bool IsLeaseActive(TimeFreezeLease lease)
            => lease.EntityGeneration == entityGeneration
            && freezeState.IsHeld(lease);

        internal bool SetTimeScale(Projectile projectile, FreezeSourceKey source,
            float scale) {
            bool hadTimeScale = HasTimeScale;
            bool changed = freezeState.SetTimeScale(projectile, source, scale,
                projectile.oldPosition);
            if (!hadTimeScale && HasTimeScale) {
                CaptureLogicalPose(projectile);
            }
            return changed;
        }

        internal bool ClearTimeScale(Projectile projectile, FreezeSourceKey source) {
            bool cleared = freezeState.ClearTimeScale(projectile, source,
                projectile.oldPosition);
            MarkNetUpdateOnRestore(projectile, cleared);
            return cleared;
        }

        internal bool TryGetRenderPosition(Projectile projectile, out Vector2 position)
            => freezeState.TryGetRenderPosition(projectile, out position);

        internal void FreezeFrame(Projectile projectile) {
            freezeState.BlockHardFrame(projectile);
            ApplyFrozenPose(projectile);
            if (freezeState.TryCompensateLifetime()) {
                CompensateLifetime(projectile);
            }
        }

        internal void FreezeImmediately(Projectile projectile) {
            freezeState.HoldPosition(projectile);
            ApplyFrozenPose(projectile);
        }

        internal bool ApplyTimeScaleFrame(Projectile projectile) {
            if (IsFrozen || freezeState.ShouldAdvanceTimeStep(projectile)) {
                return false;
            }
            bool holdFrozenPose = freezeState.BlockTimeStepFrame(projectile);
            if (holdFrozenPose) {
                ApplyFrozenPose(projectile);
            }
            else {
                ApplyLogicalPose(projectile);
            }
            if (freezeState.TryCompensateLifetime()) {
                CompensateLifetime(projectile);
            }
            return true;
        }

        internal void CompleteFrame(Projectile projectile) {
            switch (freezeState.CompleteFrame(projectile)) {
                case TimeControlFrameAction.CaptureLogicalPose:
                    CaptureLogicalPose(projectile);
                    break;
                case TimeControlFrameAction.HoldLogicalPose:
                    ApplyLogicalPose(projectile);
                    projectile.oldPosition = projectile.position;
                    break;
                case TimeControlFrameAction.HoldFrozenPose:
                    ApplyFrozenPose(projectile);
                    projectile.oldPosition = projectile.position;
                    break;
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) {
            if (renderPositionApplied) {
                projectile.position = renderPosition;
                renderPositionApplied = false;
            }
            if (TimeFreezeSystem.TryGetRenderPosition(projectile,
                out Vector2 interpolatedPosition)) {
                renderPosition = projectile.position;
                projectile.position = interpolatedPosition;
                renderPositionApplied = true;
            }
            return true;
        }

        public override void PostDraw(Projectile projectile, Color lightColor) {
            if (!renderPositionApplied) {
                return;
            }
            projectile.position = renderPosition;
            renderPositionApplied = false;
        }

        internal void ResetFreezeState() {
            freezeState.Reset();
            entityGeneration = TimeFreezeSystem.AllocateEntityGeneration();
            spawnedDuringWorldFreeze = false;
            beingKilledDuringWorldThaw = false;
            frozenFrame = 0;
            frozenSpriteDirection = 0;
            frozenRotation = 0f;
            logicalFrame = 0;
            logicalSpriteDirection = 0;
            logicalRotation = 0f;
        }

        private void CapturePoseOnEnter(Projectile projectile, bool wasFrozen) {
            if (wasFrozen || !IsFrozen) {
                return;
            }
            frozenFrame = projectile.frame;
            frozenSpriteDirection = projectile.spriteDirection;
            frozenRotation = float.IsFinite(projectile.rotation)
                ? projectile.rotation
                : 0f;
        }

        private void ApplyFrozenPose(Projectile projectile) {
            projectile.frame = frozenFrame;
            projectile.spriteDirection = frozenSpriteDirection;
            projectile.rotation = frozenRotation;
        }

        private void CaptureLogicalPose(Projectile projectile) {
            logicalFrame = projectile.frame;
            logicalSpriteDirection = projectile.spriteDirection;
            logicalRotation = float.IsFinite(projectile.rotation)
                ? projectile.rotation
                : 0f;
        }

        private void ApplyLogicalPose(Projectile projectile) {
            projectile.frame = logicalFrame;
            projectile.spriteDirection = logicalSpriteDirection;
            projectile.rotation = logicalRotation;
        }

        private static void CompensateLifetime(Projectile projectile) {
            long updates = Math.Clamp((long)projectile.extraUpdates + 1L,
                1L, int.MaxValue);
            projectile.timeLeft = (int)Math.Min((long)int.MaxValue,
                (long)projectile.timeLeft + updates);
        }

        private static void MarkNetUpdateOnRestore(Projectile projectile, bool restored) {
            if (restored && (Main.netMode != NetmodeID.MultiplayerClient
                || projectile.owner == Main.myPlayer)) {
                projectile.netUpdate = true;
            }
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
            => BlocksInteraction ? false : null;

        public override bool? CanDamage(Projectile projectile)
            => BlocksInteraction ? false : null;

        public override bool CanHitPlayer(Projectile projectile, Player target)
            => !BlocksInteraction;

        public override bool CanHitPvp(Projectile projectile, Player target)
            => !BlocksInteraction;
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
