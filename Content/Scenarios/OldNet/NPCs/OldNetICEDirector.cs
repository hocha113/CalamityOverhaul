using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// 旧网 ICE 生成权威（镜像 GaolBossRoomWatcher 的 watcher 形态）：
    /// 深潜开始一次性布巡逻，噪音档位跃迁触发猎杀响应，T4 清剿波周期补员。
    /// 会话状态随 OnWorldLoad 复位（ShouldSave=false 每次深潜全新，静态残留=幽灵威胁）。
    /// M1 单人：本机玩家即威胁源；MP 化时按 per-player 档位重排 TODO
    /// </summary>
    internal class OldNetICEDirector : ModSystem
    {
        /// <summary>巡检间隔（tick）</summary>
        private const int CheckInterval = 20;

        private int checkTimer;
        private bool patrolsSeeded;
        private bool turretsSeeded;
        private int lastTier;
        private bool cleanupWave;
        private int reinforceTimer;

        /// <summary>封锁区闸门坐标登记（生成期写入，事件节点拉闸时解封；M1b）</summary>
        internal static readonly List<Point> SealGates = [];

        public static OldNetICEDirector Instance => ModContent.GetInstance<OldNetICEDirector>();

        /// <summary>清剿波进行中：全 ICE 全图感知、潜行失效</summary>
        public static bool CleanupWaveActive => Instance?.cleanupWave ?? false;

        /// <summary>场上猎杀者数（不含正在离场的），HUD 被追指示用</summary>
        public static int ActiveHunterCount {
            get {
                int type = ModContent.NPCType<OldNetBlackICE>();
                int count = 0;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.type == type && (int)npc.ai[0] == 0) {
                        count++;
                    }
                }
                return count;
            }
        }

        private void ResetSession() {
            checkTimer = 0;
            patrolsSeeded = false;
            turretsSeeded = false;
            lastTier = 0;
            cleanupWave = false;
            reinforceTimer = 0;
        }

        //闸门表不能在这里清：生成 pass 先于 OnWorldLoad 运行，登记发生在生成期，
        //由 pass 开头自清（每次深潜重生成即重登记）
        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        public override void PostUpdateNPCs() {
            //生成权威：客户端不做任何裁决（实体乘 SyncNPC 过线）
            if (VaultUtils.isClient || !OldNetWorld.Active) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            if (!patrolsSeeded) {
                patrolsSeeded = true;
                SeedPatrols();
            }
            if (!turretsSeeded) {
                turretsSeeded = true;
                SeedTurrets();
            }

            Player player = ResolveThreatTarget();
            if (player == null) {
                return;
            }
            OldNetPlayer session = OldNetPlayer.Get(player);
            int tier = session.NoiseTier;

            //档位跃迁响应：T2 猎杀小队 / T3 精英 / T4 清剿波
            if (tier >= 2 && lastTier < 2) {
                SpawnHuntSquad(player, 2, elite: false);
            }
            if (tier >= 3 && lastTier < 3) {
                SpawnHuntSquad(player, 1, elite: true);
            }
            if (tier >= 4 && lastTier < 4) {
                cleanupWave = true;
                //入档立即补一次员，之后按周期
                reinforceTimer = OldNetMetrics.T4ReinforceTicks;
            }
            lastTier = tier;

            //清剿波：补员至场上 N 只，直至噪音冷却到释放线以下
            if (cleanupWave) {
                if (session.Noise < OldNetMetrics.T4ReleaseBelow) {
                    cleanupWave = false;
                }
                else {
                    reinforceTimer += CheckInterval;
                    if (reinforceTimer >= OldNetMetrics.T4ReinforceTicks) {
                        reinforceTimer = 0;
                        int lack = OldNetMetrics.T4SustainCount - ActiveHunterCount;
                        if (lack > 0) {
                            SpawnHuntSquad(player, lack, elite: false);
                        }
                    }
                }
            }
        }

        //M1 单人：本机玩家；服务器兜底取首个活人
        private static Player ResolveThreatTarget() {
            if (!Main.dedServ && Main.LocalPlayer?.active == true && !Main.LocalPlayer.dead) {
                return Main.LocalPlayer;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead) {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// 拉闸解封：移除全图登记的封锁闸门（事件节点右键调用，本机删格）。
        /// MP 化时与节点右键一起过 SendTileSquare 账 TODO
        /// </summary>
        internal static void UnsealAll() {
            int gateType = ModContent.TileType<Tiles.OldNetSealGateTile>();
            foreach (Point gate in SealGates) {
                Tile tile = Framing.GetTileSafely(gate.X, gate.Y);
                if (!tile.HasTile || tile.TileType != gateType) {
                    continue;
                }
                WorldGen.KillTile(gate.X, gate.Y, noItem: true);
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendTileSquare(-1, gate.X, gate.Y, 1);
                }
            }
            SealGates.Clear();
        }

        /// <summary>
        /// 巡逻 ICE 目击玩家：立即触发一次 T2 响应（已在 T2+ 则补员 1 只）。
        /// 由 OldNetPatrolICE 在权威端调用
        /// </summary>
        internal static void NotifySpotted(Player player) {
            OldNetICEDirector inst = Instance;
            if (inst == null || VaultUtils.isClient || !OldNetWorld.Active || player == null) {
                return;
            }
            int count = inst.lastTier >= 2 || inst.cleanupWave ? 1 : 2;
            inst.SpawnHuntSquad(player, count, elite: false);
        }

        //──── 猎杀小队生成：从墙的方向来 ────

        private void SpawnHuntSquad(Player player, int count, bool elite) {
            //上限对齐清剿波补员目标，防目击连报堆一屏
            int room = OldNetMetrics.T4SustainCount - ActiveHunterCount;
            count = Math.Min(count, room);
            if (count <= 0) {
                return;
            }

            int type = ModContent.NPCType<OldNetBlackICE>();
            int spawnX = OldNetMetrics.HunterSpawnCol * 16;
            float minY = (OldNetMetrics.BorderThick + 5) * 16f;
            float maxY = (OldNetMetrics.FloorRow - 3) * 16f;

            for (int i = 0; i < count; i++) {
                int spawnY = (int)MathHelper.Clamp(
                    player.Center.Y + Main.rand.NextFloat(-90f, 90f), minY, maxY);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), spawnX, spawnY, type,
                    ai3: elite ? 1f : 0f);
                if (idx >= 0 && idx < Main.maxNPCs && VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }

            //被追次数：每次响应事件 +1（战报统计）
            OldNetPlayer.Get(player).HuntedCount++;

            //派遣提示音：玩家耳边低鸣，威胁上线的听觉阀
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultTransition with { Volume = 0.45f, Pitch = -0.4f },
                    player.Center);
            }
            CWRMod.Instance.Logger.Info(
                $"[OldNet] hunt squad spawned count={count} elite={elite} tier={lastTier} wave={cleanupWave}");
        }

        //──── 巡逻布防：深潜开始一次性铺设 ────

        private void SeedPatrols() {
            int type = ModContent.NPCType<OldNetPatrolICE>();
            int fadeLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols + OldNetMetrics.RuinCols;
            int minX = OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols + 150;
            int maxX = fadeLeft - 40;
            int placed = 0;

            for (int col = minX; col < maxX; col += OldNetMetrics.PatrolSpacingCols) {
                int x = col + Main.rand.Next(-25, 26);
                int surfaceRow = ProbeSurfaceRow(x);
                if (surfaceRow < 0) {
                    continue;
                }
                int spawnY = (int)(surfaceRow * 16f - OldNetMetrics.PatrolHoverHeight);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), x * 16 + 8, spawnY, type,
                    ai0: x * 16f + 8f, ai1: Main.rand.NextBool() ? 1f : -1f);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    placed++;
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                    }
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] patrol ICE seeded={placed}");
        }

        //──── 哨戒炮塔布防：地下机房吊装（M3 威胁扩容）────
        //消费 gen 期规划态：每次深潜重生成，Plans 与本会话同源（SP/服务器同端）

        private void SeedTurrets() {
            int type = ModContent.NPCType<OldNetTurretICE>();
            int placed = 0;
            foreach (Gen.OldNetBuildContext ctx in new[] { Gen.OldNetPlans.Z1, Gen.OldNetPlans.Z2, Gen.OldNetPlans.Z3 }) {
                if (ctx == null) {
                    continue;
                }
                foreach (Gen.Rooms.OldNetRoomNode room in ctx.Graph.Rooms) {
                    if (room.Role == Gen.Rooms.OldNetRoomRole.Landing) {
                        continue;
                    }
                    //深层机房必装；浅层按概率
                    bool deep = room.FloorTop >= OldNetMetrics.UnderShallowBottom;
                    if (!deep && Main.rand.NextFloat() >= OldNetMetrics.TurretRoomChance) {
                        continue;
                    }
                    int cx = (room.InteriorLeft + room.InteriorRight) / 2;
                    int cy = room.InteriorTop + 1;
                    int idx = NPC.NewNPC(new EntitySource_WorldEvent(),
                        cx * 16 + 8, cy * 16 + 8, type);
                    if (idx >= 0 && idx < Main.maxNPCs) {
                        placed++;
                        if (VaultUtils.isServer) {
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                        }
                    }
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] turret ICE seeded={placed}");
        }

        //从天空向下找该列首块实心，返回行号；找不到给 -1
        private static int ProbeSurfaceRow(int x) {
            for (int y = OldNetMetrics.BorderThick + 4; y < OldNetMetrics.FloorRow + 12; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }
            return -1;
        }
    }
}
