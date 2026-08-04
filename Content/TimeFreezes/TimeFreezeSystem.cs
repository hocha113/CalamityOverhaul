using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    [Flags]
    internal enum TimeFreezeSource : byte
    {
        None = 0,
        World = 1 << 0,
        Cinematic = 1 << 1,
    }

    internal enum TimeFreezeAnchorPriority
    {
        Default = 0,
        Effect = 100,
        Authoritative = 200,
    }

    internal enum TimeFreezeResumePriority
    {
        Default = 0,
        Domain = 100,
    }

    internal readonly record struct FreezeSourceKey(Type SourceType, long InstanceId);

    internal readonly struct TimeFreezeLease
    {
        internal FreezeSourceKey Source { get; }
        internal ulong EntityGeneration { get; }
        internal ulong LeaseEpoch { get; }
        internal Vector2 ResumeVelocity { get; }
        internal bool IsValid => Source.SourceType != null && EntityGeneration != 0
            && LeaseEpoch != 0;

        internal TimeFreezeLease(FreezeSourceKey source, ulong entityGeneration,
            ulong leaseEpoch, Vector2 resumeVelocity) {
            Source = source;
            EntityGeneration = entityGeneration;
            LeaseEpoch = leaseEpoch;
            ResumeVelocity = resumeVelocity;
        }
    }

    /// <summary>NPC / 弹幕共享的冻结来源与运动快照</summary>
    internal sealed class EntityFreezeState
    {
        private sealed class HeldSource
        {
            internal Vector2? AnchorPosition;
            internal int AnchorPriority;
            internal ulong LeaseEpoch;
        }

        private readonly record struct ResumePolicy(Vector2 Velocity, int Priority);

        private TimeFreezeSource transientSources;
        private Dictionary<Type, ulong> timedSources;
        private Dictionary<FreezeSourceKey, HeldSource> heldSources;
        private Dictionary<FreezeSourceKey, ResumePolicy> resumePolicies;
        private Dictionary<FreezeSourceKey, float> velocityScales;
        private bool motionSnapshotCaptured;
        private Vector2 capturedVelocity;
        private bool hardPositionCaptured;
        private Vector2 baseFrozenPosition;
        private Vector2 frozenPosition;

        internal bool IsFrozen => transientSources != TimeFreezeSource.None
            || timedSources?.Count > 0 || heldSources?.Count > 0;
        internal bool HasVelocityScale => velocityScales?.Count > 0;
        internal bool HasTimeControl => IsFrozen || HasVelocityScale;
        internal TimeFreezeSource TransientSources => transientSources;
        internal Vector2 ResumeVelocity => motionSnapshotCaptured
            ? ResolveResumeVelocity()
            : Vector2.Zero;
        internal Vector2 EffectiveResumeVelocity => motionSnapshotCaptured
            ? ResolveResumeVelocity() * ResolveVelocityScale()
            : Vector2.Zero;

        internal bool SyncTransientSources(Entity entity, TimeFreezeSource sources, Vector2 fallbackPosition) {
            bool wasFrozen = IsFrozen;
            bool hadTimeControl = HasTimeControl;
            RemoveExpiredTimedSources();
            transientSources = sources;
            return UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
        }

        internal void RefreshTimedSource(Entity entity, Type source, int ticks,
            Vector2 fallbackPosition) {
            if (source == null || ticks <= 0) {
                return;
            }

            bool wasFrozen = IsFrozen;
            bool hadTimeControl = HasTimeControl;
            RemoveExpiredTimedSources();
            timedSources ??= new Dictionary<Type, ulong>();
            ulong expiry = Main.GameUpdateCount + (ulong)ticks + 1UL;
            if (!timedSources.TryGetValue(source, out ulong oldExpiry) || expiry > oldExpiry) {
                timedSources[source] = expiry;
            }
            UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
        }

        internal bool AddTransientSource(Entity entity, TimeFreezeSource source, Vector2 fallbackPosition) {
            bool wasFrozen = IsFrozen;
            bool hadTimeControl = HasTimeControl;
            RemoveExpiredTimedSources();
            transientSources |= source;
            UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
            return !wasFrozen && IsFrozen;
        }

        internal bool RemoveTransientSource(Entity entity, TimeFreezeSource source, Vector2 fallbackPosition) {
            bool wasFrozen = IsFrozen;
            bool hadTimeControl = HasTimeControl;
            RemoveExpiredTimedSources();
            transientSources &= ~source;
            return UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
        }

        internal TimeFreezeLease Acquire(Entity entity, FreezeSourceKey source,
            ulong entityGeneration, Vector2 fallbackPosition, Vector2? anchorPosition,
            int anchorPriority) {
            if (source.SourceType == null) {
                return default;
            }

            bool wasFrozen = IsFrozen;
            bool hadTimeControl = HasTimeControl;
            RemoveExpiredTimedSources();
            heldSources ??= new Dictionary<FreezeSourceKey, HeldSource>();
            if (!heldSources.TryGetValue(source, out HeldSource held)) {
                held = new HeldSource {
                    LeaseEpoch = TimeFreezeSystem.AllocateLeaseEpoch(),
                };
                heldSources[source] = held;
            }
            held.AnchorPosition = anchorPosition.HasValue && IsFinite(anchorPosition.Value)
                ? anchorPosition
                : null;
            held.AnchorPriority = anchorPriority;
            resumePolicies?.Remove(source);
            UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
            return new TimeFreezeLease(source, entityGeneration, held.LeaseEpoch,
                ResumeVelocity);
        }

        internal bool Release(Entity entity, TimeFreezeLease lease, Vector2 fallbackPosition,
            Vector2? releaseVelocity, int resumePriority) {
            if (!lease.IsValid || heldSources == null
                || !heldSources.TryGetValue(lease.Source, out HeldSource held)
                || held.LeaseEpoch != lease.LeaseEpoch) {
                return false;
            }
            heldSources.Remove(lease.Source);

            bool wasFrozen = true;
            bool hadTimeControl = true;
            if (releaseVelocity.HasValue && IsFinite(releaseVelocity.Value)) {
                resumePolicies ??= new Dictionary<FreezeSourceKey, ResumePolicy>();
                resumePolicies[lease.Source] = new ResumePolicy(releaseVelocity.Value,
                    resumePriority);
            }
            bool restored = UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
            if (IsFrozen) {
                HoldPosition(entity);
            }
            return restored;
        }

        internal bool IsHeld(TimeFreezeLease lease)
            => lease.IsValid && heldSources != null
            && heldSources.TryGetValue(lease.Source, out HeldSource held)
            && held.LeaseEpoch == lease.LeaseEpoch;

        internal void AcquireVelocityScale(Entity entity, FreezeSourceKey source,
            float scale, Vector2 fallbackPosition) {
            if (source.SourceType == null || !float.IsFinite(scale)) {
                return;
            }

            bool wasFrozen = IsFrozen;
            bool hadTimeControl = HasTimeControl;
            RemoveExpiredTimedSources();
            velocityScales ??= new Dictionary<FreezeSourceKey, float>();
            velocityScales[source] = Math.Clamp(scale, 0f, 1f);
            UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
        }

        internal bool ReleaseVelocityScale(Entity entity, FreezeSourceKey source,
            Vector2 fallbackPosition) {
            if (source.SourceType == null || velocityScales == null
                || !velocityScales.Remove(source)) {
                return false;
            }

            bool wasFrozen = IsFrozen;
            bool hadTimeControl = true;
            UpdateState(entity, fallbackPosition, wasFrozen, hadTimeControl);
            return true;
        }

        internal void HoldPosition(Entity entity) {
            if (!hardPositionCaptured || !IsFrozen) {
                return;
            }
            entity.position = frozenPosition;
            entity.velocity = Vector2.Zero;
        }

        internal void Reset() {
            transientSources = TimeFreezeSource.None;
            timedSources?.Clear();
            heldSources?.Clear();
            resumePolicies?.Clear();
            velocityScales?.Clear();
            motionSnapshotCaptured = false;
            capturedVelocity = Vector2.Zero;
            hardPositionCaptured = false;
            baseFrozenPosition = Vector2.Zero;
            frozenPosition = Vector2.Zero;
        }

        private bool UpdateState(Entity entity, Vector2 fallbackPosition,
            bool wasFrozen, bool hadTimeControl) {
            bool isFrozen = IsFrozen;
            bool hasTimeControl = HasTimeControl;

            if (!hadTimeControl && hasTimeControl) {
                CaptureMotion(entity);
            }
            if (!wasFrozen && isFrozen) {
                CaptureHardPosition(entity, fallbackPosition);
            }
            if (isFrozen) {
                ResolveFrozenPosition();
            }

            bool hardFreezeEnded = wasFrozen && !isFrozen;
            if (hardFreezeEnded || (!isFrozen && hadTimeControl && !hasTimeControl)) {
                RestoreMotion(entity);
            }
            if (hardFreezeEnded) {
                hardPositionCaptured = false;
                baseFrozenPosition = Vector2.Zero;
                frozenPosition = Vector2.Zero;
            }
            if (hadTimeControl && !hasTimeControl) {
                ClearMotionSnapshot();
            }
            return hardFreezeEnded;
        }

        private void CaptureMotion(Entity entity) {
            capturedVelocity = IsFinite(entity.velocity) ? entity.velocity : Vector2.Zero;
            motionSnapshotCaptured = true;
        }

        private void CaptureHardPosition(Entity entity, Vector2 fallbackPosition) {
            baseFrozenPosition = IsFinite(entity.position)
                ? entity.position
                : IsFinite(fallbackPosition) ? fallbackPosition : Vector2.Zero;
            frozenPosition = baseFrozenPosition;
            hardPositionCaptured = true;
        }

        private void ResolveFrozenPosition() {
            if (!hardPositionCaptured) {
                return;
            }

            Vector2 resolved = baseFrozenPosition;
            int bestPriority = int.MinValue;
            FreezeSourceKey bestSource = default;
            bool hasAnchor = false;
            if (heldSources != null) {
                foreach (var pair in heldSources) {
                    HeldSource held = pair.Value;
                    if (!held.AnchorPosition.HasValue || !IsFinite(held.AnchorPosition.Value)) {
                        continue;
                    }
                    if (!hasAnchor || held.AnchorPriority > bestPriority
                        || held.AnchorPriority == bestPriority
                        && CompareSourceKeys(pair.Key, bestSource) > 0) {
                        resolved = held.AnchorPosition.Value;
                        bestPriority = held.AnchorPriority;
                        bestSource = pair.Key;
                        hasAnchor = true;
                    }
                }
            }
            frozenPosition = resolved;
        }

        private Vector2 ResolveResumeVelocity() {
            Vector2 resolved = IsFinite(capturedVelocity) ? capturedVelocity : Vector2.Zero;
            int bestPriority = int.MinValue;
            FreezeSourceKey bestSource = default;
            bool hasPolicy = false;
            if (resumePolicies != null) {
                foreach (var pair in resumePolicies) {
                    ResumePolicy policy = pair.Value;
                    if (!IsFinite(policy.Velocity)) {
                        continue;
                    }
                    if (!hasPolicy || policy.Priority > bestPriority
                        || policy.Priority == bestPriority
                        && CompareSourceKeys(pair.Key, bestSource) > 0) {
                        resolved = policy.Velocity;
                        bestPriority = policy.Priority;
                        bestSource = pair.Key;
                        hasPolicy = true;
                    }
                }
            }
            return resolved;
        }

        private float ResolveVelocityScale() {
            float scale = 1f;
            if (velocityScales == null) {
                return scale;
            }
            foreach (float value in velocityScales.Values) {
                if (float.IsFinite(value)) {
                    scale = Math.Min(scale, Math.Clamp(value, 0f, 1f));
                }
            }
            return scale;
        }

        private void RestoreMotion(Entity entity) {
            if (!motionSnapshotCaptured) {
                return;
            }
            if (!IsFinite(entity.position) && hardPositionCaptured) {
                entity.position = frozenPosition;
            }
            Vector2 velocity = EffectiveResumeVelocity;
            entity.velocity = IsFinite(velocity) ? velocity : Vector2.Zero;
        }

        private void ClearMotionSnapshot() {
            motionSnapshotCaptured = false;
            capturedVelocity = Vector2.Zero;
            resumePolicies?.Clear();
        }

        private void RemoveExpiredTimedSources() {
            if (timedSources == null || timedSources.Count == 0) {
                return;
            }

            ulong now = Main.GameUpdateCount;
            List<Type> expired = null;
            foreach (var pair in timedSources) {
                if (pair.Value <= now) {
                    expired ??= [];
                    expired.Add(pair.Key);
                }
            }
            if (expired == null) {
                return;
            }
            foreach (Type source in expired) {
                timedSources.Remove(source);
            }
        }

        private static int CompareSourceKeys(FreezeSourceKey left, FreezeSourceKey right) {
            int typeComparison = string.CompareOrdinal(left.SourceType?.FullName,
                right.SourceType?.FullName);
            return typeComparison != 0
                ? typeComparison
                : left.InstanceId.CompareTo(right.InstanceId);
        }

        internal static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    /// <summary>所有实体时停来源的统一入口</summary>
    internal static class TimeFreezeSystem
    {
        private readonly record struct ProjectileIdentity(ulong EntityGeneration,
            int Type, int Owner, int Identity);

        private static readonly Dictionary<Type, ulong> cinematicSources = new();
        private static ulong nextEntityGeneration;
        private static ulong nextLeaseEpoch;
        private static ProjectileIdentity[] worldFreezeProjectileBaseline;
        private static bool worldFreezeProjectileBaselineValid;

        internal static ulong AllocateEntityGeneration() {
            nextEntityGeneration++;
            if (nextEntityGeneration == 0) {
                nextEntityGeneration++;
            }
            return nextEntityGeneration;
        }

        internal static ulong AllocateLeaseEpoch() {
            nextLeaseEpoch++;
            if (nextLeaseEpoch == 0) {
                nextLeaseEpoch++;
            }
            return nextLeaseEpoch;
        }

        internal static bool IsCinematicFreezeActive {
            get {
                ulong now = Main.GameUpdateCount;
                foreach (ulong expiry in cinematicSources.Values) {
                    if (expiry > now) {
                        return true;
                    }
                }
                return false;
            }
        }
        internal static bool IsAnyGlobalFreezeActive
            => WorldFreezeSystem.IsActive || IsCinematicFreezeActive;

        internal static void RefreshCinematic<TSource>(int ticks) {
            if (ticks <= 0) {
                return;
            }
            Type source = typeof(TSource);
            ulong expiry = Main.GameUpdateCount + (ulong)ticks + 1UL;
            if (!cinematicSources.TryGetValue(source, out ulong oldExpiry) || expiry > oldExpiry) {
                cinematicSources[source] = expiry;
            }
        }

        internal static void RefreshNPC<TSource>(NPC npc, int ticks) {
            if (npc?.active == true) {
                npc.GetGlobalNPC<TimeFreezeNPC>().RefreshTimedSource(npc, typeof(TSource), ticks);
            }
        }

        internal static void RefreshProjectile<TSource>(Projectile projectile, int ticks) {
            if (projectile?.active == true) {
                projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                    .RefreshTimedSource(projectile, typeof(TSource), ticks);
            }
        }

        internal static TimeFreezeLease AcquireNPC<TSource>(NPC npc,
            Vector2? anchorCenter = null, long sourceInstance = 0,
            TimeFreezeAnchorPriority anchorPriority = TimeFreezeAnchorPriority.Default) {
            if (npc?.active != true) {
                return default;
            }
            TimeFreezeNPC freeze = npc.GetGlobalNPC<TimeFreezeNPC>();
            TimeFreezeLease lease = freeze.Acquire(npc,
                new FreezeSourceKey(typeof(TSource), sourceInstance), anchorCenter,
                (int)anchorPriority);
            freeze.FreezeImmediately(npc);
            return lease;
        }

        internal static TimeFreezeLease AcquireProjectile<TSource>(Projectile projectile,
            Vector2? anchorCenter = null, long sourceInstance = 0,
            TimeFreezeAnchorPriority anchorPriority = TimeFreezeAnchorPriority.Default) {
            if (projectile?.active != true) {
                return default;
            }
            TimeFreezeProjectile freeze = projectile.GetGlobalProjectile<TimeFreezeProjectile>();
            TimeFreezeLease lease = freeze.Acquire(projectile,
                new FreezeSourceKey(typeof(TSource), sourceInstance), anchorCenter,
                (int)anchorPriority);
            freeze.FreezeImmediately(projectile);
            return lease;
        }

        internal static void ReleaseNPC(NPC npc, TimeFreezeLease lease,
            Vector2? releaseVelocity = null,
            TimeFreezeResumePriority resumePriority = TimeFreezeResumePriority.Default) {
            if (npc?.active == true && lease.IsValid) {
                npc.GetGlobalNPC<TimeFreezeNPC>().Release(npc, lease, releaseVelocity,
                    (int)resumePriority);
            }
        }

        internal static void ReleaseProjectile(Projectile projectile, TimeFreezeLease lease,
            Vector2? releaseVelocity = null,
            TimeFreezeResumePriority resumePriority = TimeFreezeResumePriority.Default) {
            if (projectile?.active == true && lease.IsValid) {
                projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                    .Release(projectile, lease, releaseVelocity, (int)resumePriority);
            }
        }

        internal static bool IsLeaseActive(NPC npc, TimeFreezeLease lease)
            => npc?.active == true && lease.IsValid
            && npc.GetGlobalNPC<TimeFreezeNPC>().IsLeaseActive(lease);

        internal static bool IsLeaseActive(Projectile projectile, TimeFreezeLease lease)
            => projectile?.active == true && lease.IsValid
            && projectile.GetGlobalProjectile<TimeFreezeProjectile>().IsLeaseActive(lease);

        internal static bool IsFrozen(NPC npc)
            => npc?.active == true && npc.GetGlobalNPC<TimeFreezeNPC>().IsFrozen;

        internal static bool IsFrozen(Projectile projectile)
            => projectile?.active == true
            && projectile.GetGlobalProjectile<TimeFreezeProjectile>().IsFrozen;

        internal static void AcquireVelocityScaleNPC<TSource>(NPC npc, float scale,
            long sourceInstance = 0) {
            if (npc?.active == true) {
                npc.GetGlobalNPC<TimeFreezeNPC>().AcquireVelocityScale(npc,
                    new FreezeSourceKey(typeof(TSource), sourceInstance), scale);
            }
        }

        internal static void AcquireVelocityScaleProjectile<TSource>(Projectile projectile,
            float scale, long sourceInstance = 0) {
            if (projectile?.active == true) {
                projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                    .AcquireVelocityScale(projectile,
                        new FreezeSourceKey(typeof(TSource), sourceInstance), scale);
            }
        }

        internal static void ReleaseVelocityScaleNPC<TSource>(NPC npc,
            long sourceInstance = 0) {
            if (npc?.active == true) {
                npc.GetGlobalNPC<TimeFreezeNPC>().ReleaseVelocityScale(npc,
                    new FreezeSourceKey(typeof(TSource), sourceInstance));
            }
        }

        internal static void ReleaseVelocityScaleProjectile<TSource>(Projectile projectile,
            long sourceInstance = 0) {
            if (projectile?.active == true) {
                projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                    .ReleaseVelocityScale(projectile,
                        new FreezeSourceKey(typeof(TSource), sourceInstance));
            }
        }

        internal static Vector2 GetEffectiveResumeVelocity(NPC npc) {
            if (npc?.active != true) {
                return Vector2.Zero;
            }
            TimeFreezeNPC freeze = npc.GetGlobalNPC<TimeFreezeNPC>();
            return freeze.HasTimeControl
                ? freeze.EffectiveResumeVelocity
                : EntityFreezeState.IsFinite(npc.velocity) ? npc.velocity : Vector2.Zero;
        }

        internal static Vector2 GetEffectiveResumeVelocity(Projectile projectile) {
            if (projectile?.active != true) {
                return Vector2.Zero;
            }
            TimeFreezeProjectile freeze = projectile.GetGlobalProjectile<TimeFreezeProjectile>();
            return freeze.HasTimeControl
                ? freeze.EffectiveResumeVelocity
                : EntityFreezeState.IsFinite(projectile.velocity)
                    ? projectile.velocity
                    : Vector2.Zero;
        }

        internal static bool TryGetResumeVelocity(Projectile projectile, out Vector2 velocity) {
            velocity = Vector2.Zero;
            if (projectile?.active != true) {
                return false;
            }

            TimeFreezeProjectile freeze = projectile.GetGlobalProjectile<TimeFreezeProjectile>();
            if (!freeze.HasTimeControl) {
                return false;
            }
            velocity = freeze.EffectiveResumeVelocity;
            return true;
        }

        internal static bool FreezeNPCPreAI(NPC npc) {
            TimeFreezeNPC freeze = npc.GetGlobalNPC<TimeFreezeNPC>();
            freeze.SyncTransientSources(npc, GetTransientSources(npc));
            if (freeze.IsFrozen) {
                freeze.FreezeFrame(npc);
                return true;
            }
            return freeze.ApplyVelocityScaleFrame(npc);
        }

        internal static bool FreezeProjectilePreAI(Projectile projectile) {
            TimeFreezeProjectile freeze = projectile.GetGlobalProjectile<TimeFreezeProjectile>();
            freeze.SyncTransientSources(projectile, GetTransientSources(projectile));
            if (freeze.IsFrozen) {
                freeze.FreezeFrame(projectile);
                return true;
            }
            return freeze.ApplyVelocityScaleFrame(projectile);
        }

        internal static void SynchronizeEntitySources() {
            if (Main.npc != null) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc?.active == true && npc.type > NPCID.None) {
                        npc.GetGlobalNPC<TimeFreezeNPC>()
                            .SyncTransientSources(npc, GetTransientSources(npc));
                    }
                }
            }
            if (Main.projectile != null) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (projectile?.active == true && projectile.type > ProjectileID.None) {
                        projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                            .SyncTransientSources(projectile, GetTransientSources(projectile));
                    }
                }
            }
        }

        internal static void RelockFrozenNPCs() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active == true) {
                    npc.GetGlobalNPC<TimeFreezeNPC>().RelockFrozenFrame(npc);
                }
            }
        }

        internal static void RelockFrozenProjectiles() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile?.active == true) {
                    projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                        .RelockFrozenFrame(projectile);
                }
            }
        }

        internal static void BeginWorldFreeze() {
            CaptureWorldFreezeProjectileBaseline();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && WorldFreezeSystem.ShouldFreezeNPC(npc)) {
                    npc.GetGlobalNPC<TimeFreezeNPC>().BeginWorldFreeze(npc);
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && WorldFreezeSystem.ShouldFreezeProjectile(projectile)) {
                    projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                        .BeginWorldFreeze(projectile, spawnedDuringFreeze: false);
                }
            }
        }

        internal static void EndWorldFreeze() {
            try {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active) {
                        npc.GetGlobalNPC<TimeFreezeNPC>().EndWorldFreeze(npc);
                    }
                }

                const int maxThawPasses = 8;
                for (int pass = 0; pass < maxThawPasses; pass++) {
                    bool foundSpawnedProjectile = false;
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile projectile = Main.projectile[i];
                        if (!projectile.active) {
                            continue;
                        }
                        TimeFreezeProjectile freeze = projectile
                            .GetGlobalProjectile<TimeFreezeProjectile>();
                        if (!freeze.PrepareWorldThaw(projectile,
                            WasSpawnedDuringWorldFreeze(projectile, freeze))) {
                            continue;
                        }
                        foundSpawnedProjectile = true;
                        SafeKillDuringWorldThaw(projectile);
                    }
                    if (!foundSpawnedProjectile) {
                        return;
                    }
                }

                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (!projectile.active) {
                        continue;
                    }
                    TimeFreezeProjectile freeze = projectile
                        .GetGlobalProjectile<TimeFreezeProjectile>();
                    if (freeze.PrepareWorldThaw(projectile,
                        WasSpawnedDuringWorldFreeze(projectile, freeze))) {
                        projectile.active = false;
                    }
                }
            }
            finally {
                ClearWorldFreezeProjectileBaseline();
            }
        }

        internal static void RollbackWorldFreeze() {
            try {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active) {
                        npc.GetGlobalNPC<TimeFreezeNPC>().EndWorldFreeze(npc);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (projectile.active) {
                        projectile.GetGlobalProjectile<TimeFreezeProjectile>()
                            .CancelWorldFreeze(projectile);
                    }
                }
            }
            finally {
                ClearWorldFreezeProjectileBaseline();
            }
        }

        internal static void ResetSession() {
            cinematicSources.Clear();
            ClearWorldFreezeProjectileBaseline();
            if (Main.npc != null) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc?.active == true && npc.type > NPCID.None) {
                        npc.GetGlobalNPC<TimeFreezeNPC>().ResetFreezeState();
                    }
                }
            }
            if (Main.projectile != null) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (projectile?.active == true && projectile.type > ProjectileID.None) {
                        projectile.GetGlobalProjectile<TimeFreezeProjectile>().ResetFreezeState();
                    }
                }
            }
        }

        private static bool ShouldCinematicFreeze(Projectile projectile)
            => !projectile.hide && !projectile.friendly
            && !Main.projPet[projectile.type] && !projectile.minion
            && !Main.projHook[projectile.type]
            && !CWRLoad.ProjValue.ImmuneFrozen[projectile.type];

        private static TimeFreezeSource GetTransientSources(NPC npc) {
            TimeFreezeSource sources = TimeFreezeSource.None;
            if (WorldFreezeSystem.IsActive && WorldFreezeSystem.ShouldFreezeNPC(npc)) {
                sources |= TimeFreezeSource.World;
            }
            if (IsCinematicFreezeActive) {
                sources |= TimeFreezeSource.Cinematic;
            }
            return sources;
        }

        private static TimeFreezeSource GetTransientSources(Projectile projectile) {
            TimeFreezeSource sources = TimeFreezeSource.None;
            if (WorldFreezeSystem.IsActive
                && WorldFreezeSystem.ShouldFreezeProjectile(projectile)) {
                sources |= TimeFreezeSource.World;
            }
            if (IsCinematicFreezeActive && ShouldCinematicFreeze(projectile)) {
                sources |= TimeFreezeSource.Cinematic;
            }
            return sources;
        }

        private static void SafeKillDuringWorldThaw(Projectile projectile) {
            try {
                projectile.Kill();
            }
            catch (Exception exception) {
                projectile.active = false;
                CWRMod.Instance?.Logger.Error(
                    $"World freeze projectile cleanup failed: {exception}");
            }
        }

        private static void CaptureWorldFreezeProjectileBaseline() {
            if (worldFreezeProjectileBaseline == null
                || worldFreezeProjectileBaseline.Length != Main.maxProjectiles) {
                worldFreezeProjectileBaseline = new ProjectileIdentity[Main.maxProjectiles];
            }
            else {
                Array.Clear(worldFreezeProjectileBaseline, 0,
                    worldFreezeProjectileBaseline.Length);
            }

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active) {
                    continue;
                }
                TimeFreezeProjectile freeze = projectile
                    .GetGlobalProjectile<TimeFreezeProjectile>();
                worldFreezeProjectileBaseline[i] = new ProjectileIdentity(
                    freeze.EntityGeneration, projectile.type, projectile.owner,
                    projectile.identity);
            }
            worldFreezeProjectileBaselineValid = true;
        }

        private static bool WasSpawnedDuringWorldFreeze(Projectile projectile,
            TimeFreezeProjectile freeze) {
            if (!worldFreezeProjectileBaselineValid
                || projectile.whoAmI < 0
                || projectile.whoAmI >= worldFreezeProjectileBaseline.Length) {
                return false;
            }
            ProjectileIdentity baseline = worldFreezeProjectileBaseline[projectile.whoAmI];
            return baseline.EntityGeneration == 0
                || baseline.EntityGeneration != freeze.EntityGeneration
                || baseline.Type != projectile.type
                || baseline.Owner != projectile.owner
                || baseline.Identity != projectile.identity;
        }

        private static void ClearWorldFreezeProjectileBaseline() {
            worldFreezeProjectileBaselineValid = false;
            if (worldFreezeProjectileBaseline != null) {
                Array.Clear(worldFreezeProjectileBaseline, 0,
                    worldFreezeProjectileBaseline.Length);
            }
        }

        internal static void PruneExpiredCinematicSources() {
            if (cinematicSources.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            List<Type> expired = null;
            foreach (var pair in cinematicSources) {
                if (pair.Value <= now) {
                    expired ??= [];
                    expired.Add(pair.Key);
                }
            }
            if (expired == null) {
                return;
            }
            foreach (Type source in expired) {
                cinematicSources.Remove(source);
            }
        }
    }

    internal sealed class TimeFreezeLifecycleSystem : ModSystem
    {
        public override void OnWorldLoad() => WorldFreezeSystem.ResetSession();

        public override void OnWorldUnload() => WorldFreezeSystem.ResetSession();

        public override void ClearWorld() => WorldFreezeSystem.ResetSession();

        public override void PreUpdateEntities() {
            TimeFreezeSystem.PruneExpiredCinematicSources();
            TimeFreezeSystem.SynchronizeEntitySources();
        }

        public override void PostUpdateNPCs()
            => TimeFreezeSystem.RelockFrozenNPCs();

        public override void PostUpdateProjectiles()
            => TimeFreezeSystem.RelockFrozenProjectiles();
    }
}
