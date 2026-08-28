using System;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen.Passes
{
    //P10 骨架：规划态复位 + 边界 + 逐列地板线 + 洼地挖坑 + 地体浇筑 + 出生锚点
    //只管地形体块与洼地登记；材质细化/灌水/村落归后续 pass
    internal class KiameSkeletonPass : GenPass
    {
        public KiameSkeletonPass() : base("Kiame Skeleton", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiamePlans.Report(progress, "雨里浮出一片洼地...");
            CWRMod.Instance.Logger.Info("[Kiame] Skeleton start");

            //每次进雨重生成：规划态与计数器全部重置
            //（不能在 OnWorldLoad 清，生成 pass 先于它运行）
            KiamePlans.Reset();
            KiameTileBrush.ResetForNewGen();
            //SubLib 进 gen 时把 worldSurface 设成 maxY*0.3；必须在第一 pass 就改成玩法层的值
            Main.worldSurface = KiameMetrics.WorldSurfaceRow;
            Main.rockLayer = KiameMetrics.RockLayerRow;

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            int[] floorTop = BuildFloorLine(width);
            //先挖两片工造大水面，再撒散洼（散洼避让已登记的水面）
            CarveLakeBasin(floorTop, width, KiameMetrics.FlatsPondCenter,
                KiameMetrics.FlatsPondHalfW, KiameMetrics.FlatsPondDepth);
            CarveLakeBasin(floorTop, width, KiameMetrics.MarshLakeCenter,
                KiameMetrics.MarshLakeHalfW, KiameMetrics.MarshLakeDepth);
            CarvePools(floorTop, width);
            KiamePlans.FloorTop = floorTop;

            for (int x = 0; x < width; x++) {
                progress.Set(x / (double)(width - 1));
                bool sideBorder = x < KiameMetrics.BorderThick || x >= width - KiameMetrics.BorderThick;
                ushort ground = KiameMetrics.BandForColumn(x)?.GroundTile ?? TileID.Stone;

                for (int y = 0; y < height; y++) {
                    bool capBorder = y < KiameMetrics.BorderThick || y >= height - KiameMetrics.BorderThick;
                    if (sideBorder || capBorder) {
                        //世界壁统一黑曜石砖：雨的边界不是地形，是走不过去
                        KiameTileBrush.SetSolid(x, y, TileID.ObsidianBrick);
                        continue;
                    }
                    if (y >= floorTop[x]) {
                        KiameTileBrush.SetSolid(x, y, ground);
                    }
                    else {
                        KiameTileBrush.ClearCell(x, y);
                    }
                }
            }

            //出生在台地上，脚下那格实心
            Main.spawnTileX = KiameMetrics.SpawnX;
            Main.spawnTileY = floorTop[KiameMetrics.SpawnX];

            CWRMod.Instance.Logger.Info(
                $"[Kiame] Skeleton solid={KiameTileBrush.SolidWrites} air={KiameTileBrush.ClearWrites}"
                + $" pools={KiamePlans.Pools.Count}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY}) macroSeed={KiameMetrics.MacroSeed}");
        }

        //逐列地板顶行：带表基准曲线 + 随机游走起伏；出生区钉平
        private static int[] BuildFloorLine(int width) {
            int[] floorTop = new int[width];
            int spawnLeft = KiameMetrics.SpawnX - KiameMetrics.SpawnFlatCols / 2;
            int spawnRight = spawnLeft + KiameMetrics.SpawnFlatCols;
            int wobble = 0;

            for (int x = 0; x < width; x++) {
                int amp = KiameMetrics.WobbleAmpAt(x);
                wobble = Math.Clamp(wobble + WorldGen.genRand.Next(-1, 2), -amp, amp);
                floorTop[x] = (int)MathF.Round(KiameMetrics.BaseFloorAt(x)) + wobble;
            }

            //出生平台：整段钉在同一行，两侧各 8 列做线性过渡免出台阶
            int flatRow = floorTop[Math.Clamp(KiameMetrics.SpawnX, 0, width - 1)];
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

        /// <summary>
        /// 工造湖盆：椭圆剖面的大水面，水面钉在两缘中较低那侧的岸沿。
        /// 在散洼之前挖，散洼按登记表避让
        /// </summary>
        private static void CarveLakeBasin(int[] floorTop, int width, int centerCol, int halfW, int depth) {
            int left = centerCol - halfW;
            int right = centerCol + halfW;
            if (left <= KiameMetrics.BorderThick + 2 || right >= width - KiameMetrics.BorderThick - 2) {
                return;
            }
            int surfaceRow = Math.Max(floorTop[left - 1], floorTop[right + 1]);
            for (int x = left; x <= right; x++) {
                float t = (x - centerCol) / (float)halfW;
                //椭圆剖面：湖心最深，岸线自然爬升
                int carve = (int)MathF.Round(depth * MathF.Sqrt(MathF.Max(1f - t * t, 0f)));
                int baseRow = Math.Max(floorTop[x], surfaceRow);
                floorTop[x] = baseRow + carve;
            }
            KiamePlans.Pools.Add(new KiamePoolSpan(left, right, surfaceRow));
        }

        /// <summary>
        /// 洼地挖坑：逐带按配置抽签落盆，余弦剖面平滑下凹，登记水面行。
        /// 水面 = 两缘中较低那侧的地板行（行号大者），灌到这里绝不外溢；
        /// 两缘高差超过 3 行的陡坡位不落盆，免得水挂在坡上
        /// </summary>
        private static void CarvePools(int[] floorTop, int width) {
            for (int bandIdx = 0; bandIdx < KiameMetrics.Bands.Length; bandIdx++) {
                KiamePoolProfile profile = KiameMetrics.PoolProfiles[bandIdx];
                if (!profile.Any) {
                    continue;
                }
                KiameBand band = KiameMetrics.Bands[bandIdx];
                int want = WorldGen.genRand.Next(profile.CountMin, profile.CountMax + 1);
                int placed = 0;
                //抽签上限给足：斜坡/重叠会吃掉不少位
                for (int attempt = 0; attempt < want * 10 && placed < want; attempt++) {
                    int halfW = WorldGen.genRand.Next(profile.HalfWidthMin, profile.HalfWidthMax + 1);
                    int depth = WorldGen.genRand.Next(profile.DepthMin, profile.DepthMax + 1);
                    int cx = WorldGen.genRand.Next(band.Left + halfW + 4, band.Right - halfW - 4);
                    int left = cx - halfW;
                    int right = cx + halfW;

                    if (left <= KiameMetrics.BorderThick + 2 || right >= width - KiameMetrics.BorderThick - 2) {
                        continue;
                    }
                    //避开出生平台与既有水面（3 列缓冲）
                    if (right >= KiameMetrics.SpawnReserveLeft && left < KiameMetrics.SpawnReserveRight) {
                        continue;
                    }
                    if (KiamePlans.OverlapsPool(left, right, margin: 3)) {
                        continue;
                    }

                    int edgeLeft = floorTop[left - 1];
                    int edgeRight = floorTop[right + 1];
                    if (Math.Abs(edgeLeft - edgeRight) > 3) {
                        continue;
                    }
                    //水面钉在较低的那侧岸沿（行号大者），水永远兜得住
                    int surfaceRow = Math.Max(edgeLeft, edgeRight);

                    for (int x = left; x <= right; x++) {
                        float t = (x - cx) / (float)halfW;
                        //余弦剖面：盆心最深，两缘归零
                        int carve = (int)MathF.Round(depth * (0.5f + 0.5f * MathF.Cos(t * MathHelper.Pi)));
                        int baseRow = Math.Max(floorTop[x], surfaceRow);
                        floorTop[x] = baseRow + carve;
                    }
                    KiamePlans.Pools.Add(new KiamePoolSpan(left, right, surfaceRow));
                    placed++;
                }
            }
        }
    }
}
