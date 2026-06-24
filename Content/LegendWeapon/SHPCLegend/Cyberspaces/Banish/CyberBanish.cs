using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>领域放逐管理：故障滤镜→抹除；CyberspaceSystem 驱动 net 同步</summary>
    internal class CyberBanish : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>
        /// 放逐动画总时长（帧，约1.8秒 = 108帧）
        /// </summary>
        public const int BanishDuration = 108;

        /// <summary>
        /// 单次放逐消耗的RAM量
        /// </summary>
        public const int RamCostPerCast = 5;

        /// <summary>
        /// 当前正在被放逐的NPC列表
        /// </summary>
        public static readonly List<BanishEntry> ActiveBanishments = [];

        /// <summary>
        /// 判断某NPC是否正在被放逐
        /// </summary>
        public static bool IsBanishing(int npcIndex) {
            for (int i = 0; i < ActiveBanishments.Count; i++) {
                if (ActiveBanishments[i].NpcIndex == npcIndex)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取某NPC的放逐进度 (0=刚开始, 1=完成)，不在放逐中返回-1
        /// </summary>
        public static float GetProgress(int npcIndex) {
            for (int i = 0; i < ActiveBanishments.Count; i++) {
                if (ActiveBanishments[i].NpcIndex == npcIndex)
                    return ActiveBanishments[i].Progress;
            }
            return -1f;
        }

        /// <summary>
        /// 光标下 NPC 放逐，输入侧 myPlayer
        /// </summary>
        public static void BanishAtCursor() {
            CyberspacePlayer cp = Cyberspace.Local;
            if (cp == null) return;
            if (!cp.Active || cp.Intensity < 0.5f || cp.CurrentLayer < 2) return;

            //找目标再分 Boss/普怪 RAM 路径
            int hitIndex = FindCursorTarget(cp);
            if (hitIndex < 0) return;

            NPC hitNpc = Main.npc[hitIndex];
            bool boss = CyberBossExecution.IsBossTier(hitNpc);
            int ramCost = boss ? CyberBossExecution.RamCostPerCast : RamCostPerCast;

            //RAM检查：不足时触发HUD故障闪烁提示
            if (!HackTime.InfiniteHack && (RamSystem.IsLocked || !RamSystem.CanAfford(ramCost))) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.4f,
                        Pitch = -0.3f,
                    }, Main.LocalPlayer.Center);
                    RamSystem.NotifyInsufficient();
                    Terraria.CombatText.NewText(Main.LocalPlayer.Hitbox, new Microsoft.Xna.Framework.Color(255, 90, 80), "// LOW RAM", true);
                }
                return;
            }

            //命中目标后消耗RAM
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume(ramCost);
            }

            //收集本次受影响的所有 NPC 索引（命中目标 + 同组成员），统一给一份种子
            //冻结锚点坐标也一并收集并随包广播，保证所有端的放逐演出钉在同一位置
            List<(int idx, float seed, Vector2 center)> entries = new();
            entries.Add((hitIndex, Main.rand.NextFloat(), hitNpc.Center));
            NPC root = Main.npc[hitIndex];
            NpcGroupHelper.CollectGroupIndices(root, banishGroupBuffer);
            for (int i = 0; i < banishGroupBuffer.Count; i++) {
                int memberIdx = banishGroupBuffer[i];
                if (memberIdx == hitIndex) continue;
                if (IsBanishing(memberIdx)) continue;
                entries.Add((memberIdx, Main.rand.NextFloat(), Main.npc[memberIdx].Center));
            }
            banishGroupBuffer.Clear();

            int ownerWho = Main.myPlayer;

            //先在本机应用，让本地立刻看到放逐演出
            ApplyBanishBatch(ownerWho, boss, entries);

            //广播给其它客户端 / 服务端
            if (Main.netMode != NetmodeID.SinglePlayer) {
                BroadcastStart(ownerWho, boss, entries, ignoreClient: -1);
            }
        }

        /// <summary>
        /// 领域内光标最近未放逐 NPC，无则 -1
        /// </summary>
        private static int FindCursorTarget(CyberspacePlayer cp) {
            Vector2 mouse = Main.MouseWorld;
            Vector2 domainCenter = cp.DomainCenter;
            float effectiveRadius = cp.Radius * cp.ExpandProgress;

            int bestIndex = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.townNPC) continue;

                float dx = npc.Center.X - domainCenter.X;
                float dy = npc.Center.Y - domainCenter.Y;
                if (dx * dx + dy * dy > effectiveRadius * effectiveRadius) continue;

                if (IsBanishing(i)) continue;
                if (CyberBossExecution.IsExecuting(i)) continue;

                Rectangle hitbox = npc.Hitbox;
                hitbox.Inflate(8, 8);
                if (!hitbox.Contains(mouse.ToPoint())) continue;

                float distSq = Vector2.DistanceSquared(npc.Center, mouse);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        //群组放逐用的复用缓冲，避免每次扩散都重新分配
        private static readonly List<int> banishGroupBuffer = [];

        /// <summary>
        /// 把名单写入本机的 ActiveBanishments，并冻住每个 NPC 的速度 + 播放放逐起手音效
        /// <br/>冻结锚点使用包内统一坐标，保证所有端的放逐演出钉在同一位置
        /// </summary>
        private static void ApplyBanishBatch(int ownerWho, bool isBoss, List<(int idx, float seed, Vector2 center)> entries) {
            for (int i = 0; i < entries.Count; i++) {
                int idx = entries[i].idx;
                if (idx < 0 || idx >= Main.maxNPCs) continue;
                NPC npc = Main.npc[idx];
                if (!npc.active) continue;
                if (IsBanishing(idx)) continue;

                ActiveBanishments.Add(new BanishEntry {
                    NpcIndex = idx,
                    Timer = 0,
                    OriginalScale = npc.scale,
                    FreezePosition = entries[i].center,
                    Seed = entries[i].seed,
                    IsBoss = isBoss,
                    OwnerWho = ownerWho,
                    ExecutionTriggered = false,
                });

                npc.velocity = Vector2.Zero;

                //音效定位到 NPC 中心，远端客户端就能在被放逐目标的位置听见，而不是定在自己玩家身上
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.Fault, npc.Center);
                }
            }
        }

        private static void BroadcastStart(int ownerWho, bool isBoss,
            List<(int idx, float seed, Vector2 center)> entries, int ignoreClient) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberBanishStart);
            packet.Write((byte)ownerWho);
            packet.Write(isBoss);
            packet.Write((ushort)entries.Count);
            for (int i = 0; i < entries.Count; i++) {
                packet.Write((ushort)entries[i].idx);
                packet.Write(entries[i].seed);
                packet.Write(entries[i].center.X);
                packet.Write(entries[i].center.Y);
            }
            packet.Send(-1, ignoreClient);
        }

        /// <summary>
        /// 收到远端放逐广播：在本机也把这批 NPC 加入放逐列表
        /// </summary>
        internal static void HandleNetStart(BinaryReader reader, int whoAmI) {
            int ownerWho = reader.ReadByte();
            bool isBoss = reader.ReadBoolean();
            int count = reader.ReadUInt16();
            List<(int idx, float seed, Vector2 center)> entries = new(count);
            for (int i = 0; i < count; i++) {
                int idx = reader.ReadUInt16();
                float seed = reader.ReadSingle();
                Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
                entries.Add((idx, seed, center));
            }

            ApplyBanishBatch(ownerWho, isBoss, entries);

            //服务端转发给除发送者外的其它客户端
            if (VaultUtils.isServer) {
                BroadcastStart(ownerWho, isBoss, entries, ignoreClient: whoAmI);
            }
        }

        /// <summary>
        /// 每帧更新所有活跃放逐
        /// </summary>
        public static void Update() {
            for (int i = ActiveBanishments.Count - 1; i >= 0; i--) {
                BanishEntry entry = ActiveBanishments[i];
                entry.Timer += TimeGear.PullFrameAdvance(ref entry.TimerCarry);

                NPC npc = Main.npc[entry.NpcIndex];
                if (!npc.active) {
                    ActiveBanishments.RemoveAt(i);
                    continue;
                }

                float progress = entry.Progress;

                //冻结NPC位置
                npc.Center = entry.FreezePosition;
                npc.velocity = Vector2.Zero;

                if (entry.IsBoss) {
                    //Boss版：不缩小、不抹除。末段触发雷击后仍保留滤镜到结束
                    if (!Main.dedServ) {
                        CyberBanishParticles.SpawnBanishParticles(npc, progress, entry.Seed);
                    }

                    if (!entry.ExecutionTriggered && progress >= 0.7f) {
                        entry.ExecutionTriggered = true;
                        Player owner = entry.OwnerWho >= 0 && entry.OwnerWho < Main.maxPlayers ? Main.player[entry.OwnerWho] : Main.LocalPlayer;
                        CyberBossExecution.StartExecution(entry.NpcIndex, owner);
                    }

                    if (entry.Timer >= BanishDuration) {
                        ActiveBanishments.RemoveAt(i);
                    }
                    continue;
                }

                //阶段一 (0~0.5): 强烈故障闪烁，NPC保持原大小
                //阶段二 (0.5~0.85): 开始缩小 + 更激烈故障
                //阶段三 (0.85~1.0): 急速缩小 → 高光闪白 → 消失
                if (progress > 0.5f) {
                    float shrinkPhase = (progress - 0.5f) / 0.5f;
                    float shrink = 1f - MathF.Pow(shrinkPhase, 2.2f);
                    npc.scale = entry.OriginalScale * Math.Max(shrink, 0.02f);
                }

                //每帧生成故障粒子（仅客户端）
                if (!Main.dedServ) {
                    CyberBanishParticles.SpawnBanishParticles(npc, progress, entry.Seed);
                }

                //动画完毕 → 抹除（仅由"放逐发起者"或服务端真正去抹除，避免多端各自把 NPC 写死）
                if (entry.Timer >= BanishDuration) {
                    bool authoritative = Main.netMode == NetmodeID.SinglePlayer
                        || VaultUtils.isServer
                        || entry.OwnerWho == Main.myPlayer;
                    if (authoritative) {
                        npc.active = false;
                        npc.life = 0;
                        if (Main.netMode == NetmodeID.Server) {
                            //在服务端抹除时同步 NPC.active=false 给所有客户端
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        }
                    }
                    //最终爆发粒子（仅客户端）
                    if (!Main.dedServ) {
                        CyberBanishParticles.SpawnFinalBurst(npc.Center, entry.OriginalScale);
                    }
                    ActiveBanishments.RemoveAt(i);
                }
            }
        }

        public static void Reset() {
            ActiveBanishments.Clear();
        }
    }

    /// <summary>
    /// 单个NPC的放逐数据
    /// </summary>
    internal class BanishEntry
    {
        public int NpcIndex;
        public int Timer;
        internal float TimerCarry;
        public float OriginalScale;
        public Vector2 FreezePosition;
        public float Seed;
        /// <summary>
        /// 是否为Boss级演出：不缩小不抹除，末段触发<see cref="CyberBossExecution"/>
        /// </summary>
        public bool IsBoss;
        /// <summary>
        /// 发起者whoAmI，Boss版雷击伤害会从该玩家读取SHPC面板
        /// </summary>
        public int OwnerWho;
        /// <summary>
        /// Boss版是否已触发雷击，避免重复调用StartExecution
        /// </summary>
        public bool ExecutionTriggered;

        /// <summary>
        /// 放逐进度 0→1
        /// </summary>
        public float Progress => (float)Timer / CyberBanish.BanishDuration;
    }
}
