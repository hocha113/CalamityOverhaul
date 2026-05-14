using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.RAMSystems;
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
    /// <summary>
    /// 赛博领域冻结系统 —— 状态管理器
    /// <br/>按下按键后冻结领域范围内所有敌对NPC和弹幕，每个实体有独立的冻结计时
    /// <br/>多人语义：本地玩家发起后，先在本机决定哪些 NPC/弹幕进入冻结，
    /// 再通过 <see cref="CWRMessageType.CyberDomainFreezeStart"/> 把名单广播给其它客户端及服务端，
    /// 让所有客户端都进入相同的冻结状态（视觉滤镜 + AI 拦截一致），并由服务端真正阻断 NPC 行为
    /// <br/>冻结时生成黑墙风格能量波扩散演出
    /// <br/>被冻结的实体带有故障滤镜 + 六角能量网格覆盖效果
    /// </summary>
    internal class CyberDomainFreeze : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>
        /// 默认冻结时长（帧，10秒 = 600帧）
        /// </summary>
        public const int DefaultFreezeDuration = 600;

        /// <summary>
        /// 触发领域冻结的RAM消耗量
        /// </summary>
        public const int RamCost = 4;

        /// <summary>
        /// 当前正在被冻结的NPC列表
        /// </summary>
        public static readonly List<FreezeEntry> FrozenNPCs = [];

        /// <summary>
        /// 当前正在被冻结的弹幕列表
        /// </summary>
        public static readonly List<FreezeProjEntry> FrozenProjectiles = [];

        /// <summary>
        /// 判断某NPC是否正在被冻结
        /// </summary>
        public static bool IsNPCFrozen(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断某弹幕是否正在被冻结
        /// </summary>
        public static bool IsProjectileFrozen(int projIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                if (FrozenProjectiles[i].EntityIndex == projIndex)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取某NPC的冻结进度 (0=刚冻结, 1=即将解冻)，不在冻结中返回-1
        /// </summary>
        public static float GetNPCFreezeProgress(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex)
                    return FrozenNPCs[i].Progress;
            }
            return -1f;
        }

        /// <summary>
        /// 获取某弹幕的冻结进度
        /// </summary>
        public static float GetProjectileFreezeProgress(int projIndex) {
            for (int i = 0; i < FrozenProjectiles.Count; i++) {
                if (FrozenProjectiles[i].EntityIndex == projIndex)
                    return FrozenProjectiles[i].Progress;
            }
            return -1f;
        }

        /// <summary>
        /// 获取某NPC的冻结种子
        /// </summary>
        public static float GetNPCSeed(int npcIndex) {
            for (int i = 0; i < FrozenNPCs.Count; i++) {
                if (FrozenNPCs[i].EntityIndex == npcIndex)
                    return FrozenNPCs[i].Seed;
            }
            return 0f;
        }

        /// <summary>
        /// 触发领域冻结：冻结领域内所有敌对实体 + 生成能量波 + 通过网络广播给所有客户端
        /// <br/>仅本地玩家从输入处理调用进入；远端玩家由 <see cref="HandleNetStart"/> 复刻同一帧的冻结结果
        /// </summary>
        public static void TriggerFreeze(Player owner) {
            if (owner == null) return;
            CyberspacePlayer cp = Cyberspace.For(owner);
            if (cp == null) return;
            if (!cp.Active || cp.Intensity < 0.5f || cp.CurrentLayer < Cyberspace.MaxLayerCount) return;

            //RAM检查：消耗极高，不足时触发HUD故障闪烁并拦截（仅本机看到，因为其它客户端不会触发此函数）
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

            //先在本地计算所有应被冻结的 NPC / 弹幕索引和种子，再统一应用 + 广播
            //同组（蠕虫等多节实体）视为整体一并冻结
            List<(int idx, float seed)> npcEntries = new();
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
                    npcEntries.Add((idx, Main.rand.NextFloat()));
                });
            }

            List<(int idx, float seed)> projEntries = new();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active) continue;
                if (proj.friendly) continue;
                if (Main.projPet[proj.type] || proj.minion || Main.projHook[proj.type]) continue;
                if (IsProjectileFrozen(i)) continue;

                float dx = proj.Center.X - domainCenter.X;
                float dy = proj.Center.Y - domainCenter.Y;
                if (dx * dx + dy * dy > radiusSq) continue;

                projEntries.Add((i, Main.rand.NextFloat()));
            }

            //本机先把这批冻结记录入 list（让本地立刻看到效果），再广播给其它端
            ApplyFreezeBatch(owner.whoAmI, npcEntries, projEntries);

            //生成黑墙能量波弹幕（NewProjectile 自带网络同步，远端会看到能量波）
            if (Main.myPlayer == owner.whoAmI) {
                IEntitySource source = owner.GetSource_FromThis();
                Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberFreezeWaveProj>(), 0, 0, owner.whoAmI);
            }

            //广播给其它客户端 / 服务端
            if (Main.netMode != NetmodeID.SinglePlayer) {
                BroadcastStart(owner.whoAmI, npcEntries, projEntries, ignoreClient: -1);
            }
        }

        /// <summary>
        /// 把名单写入本机的 FrozenNPCs / FrozenProjectiles，并冻住实体的速度
        /// </summary>
        private static void ApplyFreezeBatch(int ownerWho,
            List<(int idx, float seed)> npcEntries, List<(int idx, float seed)> projEntries) {
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
                    FreezePosition = npc.Center,
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
                    FreezePosition = proj.Center,
                    Seed = projEntries[i].seed,
                    FreezeVelocity = proj.velocity,
                    OwnerWho = ownerWho,
                });
                proj.velocity = Vector2.Zero;
            }
        }

        private static void BroadcastStart(int ownerWho,
            List<(int idx, float seed)> npcEntries, List<(int idx, float seed)> projEntries,
            int ignoreClient) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberDomainFreezeStart);
            packet.Write((byte)ownerWho);
            packet.Write((ushort)npcEntries.Count);
            for (int i = 0; i < npcEntries.Count; i++) {
                packet.Write((ushort)npcEntries[i].idx);
                packet.Write(npcEntries[i].seed);
            }
            packet.Write((ushort)projEntries.Count);
            for (int i = 0; i < projEntries.Count; i++) {
                packet.Write((ushort)projEntries[i].idx);
                packet.Write(projEntries[i].seed);
            }
            packet.Send(-1, ignoreClient);
        }

        /// <summary>
        /// 收到远端冻结广播：在本机也把这批实体冻起来
        /// </summary>
        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            int ownerWho = reader.ReadByte();
            int npcCount = reader.ReadUInt16();
            List<(int idx, float seed)> npcEntries = new(npcCount);
            for (int i = 0; i < npcCount; i++) {
                int idx = reader.ReadUInt16();
                float seed = reader.ReadSingle();
                npcEntries.Add((idx, seed));
            }
            int projCount = reader.ReadUInt16();
            List<(int idx, float seed)> projEntries = new(projCount);
            for (int i = 0; i < projCount; i++) {
                int idx = reader.ReadUInt16();
                float seed = reader.ReadSingle();
                projEntries.Add((idx, seed));
            }

            ApplyFreezeBatch(ownerWho, npcEntries, projEntries);

            //服务端转发给除发送者外的其它客户端，让所有客户端共享同一份冻结名单
            if (VaultUtils.isServer) {
                BroadcastStart(ownerWho, npcEntries, projEntries, ignoreClient: whoAmI);
            }
        }

        /// <summary>
        /// 每帧更新所有冻结实体
        /// </summary>
        public static void Update() {
            UpdateFrozenNPCs();
            UpdateFrozenProjectiles();
        }

        private static void UpdateFrozenNPCs() {
            for (int i = FrozenNPCs.Count - 1; i >= 0; i--) {
                FreezeEntry entry = FrozenNPCs[i];
                entry.Timer++;

                NPC npc = Main.npc[entry.EntityIndex];
                if (!npc.active) {
                    FrozenNPCs.RemoveAt(i);
                    continue;
                }

                if (entry.Timer > 0) {
                    npc.CWR().TimeFrozenTick = 2;
                }

                // 如果整个群组都已离开"冻结发起者"的领域，快速推进到解冻演出阶段
                int thawStart = Math.Max(0, entry.Duration - 90);
                if (entry.Timer < thawStart
                    && !Cyberspace.IsInsideDomainOf(entry.OwnerWho, npc.Center)
                    && !AnyGroupMemberInDomain(npc, entry.OwnerWho)) {
                    entry.Timer = thawStart;
                }

                // 生成冻结粒子（仅客户端）
                if (!Main.dedServ) {
                    CyberDomainFreezeParticles.SpawnFreezeParticles(npc, entry.Progress, entry.Seed);
                }

                // 解冻动画（最后15%时间）
                float progress = entry.Progress;
                if (progress > 0.85f) {
                    float thawPhase = (progress - 0.85f) / 0.15f;
                    // 逐渐恢复一点速度抖动表示即将解冻
                    float jitter = thawPhase * 2f;
                    npc.position += new Vector2(
                        Main.rand.NextFloat(-jitter, jitter),
                        Main.rand.NextFloat(-jitter, jitter));
                }

                // 音效只由锚点节段（头部或单体）播放，避免蠕虫群组同帧触发N次
                if (entry.Timer == thawStart && NpcGroupHelper.GetAnchorIndex(npc) == npc.whoAmI) {
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(CWRSound.FaultTransition, npc.Center);
                    }
                }

                // 冻结时间结束 → 解冻
                if (entry.Timer >= entry.Duration) {
                    // 恢复原始速度
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
                entry.Timer++;

                Projectile proj = Main.projectile[entry.EntityIndex];
                if (!proj.active) {
                    FrozenProjectiles.RemoveAt(i);
                    continue;
                }

                if (entry.Timer > 0) {
                    proj.CWR().TimeFrozenTick = 2;
                }

                // 冻结时间结束 → 解冻
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

    /// <summary>
    /// NPC冻结数据条目
    /// </summary>
    internal class FreezeEntry
    {
        public int EntityIndex;
        public int Timer;
        public int Duration;
        public Vector2 FreezePosition;
        public Vector2 FreezeVelocity;
        public float Seed;
        /// <summary>
        /// 冻结发起者的玩家索引，用于"是否仍在该玩家领域内"的快速解冻判定
        /// </summary>
        public int OwnerWho;

        public float Progress => (float)Timer / Duration;
    }

    /// <summary>
    /// 弹幕冻结数据条目
    /// </summary>
    internal class FreezeProjEntry
    {
        public int EntityIndex;
        public int Timer;
        public int Duration;
        public Vector2 FreezePosition;
        public Vector2 FreezeVelocity;
        public float Seed;
        /// <summary>
        /// 冻结发起者的玩家索引
        /// </summary>
        public int OwnerWho;

        public float Progress => (float)Timer / Duration;
    }
}
