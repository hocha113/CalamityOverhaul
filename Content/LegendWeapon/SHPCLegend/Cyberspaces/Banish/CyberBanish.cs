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
    /// <summary>领域放逐，故障滤镜→抹除，net 同步</summary>
    internal class CyberBanish : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>放逐总帧，约1.8s</summary>
        public const int BanishDuration = 108;

        /// <summary>单次 RAM</summary>
        public const int RamCostPerCast = 5;

        /// <summary>放逐中 NPC</summary>
        public static readonly List<BanishEntry> ActiveBanishments = [];

        /// <summary>是否放逐中</summary>
        public static bool IsBanishing(int npcIndex) {
            for (int i = 0; i < ActiveBanishments.Count; i++) {
                if (ActiveBanishments[i].NpcIndex == npcIndex)
                    return true;
            }
            return false;
        }

        /// <summary>放逐进度 0..1，无则 -1</summary>
        public static float GetProgress(int npcIndex) {
            for (int i = 0; i < ActiveBanishments.Count; i++) {
                if (ActiveBanishments[i].NpcIndex == npcIndex)
                    return ActiveBanishments[i].Progress;
            }
            return -1f;
        }

        /// <summary>光标下放逐，myPlayer</summary>
        public static void BanishAtCursor() {
            CyberspacePlayer cp = Cyberspace.Local;
            if (cp == null) return;
            if (!cp.Active || cp.Intensity < 0.5f || cp.CurrentLayer < 2) return;

            //目标，Boss/普怪分 RAM
            int hitIndex = FindCursorTarget(cp);
            if (hitIndex < 0) return;

            NPC hitNpc = Main.npc[hitIndex];
            bool boss = CyberBossExecution.IsBossTier(hitNpc);
            int ramCost = boss ? CyberBossExecution.RamCostPerCast : RamCostPerCast;

            //RAM 不足则 HUD 闪
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

            //耗 RAM
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume(ramCost);
            }

            //目标+同组索引，共用种子
            //锚点随包广播
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

            //本机先应用
            ApplyBanishBatch(ownerWho, boss, entries);

            //广播
            if (Main.netMode != NetmodeID.SinglePlayer) {
                BroadcastStart(ownerWho, boss, entries, ignoreClient: -1);
            }
        }

        /// <summary>域内光标最近未放逐 NPC，无则 -1</summary>
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

        //群组放逐复用缓冲
        private static readonly List<int> banishGroupBuffer = [];

        /// <summary>入 ActiveBanishments，冻速+起手音，锚点随包统一</summary>
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

                //音效钉 NPC 中心
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

        /// <summary>远端广播入本机名单</summary>
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

            //服务端转发
            if (VaultUtils.isServer) {
                BroadcastStart(ownerWho, isBoss, entries, ignoreClient: whoAmI);
            }
        }

        /// <summary>每帧更新放逐</summary>
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

                //冻位
                npc.Center = entry.FreezePosition;
                npc.velocity = Vector2.Zero;

                if (entry.IsBoss) {
                    //Boss 不缩小不抹除，末段雷击后滤镜留到尾
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

                //阶段一 0~0.5 强闪，原大小
                //阶段二 0.5~0.85 缩小
                //阶段三 0.85~1 急缩闪白
                if (progress > 0.5f) {
                    float shrinkPhase = (progress - 0.5f) / 0.5f;
                    float shrink = 1f - MathF.Pow(shrinkPhase, 2.2f);
                    npc.scale = entry.OriginalScale * Math.Max(shrink, 0.02f);
                }

                //故障粒子，仅客户端
                if (!Main.dedServ) {
                    CyberBanishParticles.SpawnBanishParticles(npc, progress, entry.Seed);
                }

                //完毕抹除，仅发起者/服务端
                if (entry.Timer >= BanishDuration) {
                    bool authoritative = Main.netMode == NetmodeID.SinglePlayer
                        || VaultUtils.isServer
                        || entry.OwnerWho == Main.myPlayer;
                    if (authoritative) {
                        npc.active = false;
                        npc.life = 0;
                        if (Main.netMode == NetmodeID.Server) {
                            //服务端抹除同步 active
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        }
                    }
                    //最终爆发，仅客户端
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

    /// <summary>单 NPC 放逐条目</summary>
    internal class BanishEntry
    {
        public int NpcIndex;
        public int Timer;
        internal float TimerCarry;
        public float OriginalScale;
        public Vector2 FreezePosition;
        public float Seed;
        /// <summary>Boss 演出，不缩小不抹除，末段 <see cref="CyberBossExecution"/></summary>
        public bool IsBoss;
        /// <summary>发起者 whoAmI，Boss 雷伤读其 SHPC</summary>
        public int OwnerWho;
        /// <summary>Boss 雷击已触发</summary>
        public bool ExecutionTriggered;

        /// <summary>进度 0→1</summary>
        public float Progress => (float)Timer / CyberBanish.BanishDuration;
    }
}
