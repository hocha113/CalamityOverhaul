using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Passes
{
    //P55 撒布：枯树、滩涂礁石、枝下蛛网
    //三种形与天幕 villageRow 的抽签对齐；只往地面上放，不动体块
    internal class KiyumeScatterPass : GenPass
    {
        private const ushort TrunkTile = TileID.LivingWood;

        private static int trees;
        private static int rocks;
        private static int webs;

        public KiyumeScatterPass() : base("Kiyume Scatter", 0.6f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "枯枝与搁浅物...";
            trees = rocks = webs = 0;

            //枯林带：主产区，密；村落带：巷子间零星几棵，让剪影别是一排纯房子
            ScatterTrees(KiyumeMetrics.GroveLeft, KiyumeMetrics.RidgeLeft, 9, 17);
            progress.Set(0.45);
            ScatterTrees(KiyumeMetrics.VillageLeft, KiyumeMetrics.GroveLeft, 52, 96);
            progress.Set(0.7);
            ScatterShoalRocks();
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info($"[Kiyume] Scatter 枯树={trees} 礁石={rocks} 蛛网={webs}");
        }

        private static void ScatterTrees(int left, int right, int gapMin, int gapMax) {
            for (int x = left + 6; x < right - 6; x += WorldGen.genRand.Next(gapMin, gapMax)) {
                int h = WorldGen.genRand.Next(8, 19);
                if (!ColumnClear(x, h + 3)) {
                    continue;
                }
                BuildDeadTree(x, KiyumePlans.FloorTopAt(x), h);
                trees++;
            }
        }

        //枯树：一根细干 + 两三条秃枝，枝尖向上翘一格；不长叶子，这地方不长叶子
        private static void BuildDeadTree(int x, int groundRow, int height) {
            int top = groundRow - height;
            KiyumeTileBrush.FillRect(x, top, x + 1, groundRow, TrunkTile);

            int branches = WorldGen.genRand.Next(2, 5);
            for (int i = 0; i < branches; i++) {
                int by = top + 1 + WorldGen.genRand.Next(Math.Max(height - 3, 1));
                int dir = WorldGen.genRand.NextBool() ? 1 : -1;
                int len = WorldGen.genRand.Next(2, 6);
                for (int k = 1; k <= len; k++) {
                    KiyumeTileBrush.SetSolid(x + dir * k, by, TrunkTile);
                }
                //枝尖上翘
                KiyumeTileBrush.SetSolid(x + dir * len, by - 1, TrunkTile);

                //枝下挂网：不是每根枝都有，有的那根往下吊两三格
                if (WorldGen.genRand.NextFloat() < 0.28f) {
                    int wx = x + dir * WorldGen.genRand.Next(1, len + 1);
                    int drop = WorldGen.genRand.Next(2, 5);
                    for (int k = 1; k <= drop; k++) {
                        KiyumeTileBrush.SetSolid(wx, by + k, TileID.Cobweb);
                        webs++;
                    }
                }
            }
        }

        //滩涂礁石：半埋在淤泥里的血石堆，越靠水越多
        private static void ScatterShoalRocks() {
            int left = KiyumeMetrics.ShoalLeft - 30;
            int right = KiyumeMetrics.VillageLeft;
            for (int x = left; x < right; x += WorldGen.genRand.Next(7, 22)) {
                if (x < KiyumeMetrics.BorderThick) {
                    continue;
                }
                int ground = KiyumePlans.FloorTopAt(x);
                int w = WorldGen.genRand.Next(2, 6);
                int h = WorldGen.genRand.Next(1, 4);
                for (int dx = 0; dx < w; dx++) {
                    //边缘啃掉一角，别是个规整方块
                    int col = h - (WorldGen.genRand.NextBool() ? 1 : 0);
                    for (int dy = 0; dy < col; dy++) {
                        KiyumeTileBrush.SetSolid(x + dx, ground - 1 - dy, TileID.Crimstone);
                    }
                }
                rocks++;
            }
        }

        //地面往上这么多行必须是空的：别把树种在屋顶或礁石上
        private static bool ColumnClear(int x, int rows) {
            int ground = KiyumePlans.FloorTopAt(x);
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
