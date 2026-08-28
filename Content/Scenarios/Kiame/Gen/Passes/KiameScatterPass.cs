using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen.Passes
{
    //P55 撒布：枯树、碎砖堆、断篱笆
    //只往地面上放，不动体块；洼地与结构禁区一律让路
    internal class KiameScatterPass : GenPass
    {
        //灰烬木：泡烂发灰的枯木质感（幽灵木在暗调下读作黑曜石，2026-08-28 反馈换掉）
        private const ushort TrunkTile = TileID.AshWood;

        private static int trees;
        private static int rubble;
        private static int fences;

        public KiameScatterPass() : base("Kiame Scatter", 0.6f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiamePlans.Report(progress, "枯枝在雨里泡烂...");
            CWRMod.Instance.Logger.Info("[Kiame] Scatter start");
            trees = rubble = fences = 0;

            //洼原与泽地：枯树主产区；村带零星几棵；台地孤树一两株
            ScatterTrees(KiameMetrics.FlatsLeft, KiameMetrics.VillageEastLeft, 18, 34);
            ScatterTrees(KiameMetrics.MarshLeft, KiameMetrics.ReserveLeft, 14, 26);
            progress.Set(0.4);
            ScatterTrees(KiameMetrics.VillageWestLeft, KiameMetrics.FlatsLeft, 44, 80);
            ScatterTrees(KiameMetrics.VillageEastLeft, KiameMetrics.MarshLeft, 44, 80);
            ScatterTrees(KiameMetrics.BorderThick + 8, KiameMetrics.VillageWestLeft, 30, 56);
            progress.Set(0.7);
            ScatterRubble();
            progress.Set(0.85);
            ScatterFences();
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info($"[Kiame] Scatter 枯树={trees} 碎砖={rubble} 断篱={fences}");
        }

        private static void ScatterTrees(int left, int right, int gapMin, int gapMax) {
            for (int x = left + 6; x < right - 6; x += WorldGen.genRand.Next(gapMin, gapMax)) {
                int h = WorldGen.genRand.Next(7, 17);
                if (KiamePlans.InExclusion(x) || KiamePlans.OverlapsPool(x - 1, x + 1, margin: 1)
                    || !ColumnClear(x, h + 3)) {
                    continue;
                }
                BuildDeadTree(x, KiamePlans.FloorTopAt(x), h);
                trees++;
            }
        }

        //枯树：一根细干 + 两三条秃枝，枝尖向上翘一格；这地方不长叶子
        private static void BuildDeadTree(int x, int groundRow, int height) {
            int top = groundRow - height;
            KiameTileBrush.FillRect(x, top, x + 1, groundRow, TrunkTile);

            int branches = WorldGen.genRand.Next(2, 5);
            for (int i = 0; i < branches; i++) {
                int by = top + 1 + WorldGen.genRand.Next(Math.Max(height - 3, 1));
                int dir = WorldGen.genRand.NextBool() ? 1 : -1;
                int len = WorldGen.genRand.Next(2, 6);
                for (int k = 1; k <= len; k++) {
                    KiameTileBrush.SetSolid(x + dir * k, by, TrunkTile);
                }
                //枝尖上翘
                KiameTileBrush.SetSolid(x + dir * len, by - 1, TrunkTile);

                //枝下挂网：泡了水的旧网，不是每根枝都有
                if (WorldGen.genRand.NextFloat() < 0.24f) {
                    int wx = x + dir * WorldGen.genRand.Next(1, len + 1);
                    int drop = WorldGen.genRand.Next(2, 5);
                    for (int k = 1; k <= drop; k++) {
                        KiameTileBrush.SetSolid(wx, by + k, TileID.Cobweb);
                    }
                }
            }
        }

        //碎砖堆：塌屋滚出来的灰砖与石块，半埋在村带泥里
        private static void ScatterRubble() {
            ScatterRubbleBand(KiameMetrics.VillageWestLeft, KiameMetrics.FlatsLeft);
            ScatterRubbleBand(KiameMetrics.VillageEastLeft, KiameMetrics.MarshLeft);
        }

        private static void ScatterRubbleBand(int left, int right) {
            for (int x = left; x < right; x += WorldGen.genRand.Next(14, 34)) {
                int ground = KiamePlans.FloorTopAt(x);
                if (KiamePlans.InExclusion(x) || KiamePlans.OverlapsPool(x, x + 5, margin: 1)) {
                    continue;
                }
                int w = WorldGen.genRand.Next(2, 5);
                int h = WorldGen.genRand.Next(1, 3);
                for (int dx = 0; dx < w; dx++) {
                    //边缘啃掉一角，别是个规整方块
                    int col = h - (WorldGen.genRand.NextBool() ? 1 : 0);
                    for (int dy = 0; dy < col; dy++) {
                        ushort type = WorldGen.genRand.NextBool(3) ? TileID.Stone : TileID.GrayBrick;
                        KiameTileBrush.SetSolid(x + dx, ground - 1 - dy, type);
                    }
                }
                rubble++;
            }
        }

        //断篱笆：纯背景墙的旧篱残段，随机豁口，零碰撞
        private static void ScatterFences() {
            ScatterFenceBand(KiameMetrics.VillageWestLeft, KiameMetrics.FlatsLeft);
            ScatterFenceBand(KiameMetrics.VillageEastLeft, KiameMetrics.MarshLeft);
        }

        private static void ScatterFenceBand(int left, int right) {
            for (int x = left; x < right - 12; x += WorldGen.genRand.Next(30, 70)) {
                if (KiamePlans.InExclusion(x)) {
                    continue;
                }
                int run = WorldGen.genRand.Next(4, 10);
                for (int dx = 0; dx < run; dx++) {
                    int col = x + dx;
                    if (KiamePlans.InExclusion(col) || KiamePlans.OverlapsPool(col, col)) {
                        continue;
                    }
                    int ground = KiamePlans.FloorTopAt(col);
                    if (WorldGen.genRand.NextFloat() < 0.85f) {
                        KiameTileBrush.SetWall(col, ground - 1, WallID.WoodenFence);
                    }
                    if (WorldGen.genRand.NextFloat() < 0.55f) {
                        KiameTileBrush.SetWall(col, ground - 2, WallID.WoodenFence);
                    }
                }
                fences++;
            }
        }

        //地面往上这么多行必须是空的：别把树种在屋顶或砖堆上
        private static bool ColumnClear(int x, int rows) {
            int ground = KiamePlans.FloorTopAt(x);
            for (int y = ground - rows; y < ground; y++) {
                if (y < 0) {
                    return false;
                }
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile) {
                    return false;
                }
            }
            return true;
        }
    }
}
