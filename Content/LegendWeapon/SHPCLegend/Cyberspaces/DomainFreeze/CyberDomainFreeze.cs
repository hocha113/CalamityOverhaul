using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>领域冻结权威状态</summary>
    internal partial class CyberDomainFreeze : ICWRLoader
    {
        private readonly record struct NPCFreezeTarget(NetworkNPCIdentity Identity,
            float Seed, Vector2 Center);

        private readonly record struct ProjectileFreezeTarget(
            NetworkProjectileIdentity Identity, float Seed, Vector2 Center);

        private const int AcceleratedThawFrames = 90;
        private const int MaxEntityResolutionFrames = 120;
        private const ushort RamOperationId = RamNet.FirstExternalOperation;
        private static long nextActivationId;

        /// <summary>默认冻结时长</summary>
        public const int DefaultFreezeDuration = 600;

        /// <summary>触发冻结 RAM 消耗</summary>
        public const int RamCost = 4;

        public static readonly List<FreezeEntry> FrozenNPCs = [];
        public static readonly List<FreezeProjEntry> FrozenProjectiles = [];

        void ICWRLoader.UnLoadData() => Reset();

        public static bool IsNPCFrozen(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.EntityIndex == npcIndex && IsEntryActive(entry)) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsProjectileFrozen(int projectileIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (entry.EntityIndex == projectileIndex && IsEntryActive(entry)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>取 NPC 冻结表现参数，ownerWho 用于按施术者取领域强度</summary>
        public static bool TryGetNPCVisual(int npcIndex, out float progress,
            out float seed, out int ownerWho) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.EntityIndex == npcIndex && IsEntryActive(entry)) {
                    progress = entry.Progress;
                    seed = entry.Seed;
                    ownerWho = entry.OwnerWho;
                    return true;
                }
            }
            progress = -1f;
            seed = 0f;
            ownerWho = -1;
            return false;
        }

        /// <summary>取弹幕冻结表现参数，ownerWho 用于按施术者取领域强度</summary>
        public static bool TryGetProjectileVisual(int projectileIndex,
            out float progress, out float seed, out int ownerWho) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (entry.EntityIndex == projectileIndex && IsEntryActive(entry)) {
                    progress = entry.Progress;
                    seed = entry.Seed;
                    ownerWho = entry.OwnerWho;
                    return true;
                }
            }
            progress = -1f;
            seed = 0f;
            ownerWho = -1;
            return false;
        }

        public static void TriggerFreeze(Player owner) {
            if (owner?.active != true || owner.dead) {
                return;
            }
            CyberspacePlayer cyberspace = Cyberspace.For(owner);
            if (cyberspace == null || !cyberspace.Active || cyberspace.Intensity < 0.5f
                || cyberspace.CurrentLayer < Cyberspace.MaxLayerCount) {
                return;
            }
            if (!HackTime.InfiniteHack
                && !RamSystem.CanAfford(owner, RamCost)) {
                PlayRequestFailure(owner);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (!RamSystem.TryAllocateRequest(owner, out RamRequestToken request)) {
                    PlayRequestFailure(owner);
                    return;
                }
                SendFreezeRequest(request);
                return;
            }

            ExecuteAuthoritativeFreeze(owner, default, -1);
        }

        private static void ExecuteAuthoritativeFreeze(Player owner,
            RamRequestToken request, int responseClient) {
            if (!CanExecuteAuthoritativeFreeze(owner)) {
                CompleteRamRequest(owner, request, FreezeResultCode.InvalidState,
                    0f, responseClient);
                return;
            }

            float paid = 0f;
            if (!HackTime.InfiniteHackAuthority
                && !RamSystem.TryConsume(owner, RamCost, out paid)) {
                CompleteRamRequest(owner, request, FreezeResultCode.InsufficientRam,
                    0f, responseClient);
                if (Main.netMode == NetmodeID.SinglePlayer) {
                    PlayRequestFailure(owner);
                }
                return;
            }

            CollectFreezeTargets(owner, out List<NPCFreezeTarget> npcTargets,
                out List<ProjectileFreezeTarget> projectileTargets);
            long activationId = AllocateActivationId();
            ApplyFreezeBatch(owner.whoAmI, activationId, npcTargets,
                projectileTargets, replicated: false, elapsed: 0,
                duration: DefaultFreezeDuration, out List<NPCFreezeTarget> acceptedNPCs,
                out List<ProjectileFreezeTarget> acceptedProjectiles);

            RememberActivation(activationId);
            if (Main.netMode == NetmodeID.Server) {
                BroadcastApply(owner.whoAmI, activationId, acceptedNPCs,
                    acceptedProjectiles, 0, DefaultFreezeDuration);
            }
            PlayActivationWave(owner);
            CompleteRamRequest(owner, request, FreezeResultCode.Success,
                paid, responseClient);
        }

        private static void CompleteRamRequest(Player owner, RamRequestToken request,
            FreezeResultCode code, float paid, int responseClient) {
            if (!request.IsValid || responseClient < 0
                || Main.netMode != NetmodeID.Server) {
                return;
            }
            if (RamSystem.CompleteRequest(owner, request, RamOperationId,
                (byte)code, paid, out RamRequestResult result)) {
                RamNet.SendRequestResult(owner, result, responseClient);
                return;
            }
            RamNet.SendStateSnapshot(owner, responseClient);
        }

        private static void PlayActivationWave(Player owner) {
            if (Main.dedServ || owner?.active != true || Main.myPlayer != owner.whoAmI) {
                return;
            }
            IEntitySource source = owner.GetSource_FromThis();
            Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberFreezeWaveProj>(), 0, 0,
                owner.whoAmI);
        }

        private static void PlayRequestFailure(Player owner) {
            if (Main.dedServ || owner?.active != true
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                Volume = 0.4f,
                Pitch = -0.3f,
            }, owner.Center);
            RamSystem.NotifyInsufficient();
            CombatText.NewText(owner.Hitbox, new Color(255, 90, 80),
                "// LOW RAM", true);
        }

        private static bool CanExecuteAuthoritativeFreeze(Player owner) {
            if (owner?.active != true || owner.dead) {
                return false;
            }
            CyberspacePlayer cyberspace = Cyberspace.For(owner);
            return cyberspace != null && cyberspace.Active
                && cyberspace.Intensity >= 0.5f
                && cyberspace.CurrentLayer >= Cyberspace.MaxLayerCount;
        }

        private static void CollectFreezeTargets(Player owner,
            out List<NPCFreezeTarget> npcTargets,
            out List<ProjectileFreezeTarget> projectileTargets) {
            List<NPCFreezeTarget> collectedNPCs = [];
            List<ProjectileFreezeTarget> collectedProjectiles = [];
            npcTargets = collectedNPCs;
            projectileTargets = collectedProjectiles;
            CyberspacePlayer cyberspace = Cyberspace.For(owner);
            Vector2 domainCenter = owner.Center;
            float effectiveRadius = cyberspace.Radius * cyberspace.ExpandProgress;
            //L3 撤墙：时停即全世界时停，收集不再按半径筛选（取远超世界尺寸的有限值）
            if (cyberspace.Active
                && cyberspace.CurrentLayer >= Cyberspace.MaxLayerCount) {
                effectiveRadius = 1_000_000f;
            }
            if (!IsValidCenter(domainCenter) || !float.IsFinite(effectiveRadius)
                || effectiveRadius <= 0f) {
                return;
            }
            float radiusSquared = effectiveRadius * effectiveRadius;
            if (!float.IsFinite(radiusSquared)) {
                return;
            }

            HashSet<int> processedGroups = [];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || IsNPCFrozen(i) || CyberBanish.IsBanishing(i)
                    || Vector2.DistanceSquared(npc.Center, domainCenter) > radiusSquared) {
                    continue;
                }

                int anchor = NpcGroupHelper.GetAnchorIndex(npc);
                if (!processedGroups.Add(anchor)) {
                    continue;
                }
                NpcGroupHelper.ForEachGroupMember(npc, member => {
                    if (!member.active || IsNPCFrozen(member.whoAmI)
                        || CyberBanish.IsBanishing(member.whoAmI)
                        || !NetworkNPCIdentity.TryCapture(member,
                            out NetworkNPCIdentity identity)
                        || !IsValidCenter(member.Center)) {
                        return;
                    }
                    collectedNPCs.Add(new NPCFreezeTarget(identity,
                        Main.rand.NextFloat(), member.Center));
                });
            }

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.friendly
                    || Main.projPet[projectile.type] || projectile.minion
                    || Main.projHook[projectile.type] || IsProjectileFrozen(i)
                    || Vector2.DistanceSquared(projectile.Center, domainCenter)
                        > radiusSquared
                    || !NetworkProjectileIdentity.TryCapture(projectile,
                        out NetworkProjectileIdentity identity)
                    || !IsValidCenter(projectile.Center)) {
                    continue;
                }
                collectedProjectiles.Add(new ProjectileFreezeTarget(identity,
                    Main.rand.NextFloat(), projectile.Center));
            }
        }

        private static void ApplyFreezeBatch(int ownerWho, long activationId,
            IReadOnlyList<NPCFreezeTarget> npcTargets,
            IReadOnlyList<ProjectileFreezeTarget> projectileTargets,
            bool replicated, int elapsed, int duration,
            out List<NPCFreezeTarget> acceptedNPCs,
            out List<ProjectileFreezeTarget> acceptedProjectiles) {
            acceptedNPCs = [];
            acceptedProjectiles = [];
            if (!IsValidOwner(ownerWho) || activationId <= 0
                || !IsValidTiming(elapsed, duration)
                || npcTargets == null || projectileTargets == null
                || npcTargets.Count > Main.maxNPCs
                || projectileTargets.Count > Main.maxProjectiles
                || npcTargets.Count + projectileTargets.Count
                    > Main.maxNPCs + Main.maxProjectiles) {
                return;
            }

            HashSet<NetworkNPCIdentity> acceptedNPCIdentities = [];
            for (int i = 0; i < npcTargets.Count; i++) {
                NPCFreezeTarget target = npcTargets[i];
                if (!IsValidTarget(target)
                    || !acceptedNPCIdentities.Add(target.Identity)) {
                    continue;
                }
                if (replicated && WasNPCReleased(activationId,
                    target.Identity)) {
                    continue;
                }
                if (replicated) {
                    PrepareIncomingNPCIdentity(activationId, target.Identity);
                }
                if (target.Identity.TryResolve(out NPC npc)) {
                    if (TryApplyNPC(ownerWho, activationId, target, npc,
                        replicated, elapsed, duration)) {
                        acceptedNPCs.Add(target);
                    }
                }
                else if (replicated && QueueNPCApply(ownerWho, activationId,
                    target, elapsed, duration)) {
                    acceptedNPCs.Add(target);
                }
            }

            HashSet<NetworkProjectileIdentity> acceptedProjectileIdentities = [];
            for (int i = 0; i < projectileTargets.Count; i++) {
                ProjectileFreezeTarget target = projectileTargets[i];
                if (!IsValidTarget(target)
                    || !acceptedProjectileIdentities.Add(target.Identity)) {
                    continue;
                }
                if (replicated && WasProjectileReleased(activationId,
                    target.Identity)) {
                    continue;
                }
                if (target.Identity.TryResolve(out Projectile projectile)) {
                    if (TryApplyProjectile(ownerWho, activationId, target,
                        projectile, replicated, elapsed, duration)) {
                        acceptedProjectiles.Add(target);
                    }
                }
                else if (replicated && QueueProjectileApply(ownerWho,
                    activationId, target, elapsed, duration)) {
                    acceptedProjectiles.Add(target);
                }
            }
        }

        private static bool TryApplyNPC(int ownerWho, long activationId,
            NPCFreezeTarget target, NPC npc, bool replaceConflicts, int elapsed,
            int duration) {
            if (npc?.active != true || npc.whoAmI != target.Identity.Index
                || !target.Identity.TryResolve(out NPC resolved) || resolved != npc) {
                return false;
            }
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                FreezeEntry existing = FrozenNPCs[i];
                if (existing.ActivationId != activationId
                    || existing.Identity != target.Identity) {
                    continue;
                }
                if (existing.OwnerWho != ownerWho || existing.Duration != duration
                    || existing.Seed != target.Seed
                    || existing.FreezeCenter != target.Center) {
                    return false;
                }
                existing.Timer = Math.Max(existing.Timer, elapsed);
                if (IsEntryActive(existing)) {
                    return true;
                }
                TimeControlReplicationSystem.CancelNPC<CyberDomainFreeze>(
                    activationId, target.Identity);
                return ResolvePendingNPCEntry(existing, npc, replaceConflicts);
            }

            if (replaceConflicts) {
                RemoveNPCEntriesAtIndex(npc.whoAmI);
            }
            else if (IsNPCFrozen(npc.whoAmI)
                || CyberBanish.IsBanishing(npc.whoAmI)) {
                return false;
            }

            TimeFreezeLease lease = TimeFreezeSystem.AcquireNPC<CyberDomainFreeze>(
                npc, target.Center, activationId,
                TimeFreezeAnchorPriority.Authoritative);
            if (!lease.IsValid) {
                return false;
            }
            FrozenNPCs.Add(new FreezeEntry {
                EntityIndex = npc.whoAmI,
                Identity = target.Identity,
                ActivationId = activationId,
                Timer = elapsed,
                Duration = duration,
                Seed = target.Seed,
                FreezeCenter = target.Center,
                FreezeLease = lease,
                OwnerWho = ownerWho,
            });
            return true;
        }

        private static bool ResolvePendingNPCEntry(FreezeEntry entry, NPC npc,
            bool replaceConflicts) {
            if (entry == null || npc?.active != true
                || entry.Identity.Index != npc.whoAmI
                || !entry.Identity.TryResolve(out NPC resolved) || resolved != npc) {
                return false;
            }
            if (replaceConflicts) {
                RemoveNPCEntriesAtIndex(npc.whoAmI, entry,
                    cancelPending: false);
            }
            else if (IsNPCFrozen(npc.whoAmI)
                || CyberBanish.IsBanishing(npc.whoAmI)) {
                return false;
            }
            TimeFreezeLease lease = TimeFreezeSystem.AcquireNPC<CyberDomainFreeze>(
                npc, entry.FreezeCenter, entry.ActivationId,
                TimeFreezeAnchorPriority.Authoritative);
            if (!lease.IsValid) {
                return false;
            }
            entry.EntityIndex = npc.whoAmI;
            entry.ResolutionExpiresAt = 0;
            entry.FreezeLease = lease;
            return true;
        }

        private static bool TryApplyProjectile(int ownerWho, long activationId,
            ProjectileFreezeTarget target, Projectile projectile,
            bool replaceConflicts, int elapsed, int duration) {
            if (projectile?.active != true
                || !target.Identity.TryResolve(out Projectile resolved)
                || resolved != projectile) {
                return false;
            }
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                FreezeProjEntry existing = FrozenProjectiles[i];
                if (existing.ActivationId != activationId
                    || existing.Identity != target.Identity) {
                    continue;
                }
                if (existing.OwnerWho != ownerWho || existing.Duration != duration
                    || existing.Seed != target.Seed
                    || existing.FreezeCenter != target.Center) {
                    return false;
                }
                existing.Timer = Math.Max(existing.Timer, elapsed);
                if (IsEntryActive(existing)) {
                    return true;
                }
                TimeControlReplicationSystem.CancelProjectile<CyberDomainFreeze>(
                    activationId, target.Identity);
                return ResolvePendingProjectileEntry(existing, projectile,
                    replaceConflicts);
            }

            if (replaceConflicts) {
                RemoveProjectileEntriesAtIndex(projectile.whoAmI);
            }
            else if (IsProjectileFrozen(projectile.whoAmI)) {
                return false;
            }

            TimeFreezeLease lease = TimeFreezeSystem
                .AcquireProjectile<CyberDomainFreeze>(projectile, target.Center,
                    activationId, TimeFreezeAnchorPriority.Authoritative);
            if (!lease.IsValid) {
                return false;
            }
            FrozenProjectiles.Add(new FreezeProjEntry {
                EntityIndex = projectile.whoAmI,
                Identity = target.Identity,
                ActivationId = activationId,
                Timer = elapsed,
                Duration = duration,
                Seed = target.Seed,
                FreezeCenter = target.Center,
                FreezeLease = lease,
                OwnerWho = ownerWho,
            });
            return true;
        }

        private static bool ResolvePendingProjectileEntry(FreezeProjEntry entry,
            Projectile projectile, bool replaceConflicts) {
            if (entry == null || projectile?.active != true
                || !entry.Identity.TryResolve(out Projectile resolved)
                || resolved != projectile) {
                return false;
            }
            if (replaceConflicts) {
                RemoveProjectileEntriesAtIndex(projectile.whoAmI, entry,
                    cancelPending: false);
            }
            else if (IsProjectileFrozen(projectile.whoAmI)) {
                return false;
            }
            TimeFreezeLease lease = TimeFreezeSystem
                .AcquireProjectile<CyberDomainFreeze>(projectile,
                    entry.FreezeCenter, entry.ActivationId,
                    TimeFreezeAnchorPriority.Authoritative);
            if (!lease.IsValid) {
                return false;
            }
            entry.EntityIndex = projectile.whoAmI;
            entry.ResolutionExpiresAt = 0;
            entry.FreezeLease = lease;
            return true;
        }

        public static void Update() {
            PruneReleasedTargets();
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                UpdateClientPresentation();
                return;
            }
            UpdateAuthoritativeNPCs();
            UpdateAuthoritativeProjectiles();
            FlushBroadcasts();
        }

        private static void UpdateAuthoritativeNPCs() {
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                if (!entry.Identity.TryResolve(out NPC npc)
                    || !TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)) {
                    RemoveNPCEntryAt(i, spawnBurst: false,
                        broadcast: Main.netMode == NetmodeID.Server);
                    continue;
                }

                int previousTimer = entry.Timer;
                entry.Timer = Math.Min(entry.Timer + 1, entry.Duration);
                int thawStart = Math.Max(0, entry.Duration - AcceleratedThawFrames);
                bool accelerated = entry.Timer < thawStart
                    && !Cyberspace.IsInsideDomainOf(entry.OwnerWho, npc.Center)
                    && !AnyGroupMemberInDomain(npc, entry.OwnerWho);
                if (accelerated) {
                    entry.Timer = thawStart;
                    if (Main.netMode == NetmodeID.Server) {
                        BroadcastAdvanceNPC(entry);
                    }
                }

                if (previousTimer < thawStart && entry.Timer >= thawStart) {
                    PlayThawSound(npc);
                }
                if (!Main.dedServ) {
                    CyberDomainFreezeParticles.SpawnFreezeParticles(npc,
                        entry.Progress, entry.Seed);
                }
                if (entry.Timer >= entry.Duration) {
                    RemoveNPCEntryAt(i, spawnBurst: true,
                        broadcast: Main.netMode == NetmodeID.Server);
                }
            }
        }

        private static void UpdateAuthoritativeProjectiles() {
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (!entry.Identity.TryResolve(out Projectile projectile)
                    || !TimeFreezeSystem.IsLeaseActive(projectile,
                        entry.FreezeLease)) {
                    RemoveProjectileEntryAt(i,
                        broadcast: Main.netMode == NetmodeID.Server);
                    continue;
                }
                entry.Timer = Math.Min(entry.Timer + 1, entry.Duration);
                if (entry.Timer >= entry.Duration) {
                    RemoveProjectileEntryAt(i,
                        broadcast: Main.netMode == NetmodeID.Server);
                }
            }
        }

        private static void UpdateClientPresentation() {
            ulong now = Main.GameUpdateCount;
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.EntityIndex < 0) {
                    if (entry.ResolutionExpiresAt != 0
                        && now >= entry.ResolutionExpiresAt) {
                        RemoveNPCEntryAt(i, spawnBurst: false,
                            broadcast: false);
                    }
                    continue;
                }
                if (!entry.Identity.TryResolve(out NPC npc)
                    || !TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)) {
                    RemoveNPCEntryAt(i, spawnBurst: false, broadcast: false);
                    continue;
                }
                int previousTimer = entry.Timer;
                entry.Timer = Math.Min(entry.Timer + 1,
                    Math.Max(entry.Duration - 1, 0));
                int thawStart = Math.Max(0, entry.Duration - AcceleratedThawFrames);
                if (previousTimer < thawStart && entry.Timer >= thawStart) {
                    PlayThawSound(npc);
                }
                CyberDomainFreezeParticles.SpawnFreezeParticles(npc,
                    entry.Progress, entry.Seed);
            }
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (entry.EntityIndex < 0) {
                    if (entry.ResolutionExpiresAt != 0
                        && now >= entry.ResolutionExpiresAt) {
                        RemoveProjectileEntryAt(i, broadcast: false);
                    }
                    continue;
                }
                if (!entry.Identity.TryResolve(out Projectile projectile)
                    || !TimeFreezeSystem.IsLeaseActive(projectile,
                        entry.FreezeLease)) {
                    RemoveProjectileEntryAt(i, broadcast: false);
                    continue;
                }
                entry.Timer = Math.Min(entry.Timer + 1,
                    Math.Max(entry.Duration - 1, 0));
            }
        }

        private static bool AnyGroupMemberInDomain(NPC npc, int ownerWho) {
            int anchor = NpcGroupHelper.GetAnchorIndex(npc);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other.active && other.whoAmI != npc.whoAmI
                    && NpcGroupHelper.GetAnchorIndex(other) == anchor
                    && Cyberspace.IsInsideDomainOf(ownerWho, other.Center)) {
                    return true;
                }
            }
            return false;
        }

        private static void PlayThawSound(NPC npc) {
            if (!Main.dedServ
                && NpcGroupHelper.GetAnchorIndex(npc) == npc.whoAmI) {
                SoundEngine.PlaySound(CWRSound.FaultTransition, npc.Center);
            }
        }

        private static void RemoveNPCEntriesAtIndex(int npcIndex,
            FreezeEntry except = null, bool cancelPending = true) {
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                if (FrozenNPCs[i] != except
                    && FrozenNPCs[i].EntityIndex == npcIndex) {
                    RememberReleasedNPC(FrozenNPCs[i].ActivationId,
                        FrozenNPCs[i].Identity);
                    RemoveNPCEntryAt(i, spawnBurst: false, broadcast: false,
                        cancelPending);
                }
            }
        }

        private static void RemoveProjectileEntriesAtIndex(int projectileIndex,
            FreezeProjEntry except = null, bool cancelPending = true) {
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                if (FrozenProjectiles[i] != except
                    && FrozenProjectiles[i].EntityIndex == projectileIndex) {
                    RememberReleasedProjectile(
                        FrozenProjectiles[i].ActivationId,
                        FrozenProjectiles[i].Identity);
                    RemoveProjectileEntryAt(i, broadcast: false,
                        cancelPending);
                }
            }
        }

        private static void RemoveNPCEntryAt(int index, bool spawnBurst,
            bool broadcast, bool cancelPending = true) {
            if (index < 0 || index >= FrozenNPCs.Count) {
                return;
            }
            FreezeEntry entry = FrozenNPCs[index];
            NPC npc = null;
            if (entry.Identity.TryResolve(out NPC resolved)) {
                npc = resolved;
                TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease,
                    entry.FreezeLease.ResumeVelocity * 0.5f,
                    TimeFreezeResumePriority.Domain);
            }
            if (spawnBurst && !Main.dedServ) {
                CyberDomainFreezeParticles.SpawnThawBurst(
                    npc?.Center ?? entry.FreezeCenter);
            }
            if (broadcast) {
                BroadcastReleaseNPC(entry.ActivationId, entry.Identity);
            }
            if (cancelPending) {
                TimeControlReplicationSystem.CancelNPC<CyberDomainFreeze>(
                    entry.ActivationId, entry.Identity);
            }
            FrozenNPCs.RemoveAt(index);
        }

        private static void RemoveProjectileEntryAt(int index, bool broadcast,
            bool cancelPending = true) {
            if (index < 0 || index >= FrozenProjectiles.Count) {
                return;
            }
            FreezeProjEntry entry = FrozenProjectiles[index];
            if (entry.Identity.TryResolve(out Projectile projectile)) {
                TimeFreezeSystem.ReleaseProjectile(projectile, entry.FreezeLease,
                    entry.FreezeLease.ResumeVelocity,
                    TimeFreezeResumePriority.Domain);
            }
            if (broadcast) {
                BroadcastReleaseProjectile(entry.ActivationId, entry.Identity);
            }
            if (cancelPending) {
                TimeControlReplicationSystem.CancelProjectile<CyberDomainFreeze>(
                    entry.ActivationId, entry.Identity);
            }
            FrozenProjectiles.RemoveAt(index);
        }

        private static long AllocateActivationId() {
            nextActivationId = nextActivationId >= long.MaxValue
                ? 1 : nextActivationId + 1;
            return nextActivationId;
        }

        private static ulong ComputeResolutionExpiry(int remainingFrames)
            => Main.GameUpdateCount + (ulong)Math.Clamp(remainingFrames, 1,
                MaxEntityResolutionFrames);

        public static void Reset() {
            ClearEntries();
            nextActivationId = 0;
            ClearRememberedActivations();
            ClearReleasedTargets();
            ClearPendingBroadcasts();
        }

        private static void ClearEntries() {
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                RemoveNPCEntryAt(i, spawnBurst: false, broadcast: false);
            }
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                RemoveProjectileEntryAt(i, broadcast: false);
            }
            TimeControlReplicationSystem.CancelAll<CyberDomainFreeze>();
        }

        private static bool IsEntryActive(FreezeEntry entry) {
            return entry != null && entry.Identity.TryResolve(out NPC npc)
                && npc.whoAmI == entry.EntityIndex
                && TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease);
        }

        private static bool IsEntryActive(FreezeProjEntry entry) {
            return entry != null
                && entry.Identity.TryResolve(out Projectile projectile)
                && projectile.whoAmI == entry.EntityIndex
                && TimeFreezeSystem.IsLeaseActive(projectile, entry.FreezeLease);
        }
    }

    internal sealed class FreezeEntry
    {
        public int EntityIndex;
        public int Timer;
        public int Duration;
        public float Seed;
        public int OwnerWho;
        internal long ActivationId;
        internal NetworkNPCIdentity Identity;
        internal Vector2 FreezeCenter;
        internal TimeFreezeLease FreezeLease;
        internal ulong ResolutionExpiresAt;

        public float Progress => Duration > 0
            ? MathHelper.Clamp(Timer / (float)Duration, 0f, 1f)
            : 0f;
    }

    internal sealed class FreezeProjEntry
    {
        public int EntityIndex;
        public int Timer;
        public int Duration;
        public float Seed;
        public int OwnerWho;
        internal long ActivationId;
        internal NetworkProjectileIdentity Identity;
        internal Vector2 FreezeCenter;
        internal TimeFreezeLease FreezeLease;
        internal ulong ResolutionExpiresAt;

        public float Progress => Duration > 0
            ? MathHelper.Clamp(Timer / (float)Duration, 0f, 1f)
            : 0f;
    }
}
