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
                if (FrozenNPCs[i].EntityIndex == npcIndex)
                    return true;
            }
            return false;
        }

        /// <summary>弹幕是否冻结中</summary>
        public static bool IsProjectileFrozen(int projIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                if (FrozenProjectiles[i].EntityIndex == projIndex)
                    return true;
            }
            return false;
        }

        /// <summary>NPC 冻结进度 0~1，未冻结 -1</summary>
        public static float GetNPCFreezeProgress(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex)
                    return FrozenNPCs[i].Progress;
            }
            return -1f;
        }

        /// <summary>弹幕冻结进度</summary>
        public static float GetProjectileFreezeProgress(int projIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                if (FrozenProjectiles[i].EntityIndex == projIndex)
                    return FrozenProjectiles[i].Progress;
            }
            return -1f;
        }

        /// <summary>NPC 冻结种子</summary>
        public static float GetNPCSeed(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex)
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

            Vector2 domainCenter = owner.Center;
            float effectiveRadius = cp.Radius * cp.ExpandProgress;
            float radiusSq = effectiveRadius * effectiveRadius;

            //先算名单/种子/锚点，再应用+广播
            //锚点随包广播，解冻不跳变
            //同组一并冻
            List<(int idx, float seed, Vector2 center)> npcEntries = new();
            HashSet<int> processedGroups = new HashSet<int>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active) continue;
                if (IsNPCFrozen(i)) continue;
                if (CyberBanish.IsBanishing(i)) continue;

                float dx = npc.Center.X - domainCenter.X;
                float dy = npc.Center.Y - domainCenter.Y;
                if (dx * dx + dy * dy > radiusSq) continue;

                int anchor = NpcGroupHelper.GetAnchorIndex(npc);
                if (!processedGroups.Add(anchor)) continue;

                NpcGroupHelper.ForEachGroupMember(npc, member => {
                    int idx = member.whoAmI;
                    if (IsNPCFrozen(idx) || CyberBanish.IsBanishing(idx)) return;
                    npcEntries.Add((idx, Main.rand.NextFloat(), member.Center));
                });
            }

            List<(int idx, float seed, Vector2 center)> projEntries = new();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active) continue;
                if (proj.friendly) continue;
                if (Main.projPet[proj.type] || proj.minion || Main.projHook[proj.type]) continue;
                if (IsProjectileFrozen(i)) continue;

                float dx = proj.Center.X - domainCenter.X;
                float dy = proj.Center.Y - domainCenter.Y;
                if (dx * dx + dy * dy > radiusSq) continue;

                projEntries.Add((i, Main.rand.NextFloat(), proj.Center));
            }

            //本机先入 list，再广播
            ApplyFreezeBatch(owner.whoAmI, npcEntries, projEntries);

            //冻结能量波
            if (Main.myPlayer == owner.whoAmI) {
                IEntitySource source = owner.GetSource_FromThis();
                Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberFreezeWaveProj>(), 0, 0, owner.whoAmI);
            }

            //广播
            //弹幕用 owner+identity
            if (Main.netMode != NetmodeID.SinglePlayer) {
                List<(byte projOwner, int projIdentity, float seed, Vector2 center)> projPairs = new(projEntries.Count);
                for (int i = 0; i < projEntries.Count; i++) {
                    Projectile proj = Main.projectile[projEntries[i].idx];
                    projPairs.Add(((byte)proj.owner, proj.identity, projEntries[i].seed, projEntries[i].center));
                }
                BroadcastStart(owner.whoAmI, npcEntries, projPairs, ignoreClient: -1);
            }
        }

        /// <summary>写入 Frozen 列表并冻住速度，锚点坐标 net 统一</summary>
        private static void ApplyFreezeBatch(int ownerWho,
            List<(int idx, float seed, Vector2 center)> npcEntries,
            List<(int idx, float seed, Vector2 center)> projEntries) {
            for (int i = 0; i < npcEntries.Count; i++) {
                int idx = npcEntries[i].idx;
                if (idx < 0 || idx >= Main.maxNPCs) continue;
                NPC npc = Main.npc[idx];
                if (!npc.active) continue;
                if (IsNPCFrozen(idx) || CyberBanish.IsBanishing(idx)) continue;
                FrozenNPCs.Add(new FreezeEntry {
                    EntityIndex = idx,
                    Timer = 0,
                    Duration = DefaultFreezeDuration,
                    FreezePosition = npcEntries[i].center,
                    Seed = npcEntries[i].seed,
                    FreezeVelocity = npc.velocity,
                    OwnerWho = ownerWho,
                });
                npc.velocity = Vector2.Zero;
            }
            for (int i = 0; i < projEntries.Count; i++) {
                int idx = projEntries[i].idx;
                if (idx < 0 || idx >= Main.maxProjectiles) continue;
                Projectile proj = Main.projectile[idx];
                if (!proj.active) continue;
                if (IsProjectileFrozen(idx)) continue;
                FrozenProjectiles.Add(new FreezeProjEntry {
                    EntityIndex = idx,
                    Timer = 0,
                    Duration = DefaultFreezeDuration,
                    FreezePosition = projEntries[i].center,
                    Seed = projEntries[i].seed,
                    FreezeVelocity = proj.velocity,
                    OwnerWho = ownerWho,
                });
                proj.velocity = Vector2.Zero;
            }
        }

        private static void BroadcastStart(int ownerWho,
            List<(int idx, float seed, Vector2 center)> npcEntries,
            List<(byte projOwner, int projIdentity, float seed, Vector2 center)> projPairs,
            int ignoreClient) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberDomainFreezeStart);
            packet.Write((byte)ownerWho);
            packet.Write((ushort)npcEntries.Count);
            for (int i = 0; i < npcEntries.Count; i++) {
                packet.Write((ushort)npcEntries[i].idx);
                packet.Write(npcEntries[i].seed);
                packet.Write(npcEntries[i].center.X);
                packet.Write(npcEntries[i].center.Y);
            }
            packet.Write((ushort)projPairs.Count);
            for (int i = 0; i < projPairs.Count; i++) {
                packet.Write(projPairs[i].projOwner);
                packet.Write(projPairs[i].projIdentity);
                packet.Write(projPairs[i].seed);
                packet.Write(projPairs[i].center.X);
                packet.Write(projPairs[i].center.Y);
            }
            packet.Send(-1, ignoreClient);
        }

        /// <summary>按 owner+identity 解析弹幕索引</summary>
        private static int FindProjectileIndex(int projOwner, int projIdentity) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == projOwner && proj.identity == projIdentity) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>远端冻结广播入队</summary>
        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            int ownerWho = reader.ReadByte();
            int npcCount = reader.ReadUInt16();
            List<(int idx, float seed, Vector2 center)> npcEntries = new(npcCount);
            for (int i = 0; i < npcCount; i++) {
                int idx = reader.ReadUInt16();
                float seed = reader.ReadSingle();
                Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
                npcEntries.Add((idx, seed, center));
            }
            int projCount = reader.ReadUInt16();
            List<(byte projOwner, int projIdentity, float seed, Vector2 center)> projPairs = new(projCount);
            for (int i = 0; i < projCount; i++) {
                byte projOwner = reader.ReadByte();
                int projIdentity = reader.ReadInt32();
                float seed = reader.ReadSingle();
                Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
                projPairs.Add((projOwner, projIdentity, seed, center));
            }

            //owner+identity 解析，未同步则跳过
            List<(int idx, float seed, Vector2 center)> projEntries = new(projPairs.Count);
            for (int i = 0; i < projPairs.Count; i++) {
                int idx = FindProjectileIndex(projPairs[i].projOwner, projPairs[i].projIdentity);
                if (idx < 0) continue;
                projEntries.Add((idx, projPairs[i].seed, projPairs[i].center));
            }

            ApplyFreezeBatch(ownerWho, npcEntries, projEntries);

            //服务端转发，保留原始 owner+identity
            if (VaultUtils.isServer) {
                BroadcastStart(ownerWho, npcEntries, projPairs, ignoreClient: whoAmI);
            }
        }

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
                if (!npc.active) {
                    FrozenNPCs.RemoveAt(i);
                    continue;
                }

                if (entry.Timer > 0) {
                    npc.CWR().TimeFrozenTick = 2;
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
                    npc.velocity = entry.FreezeVelocity * 0.5f;
                    npc.CWR().TimeFrozenTick = 0;
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
                if (!proj.active) {
                    FrozenProjectiles.RemoveAt(i);
                    continue;
                }

                if (entry.Timer > 0) {
                    proj.CWR().TimeFrozenTick = 2;
                }

                //到期解冻
                if (entry.Timer >= entry.Duration) {
                    proj.velocity = entry.FreezeVelocity;
                    proj.CWR().TimeFrozenTick = 0;
                    FrozenProjectiles.RemoveAt(i);
                }
            }
        }

        public static void Reset() {
            FrozenNPCs.Clear();
            FrozenProjectiles.Clear();
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
        public float Seed;
        /// <summary>发起者 whoAmI</summary>
        public int OwnerWho;

        public float Progress => (float)Timer / Duration;
    }
}
