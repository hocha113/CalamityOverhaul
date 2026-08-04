using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>领域冻结，NPC/弹幕独立计时，net 锚点</summary>
    internal class CyberDomainFreeze : ICWRLoader
    {
        private enum FreezePacketKind : byte
        {
            Request,
            Apply,
        }

        private readonly record struct NPCFreezeTarget(int Index, int Type,
            float Seed, Vector2 Center);

        private readonly record struct ProjectileFreezeTarget(int Index, byte Owner,
            int Identity, int Type, float Seed, Vector2 Center);

        private static long nextActivationId;

        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>默认冻结时长帧数（600=10秒）</summary>
        public const int DefaultFreezeDuration = 600;

        /// <summary>触发冻结 RAM 消耗</summary>
        public const int RamCost = 4;

        /// <summary>冻结中 NPC 列表</summary>
        public static readonly List<FreezeEntry> FrozenNPCs = [];

        /// <summary>冻结中弹幕列表</summary>
        public static readonly List<FreezeProjEntry> FrozenProjectiles = [];

        /// <summary>NPC 是否冻结中</summary>
        public static bool IsNPCFrozen(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex
                    && IsEntryActive(FrozenNPCs[i]))
                    return true;
            }
            return false;
        }

        /// <summary>弹幕是否冻结中</summary>
        public static bool IsProjectileFrozen(int projIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                if (FrozenProjectiles[i].EntityIndex == projIndex
                    && IsEntryActive(FrozenProjectiles[i]))
                    return true;
            }
            return false;
        }

        /// <summary>NPC 冻结进度 0~1，未冻结 -1</summary>
        public static float GetNPCFreezeProgress(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex
                    && IsEntryActive(FrozenNPCs[i]))
                    return FrozenNPCs[i].Progress;
            }
            return -1f;
        }

        /// <summary>弹幕冻结进度</summary>
        public static float GetProjectileFreezeProgress(int projIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                if (FrozenProjectiles[i].EntityIndex == projIndex
                    && IsEntryActive(FrozenProjectiles[i]))
                    return FrozenProjectiles[i].Progress;
            }
            return -1f;
        }

        /// <summary>NPC 冻结种子</summary>
        public static float GetNPCSeed(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex
                    && IsEntryActive(FrozenNPCs[i]))
                    return FrozenNPCs[i].Seed;
            }
            return 0f;
        }

        /// <summary>触发域内冻结+能量波+net 广播</summary>
        public static void TriggerFreeze(Player owner) {
            if (owner == null) return;
            CyberspacePlayer cp = Cyberspace.For(owner);
            if (cp == null) return;
            if (!cp.Active || cp.Intensity < 0.5f || cp.CurrentLayer < Cyberspace.MaxLayerCount) return;

            //RAM 不足则 HUD 闪并拦截，仅本机
            if (!HackTime.InfiniteHack && (RamSystem.IsLocked || !RamSystem.CanAfford(RamCost))) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.4f, Pitch = -0.3f }, owner.Center);
                    RamSystem.NotifyInsufficient();
                    Terraria.CombatText.NewText(owner.Hitbox, new Microsoft.Xna.Framework.Color(255, 90, 80), "// LOW RAM", true);
                }
                return;
            }
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume(RamCost);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                SendFreezeRequest();
            }
            else {
                ExecuteAuthoritativeFreeze(owner);
            }

            //冻结能量波
            if (Main.myPlayer == owner.whoAmI) {
                IEntitySource source = owner.GetSource_FromThis();
                Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberFreezeWaveProj>(), 0, 0, owner.whoAmI);
            }
        }

        private static void ExecuteAuthoritativeFreeze(Player owner) {
            if (!CanExecuteAuthoritativeFreeze(owner)) {
                return;
            }

            CollectFreezeTargets(owner, out List<NPCFreezeTarget> npcTargets,
                out List<ProjectileFreezeTarget> projectileTargets);
            long activationId = AllocateActivationId();
            ApplyFreezeBatch(owner.whoAmI, activationId, npcTargets, projectileTargets,
                replaceExisting: false,
                out List<NPCFreezeTarget> acceptedNPCs,
                out List<ProjectileFreezeTarget> acceptedProjectiles);

            if (Main.netMode == NetmodeID.Server) {
                BroadcastApply(owner.whoAmI, activationId, acceptedNPCs,
                    acceptedProjectiles);
            }
        }

        private static bool CanExecuteAuthoritativeFreeze(Player owner) {
            if (owner?.active != true || owner.dead) {
                return false;
            }
            CyberspacePlayer cp = Cyberspace.For(owner);
            return cp != null && cp.Active && cp.Intensity >= 0.5f
                && cp.CurrentLayer >= Cyberspace.MaxLayerCount;
        }

        private static void CollectFreezeTargets(Player owner,
            out List<NPCFreezeTarget> npcTargets,
            out List<ProjectileFreezeTarget> projectileTargets) {
            List<NPCFreezeTarget> collectedNPCs = [];
            List<ProjectileFreezeTarget> collectedProjectiles = [];
            CyberspacePlayer cp = Cyberspace.For(owner);
            Vector2 domainCenter = owner.Center;
            float effectiveRadius = cp.Radius * cp.ExpandProgress;
            if (!IsFinite(domainCenter) || !float.IsFinite(effectiveRadius)
                || effectiveRadius <= 0f) {
                npcTargets = collectedNPCs;
                projectileTargets = collectedProjectiles;
                return;
            }
            float radiusSq = effectiveRadius * effectiveRadius;

            HashSet<int> processedGroups = [];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || IsNPCFrozen(i) || CyberBanish.IsBanishing(i)) {
                    continue;
                }

                Vector2 offset = npc.Center - domainCenter;
                if (offset.LengthSquared() > radiusSq) {
                    continue;
                }

                int anchor = NpcGroupHelper.GetAnchorIndex(npc);
                if (!processedGroups.Add(anchor)) {
                    continue;
                }

                NpcGroupHelper.ForEachGroupMember(npc, member => {
                    int index = member.whoAmI;
                    if (!member.active || IsNPCFrozen(index)
                        || CyberBanish.IsBanishing(index)) {
                        return;
                    }
                    collectedNPCs.Add(new NPCFreezeTarget(index, member.type,
                        Main.rand.NextFloat(), member.Center));
                });
            }

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.friendly
                    || Main.projPet[projectile.type] || projectile.minion
                    || Main.projHook[projectile.type] || IsProjectileFrozen(i)) {
                    continue;
                }

                Vector2 offset = projectile.Center - domainCenter;
                if (offset.LengthSquared() > radiusSq) {
                    continue;
                }

                collectedProjectiles.Add(new ProjectileFreezeTarget(i,
                    (byte)projectile.owner, projectile.identity, projectile.type,
                    Main.rand.NextFloat(), projectile.Center));
            }
            npcTargets = collectedNPCs;
            projectileTargets = collectedProjectiles;
        }

        /// <summary>写入服务端接受的冻结名单</summary>
        private static void ApplyFreezeBatch(int ownerWho, long activationId,
            List<NPCFreezeTarget> npcEntries,
            List<ProjectileFreezeTarget> projectileEntries,
            bool replaceExisting,
            out List<NPCFreezeTarget> acceptedNPCs,
            out List<ProjectileFreezeTarget> acceptedProjectiles) {
            acceptedNPCs = new List<NPCFreezeTarget>(npcEntries.Count);
            acceptedProjectiles = new List<ProjectileFreezeTarget>(projectileEntries.Count);
            for (int i = 0; i < npcEntries.Count; i++) {
                NPCFreezeTarget target = npcEntries[i];
                int idx = target.Index;
                if (idx < 0 || idx >= Main.maxNPCs) continue;
                if (!IsFinite(target.Center) || !float.IsFinite(target.Seed)) continue;
                NPC npc = Main.npc[idx];
                if (!npc.active || npc.type != target.Type) continue;
                if (replaceExisting && !PrepareAuthoritativeNPCSlot(npc, activationId)) {
                    continue;
                }
                if (IsNPCFrozen(idx)
                    || !replaceExisting && CyberBanish.IsBanishing(idx)) continue;
                TimeFreezeLease lease = TimeFreezeSystem.AcquireNPC<CyberDomainFreeze>(
                    npc, target.Center, activationId,
                    TimeFreezeAnchorPriority.Authoritative);
                if (!lease.IsValid) continue;
                FrozenNPCs.Add(new FreezeEntry {
                    EntityIndex = idx,
                    Timer = 0,
                    Duration = DefaultFreezeDuration,
                    FreezePosition = target.Center,
                    Seed = target.Seed,
                    FreezeVelocity = lease.ResumeVelocity,
                    FreezeLease = lease,
                    OwnerWho = ownerWho,
                });
                acceptedNPCs.Add(target);
            }
            for (int i = 0; i < projectileEntries.Count; i++) {
                ProjectileFreezeTarget target = projectileEntries[i];
                int idx = target.Index;
                if (idx < 0 || idx >= Main.maxProjectiles) continue;
                if (!IsFinite(target.Center) || !float.IsFinite(target.Seed)) continue;
                Projectile projectile = Main.projectile[idx];
                if (!projectile.active || projectile.type != target.Type
                    || projectile.owner != target.Owner
                    || projectile.identity != target.Identity) continue;
                if (replaceExisting
                    && !PrepareAuthoritativeProjectileSlot(projectile, activationId)) {
                    continue;
                }
                if (IsProjectileFrozen(idx)) continue;
                TimeFreezeLease lease = TimeFreezeSystem.AcquireProjectile<CyberDomainFreeze>(
                    projectile, target.Center, activationId,
                    TimeFreezeAnchorPriority.Authoritative);
                if (!lease.IsValid) continue;
                FrozenProjectiles.Add(new FreezeProjEntry {
                    EntityIndex = idx,
                    Timer = 0,
                    Duration = DefaultFreezeDuration,
                    FreezePosition = target.Center,
                    Seed = target.Seed,
                    FreezeVelocity = lease.ResumeVelocity,
                    FreezeLease = lease,
                    OwnerWho = ownerWho,
                });
                acceptedProjectiles.Add(target);
            }
        }

        private static void SendFreezeRequest() {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberDomainFreezeStart);
            packet.Write((byte)FreezePacketKind.Request);
            packet.Send();
        }

        private static void BroadcastApply(int ownerWho, long activationId,
            List<NPCFreezeTarget> npcEntries,
            List<ProjectileFreezeTarget> projectileEntries) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberDomainFreezeStart);
            packet.Write((byte)FreezePacketKind.Apply);
            packet.Write((byte)ownerWho);
            packet.Write(activationId);
            packet.Write((ushort)npcEntries.Count);
            for (int i = 0; i < npcEntries.Count; i++) {
                packet.Write((ushort)npcEntries[i].Index);
                packet.Write(npcEntries[i].Type);
                packet.Write(npcEntries[i].Seed);
                packet.Write(npcEntries[i].Center.X);
                packet.Write(npcEntries[i].Center.Y);
            }
            packet.Write((ushort)projectileEntries.Count);
            for (int i = 0; i < projectileEntries.Count; i++) {
                packet.Write(projectileEntries[i].Owner);
                packet.Write(projectileEntries[i].Identity);
                packet.Write(projectileEntries[i].Type);
                packet.Write(projectileEntries[i].Seed);
                packet.Write(projectileEntries[i].Center.X);
                packet.Write(projectileEntries[i].Center.Y);
            }
            packet.Send();
        }

        /// <summary>按 owner+identity 解析弹幕索引</summary>
        private static int FindProjectileIndex(int projOwner, int projIdentity, int projType) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == projOwner && proj.identity == projIdentity
                    && proj.type == projType) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>远端冻结广播入队</summary>
        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            FreezePacketKind packetKind = (FreezePacketKind)reader.ReadByte();
            if (VaultUtils.isServer) {
                if (packetKind != FreezePacketKind.Request
                    || whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                    return;
                }
                ExecuteAuthoritativeFreeze(Main.player[whoAmI]);
                return;
            }
            if (packetKind != FreezePacketKind.Apply) {
                return;
            }

            int ownerWho = reader.ReadByte();
            long activationId = reader.ReadInt64();
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers || activationId == 0) {
                return;
            }
            int npcCount = reader.ReadUInt16();
            List<NPCFreezeTarget> npcEntries = new(npcCount);
            for (int i = 0; i < npcCount; i++) {
                int idx = reader.ReadUInt16();
                int type = reader.ReadInt32();
                float seed = reader.ReadSingle();
                Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
                npcEntries.Add(new NPCFreezeTarget(idx, type, seed, center));
            }
            int projCount = reader.ReadUInt16();
            List<ProjectileFreezeTarget> projectileEntries = new(projCount);
            for (int i = 0; i < projCount; i++) {
                byte projOwner = reader.ReadByte();
                int projIdentity = reader.ReadInt32();
                int projType = reader.ReadInt32();
                float seed = reader.ReadSingle();
                Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
                int idx = FindProjectileIndex(projOwner, projIdentity, projType);
                if (idx < 0) continue;
                projectileEntries.Add(new ProjectileFreezeTarget(idx, projOwner,
                    projIdentity, projType, seed, center));
            }

            ApplyFreezeBatch(ownerWho, activationId, npcEntries, projectileEntries,
                replaceExisting: true,
                out _, out _);
        }

        private static bool PrepareAuthoritativeNPCSlot(NPC npc, long activationId) {
            bool alreadyApplied = false;
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                if (entry.EntityIndex != npc.whoAmI) {
                    continue;
                }
                if (TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)
                    && entry.FreezeLease.Source.InstanceId == activationId) {
                    alreadyApplied = true;
                    continue;
                }
                TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease,
                    entry.FreezeVelocity * 0.5f, TimeFreezeResumePriority.Domain);
                FrozenNPCs.RemoveAt(i);
            }
            return !alreadyApplied;
        }

        private static bool PrepareAuthoritativeProjectileSlot(Projectile projectile,
            long activationId) {
            bool alreadyApplied = false;
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                if (entry.EntityIndex != projectile.whoAmI) {
                    continue;
                }
                if (TimeFreezeSystem.IsLeaseActive(projectile, entry.FreezeLease)
                    && entry.FreezeLease.Source.InstanceId == activationId) {
                    alreadyApplied = true;
                    continue;
                }
                TimeFreezeSystem.ReleaseProjectile(projectile, entry.FreezeLease,
                    entry.FreezeVelocity, TimeFreezeResumePriority.Domain);
                FrozenProjectiles.RemoveAt(i);
            }
            return !alreadyApplied;
        }

        private static long AllocateActivationId() {
            nextActivationId++;
            if (nextActivationId == 0) {
                nextActivationId++;
            }
            return nextActivationId;
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        /// <summary>每帧更新冻结实体</summary>
        public static void Update() {
            UpdateFrozenNPCs();
            UpdateFrozenProjectiles();
        }

        private static void UpdateFrozenNPCs() {
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                entry.Timer += TimeGear.PullFrameAdvance(ref entry.TimerCarry);

                NPC npc = Main.npc[entry.EntityIndex];
                if (!npc.active || !TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)) {
                    FrozenNPCs.RemoveAt(i);
                    continue;
                }

                //整组离发起者域则快进解冻
                int thawStart = Math.Max(0, entry.Duration - 90);
                if (entry.Timer < thawStart
                    && !Cyberspace.IsInsideDomainOf(entry.OwnerWho, npc.Center)
                    && !AnyGroupMemberInDomain(npc, entry.OwnerWho)) {
                    entry.Timer = thawStart;
                }

                //冻结粒子，仅客户端
                if (!Main.dedServ) {
                    CyberDomainFreezeParticles.SpawnFreezeParticles(npc, entry.Progress, entry.Seed);
                }

                //末15%解冻演出，抖动仅客户端
                float progress = entry.Progress;
                if (progress > 0.85f && !Main.dedServ) {
                    float thawPhase = (progress - 0.85f) / 0.15f;
                    //解冻前速度抖
                    float jitter = thawPhase * 2f;
                    npc.position += new Vector2(
                        Main.rand.NextFloat(-jitter, jitter),
                        Main.rand.NextFloat(-jitter, jitter));
                }

                //音效仅锚点节
                if (entry.Timer == thawStart && NpcGroupHelper.GetAnchorIndex(npc) == npc.whoAmI) {
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(CWRSound.FaultTransition, npc.Center);
                    }
                }

                //到期解冻
                if (entry.Timer >= entry.Duration) {
                    TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease,
                        entry.FreezeVelocity * 0.5f, TimeFreezeResumePriority.Domain);
                    if (!Main.dedServ) {
                        CyberDomainFreezeParticles.SpawnThawBurst(npc.Center);
                    }
                    FrozenNPCs.RemoveAt(i);
                }
            }
        }

        private static bool AnyGroupMemberInDomain(NPC npc, int ownerWho) {
            int anchor = NpcGroupHelper.GetAnchorIndex(npc);
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (i == npc.whoAmI) continue;
                NPC other = Main.npc[i];
                if (!other.active) continue;
                if (NpcGroupHelper.GetAnchorIndex(other) != anchor) continue;
                if (Cyberspace.IsInsideDomainOf(ownerWho, other.Center)) return true;
            }
            return false;
        }

        private static void UpdateFrozenProjectiles() {
            for (int i = FrozenProjectiles.Count - 1; i >= 0; i--) {
                FreezeProjEntry entry = FrozenProjectiles[i];
                entry.Timer += TimeGear.PullFrameAdvance(ref entry.TimerCarry);

                Projectile proj = Main.projectile[entry.EntityIndex];
                if (!proj.active || !TimeFreezeSystem.IsLeaseActive(proj, entry.FreezeLease)) {
                    FrozenProjectiles.RemoveAt(i);
                    continue;
                }

                //到期解冻
                if (entry.Timer >= entry.Duration) {
                    TimeFreezeSystem.ReleaseProjectile(proj, entry.FreezeLease,
                        entry.FreezeVelocity, TimeFreezeResumePriority.Domain);
                    FrozenProjectiles.RemoveAt(i);
                }
            }
        }

        public static void Reset() {
            foreach (FreezeEntry entry in FrozenNPCs) {
                if (entry.EntityIndex >= 0 && entry.EntityIndex < Main.maxNPCs) {
                    NPC npc = Main.npc[entry.EntityIndex];
                    TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease,
                        entry.FreezeVelocity, TimeFreezeResumePriority.Domain);
                }
            }
            foreach (FreezeProjEntry entry in FrozenProjectiles) {
                if (entry.EntityIndex >= 0 && entry.EntityIndex < Main.maxProjectiles) {
                    Projectile projectile = Main.projectile[entry.EntityIndex];
                    TimeFreezeSystem.ReleaseProjectile(projectile, entry.FreezeLease,
                        entry.FreezeVelocity, TimeFreezeResumePriority.Domain);
                }
            }
            FrozenNPCs.Clear();
            FrozenProjectiles.Clear();
            nextActivationId = 0;
        }

        private static bool IsEntryActive(FreezeEntry entry) {
            if (entry.EntityIndex < 0 || entry.EntityIndex >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[entry.EntityIndex];
            return TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease);
        }

        private static bool IsEntryActive(FreezeProjEntry entry) {
            if (entry.EntityIndex < 0 || entry.EntityIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile projectile = Main.projectile[entry.EntityIndex];
            return TimeFreezeSystem.IsLeaseActive(projectile, entry.FreezeLease);
        }
    }

    /// <summary>NPC 冻结条目</summary>
    internal class FreezeEntry
    {
        public int EntityIndex;
        public int Timer;
        internal float TimerCarry;
        public int Duration;
        public Vector2 FreezePosition;
        public Vector2 FreezeVelocity;
        internal TimeFreezeLease FreezeLease;
        public float Seed;
        /// <summary>发起者 whoAmI，域外快速解冻判定</summary>
        public int OwnerWho;

        public float Progress => (float)Timer / Duration;
    }

    /// <summary>弹幕冻结条目</summary>
    internal class FreezeProjEntry
    {
        public int EntityIndex;
        public int Timer;
        internal float TimerCarry;
        public int Duration;
        public Vector2 FreezePosition;
        public Vector2 FreezeVelocity;
        internal TimeFreezeLease FreezeLease;
        public float Seed;
        /// <summary>发起者 whoAmI</summary>
        public int OwnerWho;

        public float Progress => (float)Timer / Duration;
    }
}
