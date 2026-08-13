using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //====================================================================
    //书架迷宫区块(ROOMS-L3 §1-#2,本层主体,纯算法):
    //
    //2D落地语法(侧视图):格间行自下而上堆叠,行间2厚楼板,行内2厚隔墙切格间——
    //  [壳][格间|隔2|格间|隔2|格间][壳]
    //  [====楼板2(带3宽平台缺口)===]
    //  [壳][格间|隔2|格间][壳]
    //·横向:每道隔墙开3高地口(本层3高豁免,§2.4-③),行内全通;
    //·纵向:每对相邻行1~2个楼板缺口(3宽,盖平台防误落),缺口下方吊平台阶
    //  (竖距≤4,F2),缺口只落在中段格间——**行两端格间构造性成为死端**;
    //·死端格间必放奖励(防白走,§2.4-③);底行两端留给对外门口不计死端;
    //·水蜡烛风险房:个别格间预点水蜡烛(刷怪压力up)+最近死端奖励升档(零直伤,INDEX §3);
    //·夹层:高格间(≥10)平台书墙二层(书架锚SolidWithTop=平台合法,F7);
    //·泡皱变体(层底甲板,L3→L4预告):底行书架留空+地面散书+灰漆水渍。
    //几何一遍冻结;家具全走合法锚定,拒绝即计数;随机全走传入rand(gen期=genRand)
    //====================================================================
    internal static class L3MazeBlock
    {
        internal struct MazePlan
        {
            internal int RowCount;       //格间行数 2~4
            internal int[] RowHeights;   //每行净高 8~11
            internal int Width;          //内膛净宽
            internal bool Forbidden;     //禁书区带变体:Slab墙+密架+格间压下限
            internal bool RiskCell;      //水蜡烛风险房变体
            internal bool SoggyBottom;   //泡皱变体(仅层底甲板)
        }

        internal struct MazeReport
        {
            internal int Cells;
            internal int DeadEnds;
            internal int Rewards;
            internal int ShelvesPlaced;
            internal int ShelvesRejected;
            internal int WaterCandles;
        }

        /// <summary>掷区块计划;maxInteriorH=甲板条带允许的内膛净高上限</summary>
        internal static MazePlan Roll(UnifiedRandom rand, int maxInteriorH, bool forbidden, bool soggy) {
            var plan = new MazePlan {
                RowCount = forbidden ? rand.Next(2, 4) : rand.Next(3, 5),
                Width = forbidden ? rand.Next(48, 65) : rand.Next(56, 97),
                Forbidden = forbidden,
                //风险房概率:迷宫区约1/3,禁书区带更密(ROOMS-L3 §3)
                RiskCell = rand.NextBool(forbidden ? 2 : 3),
                SoggyBottom = soggy,
            };
            //行高掷点后收缩到条带上限(先砍行高,再砍行数)
            while (true) {
                plan.RowHeights = new int[plan.RowCount];
                int total = 2 * (plan.RowCount - 1);
                for (int r = 0; r < plan.RowCount; r++) {
                    plan.RowHeights[r] = rand.Next(8, 12);
                    total += plan.RowHeights[r];
                }
                if (total <= maxInteriorH) {
                    return plan;
                }
                bool shrunk = false;
                for (int r = 0; r < plan.RowCount && total > maxInteriorH; r++) {
                    if (plan.RowHeights[r] > 8) {
                        total -= plan.RowHeights[r] - 8;
                        plan.RowHeights[r] = 8;
                        shrunk = true;
                    }
                }
                if (total <= maxInteriorH) {
                    return plan;
                }
                if (!shrunk || plan.RowCount <= 2) {
                    //条带装不下最小两行=上游甲板节距被改动,fail loud
                    throw new System.InvalidOperationException(
                        $"[L3MazeBlock] 内膛上限{maxInteriorH}装不下最小迷宫(2行x8+楼板),检查甲板节距");
                }
                plan.RowCount--;
            }
        }

        /// <summary>计划的内膛净尺寸(不含壳),TryPlace预留用</summary>
        internal static Point InteriorSize(MazePlan plan) {
            int h = 2 * (plan.RowCount - 1);
            foreach (int rh in plan.RowHeights) {
                h += rh;
            }
            return new Point(plan.Width, h);
        }

        //==================== 构建(房间已预留;几何→缺口→装修,一遍冻结) ====================

        internal static MazeReport Build(RoomNode room, MazePlan plan, UnifiedRandom rand) {
            var report = new MazeReport();
            ushort wall = plan.Forbidden ? L3Palette.WallSlab : L3Palette.WallBase;

            //整包络重盖蓝砖(清预览残余)+开内膛
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L3Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, wall);

            //行几何:自下而上,rowFloor[r]=该行地板首行(实心),rowTop[r]=内膛顶行
            int n = plan.RowCount;
            var rowFloor = new int[n];
            var rowTop = new int[n];
            rowFloor[0] = room.FloorTop;
            for (int r = 0; r < n; r++) {
                rowTop[r] = rowFloor[r] - plan.RowHeights[r];
                if (r + 1 < n) {
                    rowFloor[r + 1] = rowTop[r] - 2;
                    //行间楼板2厚,偶发裂纹砖做旧(F12计群系)
                    for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                        for (int dy = 1; dy <= 2; dy++) {
                            TileBrush.SetSolid(x, rowTop[r] - dy,
                                rand.Next(100) < 6 ? L3Palette.CrackedBrick : L3Palette.Brick);
                        }
                    }
                }
            }

            //行内隔墙与格间清单
            var cells = new List<(int row, int left, int right)>();
            var rowCellIdx = new List<List<int>>();
            for (int r = 0; r < n; r++) {
                rowCellIdx.Add(new List<int>());
                int cellMin = plan.Forbidden ? 12 : 12;
                int cellMax = plan.Forbidden ? 14 : 19;
                int cursor = room.InteriorLeft;
                while (true) {
                    int cw = rand.Next(cellMin, cellMax);
                    int remain = room.InteriorRight - cursor;
                    //余量不足两格间:并入当前段收尾
                    if (remain < cw + 2 + cellMin) {
                        rowCellIdx[r].Add(cells.Count);
                        cells.Add((r, cursor, room.InteriorRight));
                        break;
                    }
                    rowCellIdx[r].Add(cells.Count);
                    cells.Add((r, cursor, cursor + cw));
                    //隔墙2厚+3高地口(行内全通)
                    int wx = cursor + cw;
                    for (int dx = 0; dx < 2; dx++) {
                        for (int y = rowTop[r]; y < rowFloor[r]; y++) {
                            TileBrush.SetSolid(wx + dx, y,
                                rand.Next(100) < 6 ? L3Palette.CrackedBrick : L3Palette.Brick);
                        }
                    }
                    TileBrush.CarveRect(wx, rowFloor[r] - 3, wx + 2, rowFloor[r], wall);
                    cursor = wx + 2;
                }
            }

            //楼板缺口(纵向循环):每对相邻行1~2个,只落中段三分之一(保两端死端)
            int midL = room.InteriorLeft + plan.Width / 3;
            int midR = room.InteriorRight - plan.Width / 3;
            var gapCells = new HashSet<int>();
            for (int r = 0; r + 1 < n; r++) {
                int gaps = plan.Width > 72 ? 2 : 1;
                for (int g = 0; g < gaps; g++) {
                    int gx = rand.Next(midL, System.Math.Max(midL + 1, midR - 3));
                    //缺口3宽:上行地板层开洞+平台盖口(§2.1 PlatformGap语义)
                    int upperFloor = rowFloor[r + 1];
                    TileBrush.CarveRect(gx, upperFloor, gx + 3, upperFloor + 2, wall);
                    TileBrush.PlatformRow(gx, gx + 3, upperFloor, L3Palette.PlatformFrameY);
                    //缺口下吊平台阶,竖距4(F2满跳6.6留余量)
                    for (int py = rowFloor[r] - 4; py > upperFloor + 1; py -= 4) {
                        TileBrush.PlatformRow(gx, gx + 3, py, L3Palette.PlatformFrameY);
                    }
                    //登记缺口触及的格间(上下两行),这些格间不再算死端
                    MarkGapCell(cells, rowCellIdx[r], gx, gapCells);
                    MarkGapCell(cells, rowCellIdx[r + 1], gx, gapCells);
                }
            }

            //死端判定:各行两端格间且无缺口;底行两端留给对外门口(链边/落口),不计
            var deadEnds = new List<int>();
            for (int r = 0; r < n; r++) {
                List<int> idx = rowCellIdx[r];
                if (idx.Count < 2) {
                    continue;
                }
                if (r > 0) {
                    if (!gapCells.Contains(idx[0])) {
                        deadEnds.Add(idx[0]);
                    }
                    if (!gapCells.Contains(idx[^1])) {
                        deadEnds.Add(idx[^1]);
                    }
                }
            }

            //风险格间选定:中行的缺口格间之外随机中段格间
            int riskCell = -1;
            if (plan.RiskCell && n >= 2) {
                List<int> midRow = rowCellIdx[n / 2];
                for (int attempt = 0; attempt < 8 && riskCell < 0; attempt++) {
                    int pick = midRow[rand.Next(midRow.Count)];
                    if (!deadEnds.Contains(pick)) {
                        riskCell = pick;
                    }
                }
            }

            //==================== 装修(几何已冻结) ====================

            report.Cells = cells.Count;
            report.DeadEnds = deadEnds.Count;
            bool goldUsed = false;
            for (int ci = 0; ci < cells.Count; ci++) {
                (int r, int left, int right) = cells[ci];
                bool soggy = plan.SoggyBottom && r == 0;
                bool isDeadEnd = deadEnds.Contains(ci);
                bool isRisk = ci == riskCell;

                if (soggy) {
                    FurnishSoggyCell(left, right, rowFloor[r], rand, ref report);
                    continue;
                }
                //书架阵列:架3宽+走道3~4(§2.4-③);死端格间只留1架给奖励腾位
                FurnishShelves(left, right, rowFloor[r], rowTop[r], plan, rand,
                    isDeadEnd, ref report);

                if (isRisk) {
                    //水蜡烛风险房:预点2支水蜡烛(原版机制刷怪压力up),零直伤
                    for (int c = 0; c < 2; c++) {
                        int wx = rand.Next(left + 1, right - 1);
                        if (L3Palette.PlaceOnSurface(wx, rowFloor[r] - 1, TileID.WaterCandle)) {
                            report.WaterCandles++;
                        }
                    }
                    //风险补偿:本格间地面加一只罐
                    WorldGen.PlacePot(rand.Next(left + 1, right - 1), rowFloor[r] - 1,
                        TileID.Pots, rand.Next(L3Palette.PotStyleMin, L3Palette.PotStyleMax + 1));
                }

                if (isDeadEnd) {
                    //死端必奖励:风险房在场时首个死端升档金箱(ROOMS-L3 §3)
                    FurnishDeadEnd(left, right, rowFloor[r], rand,
                        upgrade: plan.RiskCell && !goldUsed, ref report);
                    if (plan.RiskCell && !goldUsed) {
                        goldUsed = true;
                    }
                }
            }

            //墨霉做旧:书架底部墙面霉斑(paint层,INDEX §3签名)
            L3Palette.MoldUnderShelves(room.Bounds, rand);
            return report;
        }

        private static void MarkGapCell(List<(int row, int left, int right)> cells,
            List<int> rowCells, int gx, HashSet<int> gapCells) {
            foreach (int ci in rowCells) {
                if (gx >= cells[ci].left && gx < cells[ci].right) {
                    gapCells.Add(ci);
                    return;
                }
            }
        }

        //格间书架:地面排架+高格间40%夹层平台书墙(平台+书架,F7锚SolidWithTop)
        private static void FurnishShelves(int left, int right, int floor, int top,
            MazePlan plan, UnifiedRandom rand, bool deadEnd, ref MazeReport report) {
            int h = floor - top;
            int aisle = plan.Forbidden ? 3 : rand.Next(3, 5);
            int limit = deadEnd ? 1 : 99;
            int placedHere = 0;
            for (int x = left + 1; x + 3 <= right - 1 && placedHere < limit; x += 3 + aisle) {
                if (TryShelf(x + 1, floor - 1)) {
                    placedHere++;
                    report.ShelvesPlaced++;
                    //架顶蜡烛(锚书架顶【待游戏内验证】,拒绝静默走空,STRUCTURES §2.4-③待定项)
                    if (h >= 8 && rand.NextBool(3)) {
                        L3Palette.PlaceOnSurface(x + 1, floor - 5, TileID.Candles, L3Palette.StyleCandle);
                    }
                }
                else {
                    report.ShelvesRejected++;
                }
            }
            //夹层书墙:高格间平台带(留2宽登口)+平台上书架
            if (h >= 10 && right - left >= 12 && !deadEnd && rand.Next(100) < 40) {
                bool gapAtLeft = rand.NextBool();
                int mezzY = floor - 5;
                int mlkLeft = gapAtLeft ? left + 3 : left + 1;
                int mlkRight = gapAtLeft ? right - 1 : right - 3;
                TileBrush.PlatformRow(mlkLeft, mlkRight, mezzY, L3Palette.PlatformFrameY);
                for (int x = mlkLeft + 1; x + 3 <= mlkRight - 1; x += 3 + aisle) {
                    if (TryShelf(x + 1, mezzY - 1)) {
                        report.ShelvesPlaced++;
                    }
                    else {
                        report.ShelvesRejected++;
                    }
                }
            }
        }

        //书架放置:失败缩排距重试一次,再失败留空(§2.4-③)
        private static bool TryShelf(int centerX, int standRow) {
            if (L3Palette.TryPlaceTile(centerX, standRow, TileID.Bookcases, L3Palette.StyleBookcase)) {
                return true;
            }
            return L3Palette.TryPlaceTile(centerX - 1, standRow, TileID.Bookcases, L3Palette.StyleBookcase);
        }

        //死端奖励:upgrade=金箱(风险补偿),否则木箱/罐/书堆三选一,100%有物(§2.4-③)
        private static void FurnishDeadEnd(int left, int right, int floor,
            UnifiedRandom rand, bool upgrade, ref MazeReport report) {
            int mid = (left + right) / 2;
            bool placed;
            if (upgrade) {
                placed = L3Palette.PlaceChestWithLoot(mid, floor - 1, gold: true);
            }
            else {
                placed = rand.Next(3) switch {
                    0 => L3Palette.PlaceChestWithLoot(mid, floor - 1, gold: false),
                    1 => WorldGen.PlacePot(mid, floor - 1, TileID.Pots,
                        rand.Next(L3Palette.PotStyleMin, L3Palette.PotStyleMax + 1)),
                    _ => PlaceBookNook(left, right, floor, rand),
                };
            }
            if (placed) {
                report.Rewards++;
            }
            else {
                //死端空手=违反花名册纪律,fail loud
                CWRMod.Instance.Logger.Warn($"[L3MazeBlock] 死端奖励放置失败 at ({mid},{floor - 1})");
            }
        }

        //书堆彩蛋位:地面2~3本书+墨瓶(样式0~4,水矢书5禁用)
        private static bool PlaceBookNook(int left, int right, int floor, UnifiedRandom rand) {
            bool any = false;
            int count = rand.Next(2, 4);
            for (int c = 0; c < count; c++) {
                int x = rand.Next(left + 1, right - 1);
                any |= L3Palette.PlaceBook(x, floor - 1, rand);
            }
            any |= L3Palette.PlaceInkBottle(rand.Next(left + 1, right - 1), floor - 1, rand);
            return any;
        }

        //泡皱格间(L3→L4隔离带预告,ROOMS-L3 §4):书架留空,散书+灰漆水渍爬墙
        private static void FurnishSoggyCell(int left, int right, int floor,
            UnifiedRandom rand, ref MazeReport report) {
            int books = rand.Next(2, 5);
            for (int c = 0; c < books; c++) {
                L3Palette.PlaceBook(rand.Next(left + 1, right - 1), floor - 1, rand);
            }
            //水渍:下沿墙面灰漆短竖痕,每隔3~5列一道
            for (int x = left + 1; x < right - 1; x += rand.Next(3, 6)) {
                for (int dy = 0; dy < 2; dy++) {
                    Tile tile = Main.tile[x, floor - 1 - dy];
                    if (!tile.HasTile && (tile.WallType == L3Palette.WallBase
                        || tile.WallType == L3Palette.WallSlab)) {
                        tile.WallColor = L3Palette.PaintMold;
                    }
                }
            }
            report.Rewards += 0;
        }
    }
}
