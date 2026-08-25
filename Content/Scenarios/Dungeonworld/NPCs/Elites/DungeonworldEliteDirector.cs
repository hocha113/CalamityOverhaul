using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>刷怪静默区（与 IMPL-E 的氛围静默 AmbientQuiet 异域异名，裁决 §1-5）：矩形内精英零投放</summary>
    internal sealed class SpawnQuietZone
    {
        /// <summary>tile 坐标矩形</summary>
        internal Rectangle Area;
        internal string Name;
    }

    /// <summary>
    /// 精英投放公共查询台（WAVE2-ENEMIES §3.0/§4）：静默区注册表、存活上限、层带三分位、
    /// 提灯巡守警报浓度、拾骨缝匠尸骨记录。全部静态表只在服务器被决策消费
    /// （SpawnChance/EditSpawnRate/AI 裁决都是服务端钩子），联机客户端表为空属预期。
    /// ShouldSave=false 回放制：ClearWorld 清空，生成期/运行时重登记。
    /// </summary>
    internal class DungeonworldEliteDirector : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => DungeonworldEliteGate.Enabled;

        //==================== 静默区 ====================

        /// <summary>出生点静默半径（tile）</summary>
        internal const int SpawnPointQuietRadius = 60;
        /// <summary>深牢禁室静默半径（tile，围绕 GaolBossRoomSiting.LastOrigin）</summary>
        internal const int BossRoomQuietRadius = 100;
        private const int MaxQuietZones = 64;

        private static readonly List<SpawnQuietZone> quietZones = [];

        /// <summary>
        /// 登记一片刷怪静默区（tile 矩形，可外扩）。生成期（L5 集市）与日后 C 路 Boss 房共用此口。
        /// 门禁关闭时不记（镜像 GaolBossRoomWatcher.RegisterRoom：防未加载态静态表跨次生成累积）
        /// </summary>
        internal static void RegisterQuietZone(Rectangle tileArea, int expandTiles = 0, string name = null) {
            if (!DungeonworldEliteGate.Enabled) {
                return;
            }
            if (expandTiles != 0) {
                tileArea.Inflate(expandTiles, expandTiles);
            }
            foreach (SpawnQuietZone zone in quietZones) {
                if (zone.Area == tileArea) {
                    return;
                }
            }
            if (quietZones.Count >= MaxQuietZones) {
                CWRMod.Instance.Logger.Warn($"[EliteDirector] 静默区超过{MaxQuietZones}上限,忽略登记 {name}");
                return;
            }
            quietZones.Add(new SpawnQuietZone { Area = tileArea, Name = name ?? "unnamed" });
        }

        /// <summary>刷怪点是否落在任一静默区：出生点半径 + 禁室半径 + 登记矩形（§4 公共前置 3）</summary>
        internal static bool InSpawnQuietZone(int tileX, int tileY) {
            int dx = tileX - Main.spawnTileX;
            int dy = tileY - Main.spawnTileY;
            if (dx * dx + dy * dy < SpawnPointQuietRadius * SpawnPointQuietRadius) {
                return true;
            }
            if (GaolBossRoomSiting.LastOrigin is Point origin) {
                dx = tileX - origin.X;
                dy = tileY - origin.Y;
                if (dx * dx + dy * dy < BossRoomQuietRadius * BossRoomQuietRadius) {
                    return true;
                }
            }
            foreach (SpawnQuietZone zone in quietZones) {
                if (zone.Area.Contains(tileX, tileY)) {
                    return true;
                }
            }
            return false;
        }

        //==================== 存活上限与公共前置 ====================

        /// <summary>单型 ≤2、全精英合计 ≤3（§4 公共前置 4）</summary>
        internal static bool EliteBudgetOpen(int type) {
            if (NPC.CountNPCS(type) >= 2) {
                return false;
            }
            int total = NPC.CountNPCS(ModContent.NPCType<LanternWarden>())
                + NPC.CountNPCS(ModContent.NPCType<LampeaterWisp>())
                + NPC.CountNPCS(ModContent.NPCType<DrownedTurnkey>())
                + NPC.CountNPCS(ModContent.NPCType<BoneStitcher>());
            return total < 3;
        }

        /// <summary>四怪共同前置：子世界内 + 静默区外 + 存活上限内。各怪 SpawnChance 首行调用</summary>
        internal static bool CommonSpawnGate(NPCSpawnInfo spawnInfo, int type) {
            if (!Dungeonworld.Active) {
                return false;
            }
            if (InSpawnQuietZone(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY)) {
                return false;
            }
            return EliteBudgetOpen(type);
        }

        //==================== 层带工具（纯算术零扫描，§4）====================

        /// <summary>行→层带序号（0=L1..6=L7），带外返回 -1</summary>
        internal static int BandIndexForRow(int row) {
            for (int i = 0; i < DungeonworldMetrics.Bands.Length; i++) {
                if (row >= DungeonworldMetrics.Bands[i].Top && row < DungeonworldMetrics.Bands[i].Bottom) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>带内深度分位 [0,1)，带外返回 -1（下三分带 = 值 ≥ 2/3）</summary>
        internal static float BandDepth01(int row) {
            int idx = BandIndexForRow(row);
            if (idx < 0) {
                return -1f;
            }
            LayerBand band = DungeonworldMetrics.Bands[idx];
            return (row - band.Top) / (float)(band.Bottom - band.Top);
        }

        //==================== L4 舱段查询（只读 L4WaterWorks 现成数据）====================

        /// <summary>包含该 tile 的舱段（不论水位），无则 null</summary>
        internal static L4WaterWorks.Compartment CompartmentContaining(int tileX, int tileY) {
            foreach (L4WaterWorks.Compartment c in L4WaterWorks.Compartments) {
                if (c.Area.Contains(tileX, tileY)) {
                    return c;
                }
            }
            return null;
        }

        /// <summary>舱段当前态水面行；排空态返回 Area.Bottom（无水）</summary>
        internal static int CompartmentSurfaceRow(L4WaterWorks.Compartment c)
            => L4WaterWorks.HighState ? c.HighSurfaceRow : c.LowSurfaceRow;

        /// <summary>该 tile 当前是否在某舱段的水面之下（湿舱段判定，§4 前置 5）</summary>
        internal static bool InWetCompartment(int tileX, int tileY, out L4WaterWorks.Compartment compartment) {
            compartment = CompartmentContaining(tileX, tileY);
            if (compartment == null) {
                return false;
            }
            int surface = CompartmentSurfaceRow(compartment);
            return surface < compartment.Area.Bottom && tileY >= surface;
        }

        //==================== 警报浓度（提灯巡守 §3.1，服务器专用）====================

        private sealed class AlarmSurge
        {
            internal int Source;
            internal Vector2 Pos;
            internal uint ExpireAt;
        }

        private static readonly List<AlarmSurge> surges = [];
        /// <summary>追缉结束后浓度残留 8s</summary>
        private const int SurgeLingerTicks = 480;
        /// <summary>浓度作用半径（px）</summary>
        internal const float SurgeRadius = 1500f;

        /// <summary>追缉态巡守每帧通报（服务器）：刷新位置与过期时限</summary>
        internal static void ReportAlarmChase(int sourceWho, Vector2 pos) {
            foreach (AlarmSurge surge in surges) {
                if (surge.Source == sourceWho) {
                    surge.Pos = pos;
                    surge.ExpireAt = Main.GameUpdateCount + SurgeLingerTicks;
                    return;
                }
            }
            surges.Add(new AlarmSurge { Source = sourceWho, Pos = pos, ExpireAt = Main.GameUpdateCount + SurgeLingerTicks });
        }

        /// <summary>该玩家是否处于警报浓度中（DungeonworldNPC.EditSpawnRate 服务器消费）</summary>
        internal static bool AlarmSurging(Player player) {
            for (int i = surges.Count - 1; i >= 0; i--) {
                if (Main.GameUpdateCount > surges[i].ExpireAt) {
                    surges.RemoveAt(i);
                    continue;
                }
                if (Vector2.Distance(surges[i].Pos, player.Center) < SurgeRadius) {
                    return true;
                }
            }
            return false;
        }

        //==================== 尸骨记录（拾骨缝匠 §3.4，服务器专用）====================

        private sealed class BoneRecord
        {
            internal Vector2 Pos;
            internal uint ExpireAt;
        }

        private static readonly List<BoneRecord> boneRecords = [];
        private const int BoneRecordCap = 32;
        /// <summary>记录寿命 60s</summary>
        private const int BoneRecordLifeTicks = 3600;

        /// <summary>骨系死者入表（BoneHarvestGlobal.OnKill 服务器调用；容量 32 先进先出）</summary>
        internal static void RecordBoneCorpse(Vector2 pos) {
            PruneBoneRecords();
            if (boneRecords.Count >= BoneRecordCap) {
                boneRecords.RemoveAt(0);
            }
            boneRecords.Add(new BoneRecord { Pos = pos, ExpireAt = Main.GameUpdateCount + BoneRecordLifeTicks });
        }

        /// <summary>查询最近的未认领记录（不出表）</summary>
        internal static bool TryPeekNearestBone(Vector2 from, out Vector2 pos) {
            PruneBoneRecords();
            pos = default;
            float best = float.MaxValue;
            foreach (BoneRecord record in boneRecords) {
                float dist = Vector2.DistanceSquared(from, record.Pos);
                if (dist < best) {
                    best = dist;
                    pos = record.Pos;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>认领半径内最近记录并立即出表（消费原子性：两只缝匠不重复吃）</summary>
        internal static bool TryClaimBone(Vector2 from, float within, out Vector2 pos) {
            PruneBoneRecords();
            pos = default;
            int bestIdx = -1;
            float best = within * within;
            for (int i = 0; i < boneRecords.Count; i++) {
                float dist = Vector2.DistanceSquared(from, boneRecords[i].Pos);
                if (dist < best) {
                    best = dist;
                    bestIdx = i;
                }
            }
            if (bestIdx < 0) {
                return false;
            }
            pos = boneRecords[bestIdx].Pos;
            boneRecords.RemoveAt(bestIdx);
            return true;
        }

        private static void PruneBoneRecords() {
            for (int i = boneRecords.Count - 1; i >= 0; i--) {
                if (Main.GameUpdateCount > boneRecords[i].ExpireAt) {
                    boneRecords.RemoveAt(i);
                }
            }
        }

        //==================== 回放制复位 ====================

        public override void ClearWorld() {
            quietZones.Clear();
            surges.Clear();
            boneRecords.Clear();
        }
    }
}
