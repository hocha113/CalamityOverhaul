using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Passes
{
    //P10 骨架：规划态复位 + 边界 + 逐列地板线 + 地体浇筑 + 出生锚点
    //只管地形体块；材质细化/村落轮廓/撒布归后续 pass
    internal class KiyumeSkeletonPass : GenPass
    {
        public KiyumeSkeletonPass() : base("Kiyume Skeleton", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiyumePlans.Report(progress, "湖水退去，岸上有灯...");
            CWRMod.Instance.Logger.Info("[Kiyume] Skeleton start");

            //每次进梦重生成：规划态与计数器全部重置
            //（不能在 OnWorldLoad 清，生成 pass 先于它运行）
            KiyumePlans.Reset();
            KiyumeStructures.Reset();
            KiyumeTileBrush.ResetForNewGen();
            //SubLib 进 gen 时把 worldSurface 设成 maxY*0.3；必须在第一 pass 就改成玩法层的值
            Main.worldSurface = KiyumeMetrics.WorldSurfaceRow;
            Main.rockLayer = KiyumeMetrics.RockLayerRow;

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            int[] floorTop = BuildFloorLine(width);
            KiyumePlans.FloorTop = floorTop;

            for (int x = 0; x < width; x++) {
                progress.Set(x / (double)(width - 1));
                bool sideBorder = x < KiyumeMetrics.BorderThick || x >= width - KiyumeMetrics.BorderThick;
                ushort ground = KiyumeMetrics.BandForColumn(x)?.GroundTile ?? TileID.Stone;

                for (int y = 0; y < height; y++) {
                    bool capBorder = y < KiyumeMetrics.BorderThick || y >= height - KiyumeMetrics.BorderThick;
                    if (sideBorder || capBorder) {
                        //世界壁统一黑曜石砖：梦的边界不是地形，是走不过去
                        KiyumeTileBrush.SetSolid(x, y, TileID.ObsidianBrick);
                        continue;
                    }
                    if (y >= floorTop[x]) {
                        KiyumeTileBrush.SetSolid(x, y, ground);
                    }
                    else {
                        KiyumeTileBrush.ClearCell(x, y);
                    }
                }
            }

            //出生在村口地板上，脚下那格实心
            Main.spawnTileX = KiyumeMetrics.SpawnX;
            Main.spawnTileY = floorTop[KiyumeMetrics.SpawnX];

            CWRMod.Instance.Logger.Info(
                $"[Kiyume] Skeleton solid={KiyumeTileBrush.SolidWrites} air={KiyumeTileBrush.ClearWrites}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY}) macroSeed={KiyumeMetrics.MacroSeed}");
        }

        //逐列地板顶行：带表基准曲线 + 随机游走起伏；出生区钉平，房子要放得下
        private static int[] BuildFloorLine(int width) {
            int[] floorTop = new int[width];
            int spawnLeft = KiyumeMetrics.SpawnX - KiyumeMetrics.SpawnFlatCols / 2;
            int spawnRight = spawnLeft + KiyumeMetrics.SpawnFlatCols;
            int wobble = 0;

            for (int x = 0; x < width; x++) {
                int amp = KiyumeMetrics.WobbleAmpAt(x);
                wobble = Math.Clamp(wobble + WorldGen.genRand.Next(-1, 2), -amp, amp);
                floorTop[x] = (int)MathF.Round(KiyumeMetrics.BaseFloorAt(x)) + wobble;
            }

            //出生平台：整段钉在同一行，两侧各 8 列做线性过渡免出台阶
            int flatRow = floorTop[Math.Clamp(KiyumeMetrics.SpawnX, 0, width - 1)];
            for (int x = Math.Max(spawnLeft, 0); x < Math.Min(spawnRight, width); x++) {
                floorTop[x] = flatRow;
            }
            BlendEdge(floorTop, width, spawnLeft - 8, spawnLeft, flatRow, toFlat: true);
            BlendEdge(floorTop, width, spawnRight, spawnRight + 8, flatRow, toFlat: false);
            return floorTop;
        }

        //把平台端点与自然地形之间的落差摊到 8 列里
        private static void BlendEdge(int[] floorTop, int width, int from, int to, int flatRow, bool toFlat) {
            int span = to - from;
            if (span <= 0) {
                return;
            }
            for (int x = from; x < to; x++) {
                if (x < 0 || x >= width) {
                    continue;
                }
                float t = (x - from) / (float)span;
                float k = toFlat ? t : 1f - t;
                floorTop[x] = (int)MathF.Round(MathHelper.Lerp(floorTop[x], flatRow, k));
            }
        }
    }
}
