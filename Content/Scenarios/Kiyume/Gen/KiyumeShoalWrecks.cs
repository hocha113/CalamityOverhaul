using CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Prefabs;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    //D 水缘：断裂栈桥 + 搁浅船骸三态 + 废苇塘（P3 计划书 §3.S4）
    //铁律：全部足印入 ScatterExclusions；桥面 FloorTop 不回写（桥不是地面）；
    //船骸坐床微削与苇塘围坎按 §4 总表回写；随机全程 genRand，写砖全走 TileBrush/预制件
    internal static class KiyumeShoalWrecks
    {
        //材质：幽木船板/苇杆（KiyumeStructures 船骸签名 WreckHullAt 认幽木壳，改料必同步该表）
        private const ushort PlankTile = TileID.SpookyWood;
        private const ushort PileTile = TileID.WoodenBeam;
        private const ushort BermTile = TileID.Mud;
        private const ushort HullWall = WallID.SpookyWood;

        //滩上船骸落位窗（东避旱田 [516,558]，西避栈桥岸端 332±8+岸接 ≤350）；
        //龙骨肋窗随栈桥西移退到 [220,254)：肋足印 ≤253 与栈桥足印西缘 ≥263 永不相撞
        private const int WreckShoreL = 386;
        private const int WreckShoreR = 512;
        private const int KeelL = 220;
        private const int KeelR = 254;
        //苇杆高度（格）
        private const int ReedStalkHMin = 3;
        private const int ReedStalkHMax = 6;

        //翻扣壳（可进）：顶脊破洞、东舷 3 高破口即入口，内膛登记 HideVolume(船骸)
        private static readonly KiyumePrefab HullCapsized = KiyumePrefab.Parse("船骸翻扣壳", [
            "   ####..####   ",
            "  ##........##  ",
            " ##..........## ",
            " #............D ",
            " #............D ",
            " ##...........D ",
        ], null);

        //侧倾半埋：舷板斜插出泥，板下楔形空隙登记 HideVolume（1-2 格带，封闭暗袋）
        private static readonly KiyumePrefab HullTilted = KiyumePrefab.Parse("船骸侧倾半埋", [
            "        ##  ",
            "      ##.#  ",
            "    ##...#  ",
            "  ##.....#  ",
            "##......##  ",
            "############",
        ], null);

        //龙骨肋（水下）：肋间留水，泡在湖床上
        private static readonly KiyumePrefab KeelRibs = KiyumePrefab.Parse("船骸龙骨肋", [
            "#  #  #  #  #  #",
            "#  #  #  #  #  #",
            "################",
        ], null);

        /// <summary>StructurePass 水缘挂点入口（生成端）</summary>
        internal static void Build() {
            int jettyLen = BuildJetty();
            int wrecks = BuildWrecks();
            (int cells, int stalks) = BuildReedPond();
            CWRMod.Instance.Logger.Info(
                $"[Kiyume] 水缘 栈桥长={jettyLen} 船骸={wrecks} 苇塘格={cells} 苇杆={stalks}");
        }

        //════════ 栈桥 ════════

        //断裂栈桥：东岸端向西探向湖面，桥面行 464=水面 470 上 6 行、滩涂西缘地板 466 上 2 行
        //断口以西只剩歪桩残根；FloorTop 全程不回写，下方滩涂/湖床仍是站立层
        private static int BuildJetty() {
            int deck = KiyumeMetrics.JettyDeckRow;
            int root = KiyumeMetrics.JettyRootX
                + WorldGen.genRand.Next(-KiyumeMetrics.JettyRootJitter, KiyumeMetrics.JettyRootJitter + 1);
            int len = WorldGen.genRand.Next(KiyumeMetrics.JettyLenMin, KiyumeMetrics.JettyLenMax + 1);
            int breakLen = WorldGen.genRand.Next(KiyumeMetrics.JettyBreakMin, KiyumeMetrics.JettyBreakMax + 1);
            int west = root - len;           //足印西界（断口西缘）
            int deckWest = west + breakLen;  //断口以东才有桥面

            //岸接：桥面向东延到地面追平，别让桥头悬在岸边
            int east = root;
            while (east < root + 8 && KiyumePlans.FloorTopAt(east + 1) > deck) {
                east++;
            }
            for (int x = deckWest; x <= east; x++) {
                KiyumeTileBrush.SetPlatform(x, deck, KiyumeMetrics.PlatformFrameY);
            }

            //常规桥桩：自东向西每 4-6 列一对（2 宽）
            int maxBottom = deck;
            for (int px = east - 3; px > deckWest + 4; px -= WorldGen.genRand.Next(4, 7)) {
                maxBottom = Math.Max(maxBottom, PlantPile(px, deck));
            }

            //断口东侧最后完好桩：桩头出桥面三格，向断口挑臂挂灯——P5「湖上孤灯」事件位
            int lc = deckWest + 1;
            maxBottom = Math.Max(maxBottom, PlantPile(lc, deck));
            KiyumeTileBrush.FillRect(lc, deck - 3, lc + 2, deck, PileTile);
            KiyumeTileBrush.SetSolid(lc - 1, deck - 3, PlankTile);
            if (!KiyumeTileBrush.TryPlaceObject(lc - 1, deck - 2, TileID.HangingLanterns, 0)) {
                //锚定拒绝退火把，灯位照登（P5 只认位置）
                KiyumeTileBrush.SetTorch(lc - 1, deck - 2);
            }
            KiyumeStructures.LanternPosts.Add(new Point(lc - 1, deck - 2));

            //断口西侧歪桩残根 1-2 根：斜切歪头
            int stumps = WorldGen.genRand.Next(1, 3);
            for (int i = 0; i < stumps; i++) {
                int sx = west + WorldGen.genRand.Next(1, Math.Max(breakLen - 2, 2));
                int ground = KiyumePlans.FloorTopAt(sx);
                int top = deck + WorldGen.genRand.Next(2, 5);
                if (top >= ground) {
                    top = ground - 2;
                }
                KiyumeTileBrush.SetSloped(sx, top, PileTile, (SlopeType)WorldGen.genRand.Next(1, 5));
                int bottom = Math.Min(top + 1 + WorldGen.genRand.Next(3, 7), ground);
                KiyumeTileBrush.FillRect(sx, top + 1, sx + 1, bottom, PileTile);
                maxBottom = Math.Max(maxBottom, bottom);
            }

            //整桥足印（含桩/挑臂/灯）入撒布禁区，防礁石叠上桥面
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(west - 1, deck - 6, east - west + 3, maxBottom - (deck - 6) + 2));
            return len;
        }

        //一对桥桩：滩上落地到底；水上插进水里后断掉成悬桩，再从湖床补 1-2 格残桩
        //接续视觉（计划书风险①「悬桩读作漂浮」的预防性执行）。返回桩底行
        private static int PlantPile(int px, int deck) {
            int bottomMost = deck;
            for (int c = px; c <= px + 1; c++) {
                int ground = KiyumePlans.FloorTopAt(c);
                int hang = deck + 1 + WorldGen.genRand.Next(8, 13);
                if (ground <= KiyumeMetrics.LakeSurfaceRow || hang >= ground - 2) {
                    //滩上或浅水：桩直落地面
                    KiyumeTileBrush.FillRect(c, deck + 1, c + 1, ground, PileTile);
                }
                else {
                    KiyumeTileBrush.FillRect(c, deck + 1, c + 1, hang, PileTile);
                    int stub = WorldGen.genRand.Next(1, 3);
                    KiyumeTileBrush.FillRect(c, ground - stub, c + 1, ground, PileTile);
                }
                bottomMost = Math.Max(bottomMost, ground);
            }
            return bottomMost;
        }

        //════════ 船骸 ════════

        //滩上 2-3 艘：翻扣/侧倾两态必齐（抽签不重复），第三艘随机补，先后洗牌；另湖底龙骨肋 1 副
        private static int BuildWrecks() {
            int count = WorldGen.genRand.Next(KiyumeMetrics.WreckShoreMin, KiyumeMetrics.WreckShoreMax + 1);
            List<int> kinds = [0, 1];
            if (count >= 3) {
                kinds.Add(WorldGen.genRand.Next(2));
            }
            for (int i = kinds.Count - 1; i > 0; i--) {
                int j = WorldGen.genRand.Next(i + 1);
                (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
            }

            int placed = 0;
            List<Rectangle> taken = [];
            foreach (int kind in kinds) {
                KiyumePrefab prefab = kind == 0 ? HullCapsized : HullTilted;
                for (int attempt = 0; attempt < 24; attempt++) {
                    int left = WorldGen.genRand.Next(WreckShoreL, WreckShoreR - prefab.Width);
                    var span = new Rectangle(left - 4, 0, prefab.Width + 8, 1);
                    if (OverlapX(taken, span)
                        || KiyumeStructures.InExclusion(left, KiyumePlans.FloorTopAt(left) - 2)
                        || KiyumeStructures.InExclusion(left + prefab.Width, KiyumePlans.FloorTopAt(left + prefab.Width) - 2)) {
                        continue;
                    }
                    PlaceShoreWreck(prefab, kind, left);
                    taken.Add(span);
                    placed++;
                    break;
                }
            }
            if (PlaceKeel()) {
                placed++;
            }
            return placed;
        }

        private static bool OverlapX(List<Rectangle> taken, Rectangle probe) {
            foreach (Rectangle r in taken) {
                if (probe.X < r.X + r.Width && r.X < probe.X + probe.Width) {
                    return true;
                }
            }
            return false;
        }

        private static void PlaceShoreWreck(KiyumePrefab prefab, int kind, int left) {
            //坐床：足印削垫到同一行并回写（半埋态底排顶替原地表行=「微削」；翻扣态壳沿坐在地上）
            int seat = Seat(left, left + prefab.Width);
            int bottom = kind == 0 ? seat - 1 : seat;
            int top = bottom - prefab.Height + 1;
            prefab.StampGeometry(left, top, PlankTile, HullWall, KiyumeMetrics.PlatformFrameY);

            Rectangle area = prefab.Area(left, top);
            area.Inflate(1, 1);
            KiyumeStructures.ScatterExclusions.Add(area);

            //藏身登记：翻扣=整内膛；侧倾=舷板下空隙带（贴底 2 行）
            Rectangle hide = kind == 0
                ? new Rectangle(left + 2, top + 2, 12, 4)
                : new Rectangle(left + 2, top + 3, 6, 2);
            KiyumeStructures.HideVolumes.Add((hide, KiyumeStructures.KindWreck));
        }

        //水下龙骨肋：湖床上一排肋骨，肋间留水；基线到实际床面的落差补板（床面起伏 ±7 防浮空）
        private static bool PlaceKeel() {
            for (int attempt = 0; attempt < 20; attempt++) {
                int left = WorldGen.genRand.Next(KeelL, KeelR - KeelRibs.Width);
                int bed = KiyumePlans.FloorTopAt(left + KeelRibs.Width / 2);
                if (bed < KiyumeMetrics.LakeSurfaceRow + 5) {
                    continue;   //水太浅，肋尖会出水，换位
                }
                if (KiyumeStructures.InExclusion(left, bed - 2)
                    || KiyumeStructures.InExclusion(left + KeelRibs.Width, bed - 2)) {
                    continue;
                }
                int bottom = bed - 1;
                int top = bottom - KeelRibs.Height + 1;
                KeelRibs.StampGeometry(left, top, PlankTile, WallID.None, KiyumeMetrics.PlatformFrameY);
                for (int x = left; x < left + KeelRibs.Width; x++) {
                    for (int y = bottom + 1; y < KiyumePlans.FloorTopAt(x); y++) {
                        KiyumeTileBrush.SetSolid(x, y, PlankTile);
                    }
                }
                Rectangle area = KeelRibs.Area(left, top);
                area.Inflate(1, 2);
                KiyumeStructures.ScatterExclusions.Add(area);
                return true;
            }
            return false;
        }

        //足印削垫到区间最高地面行并回写 FloorTop（Flatten 同款双动作，垫料淤泥），区间半开
        private static int Seat(int left, int right) {
            int row = int.MaxValue;
            for (int x = left; x < right; x++) {
                row = Math.Min(row, KiyumePlans.FloorTopAt(x));
            }
            int[] topArr = KiyumePlans.FloorTop;
            for (int x = left; x < right; x++) {
                int cur = KiyumePlans.FloorTopAt(x);
                if (cur > row) {
                    KiyumeTileBrush.FillRect(x, row, x + 1, cur, BermTile);
                }
                else if (cur < row) {
                    KiyumeTileBrush.CarveRect(x, cur, x + 1, row);
                }
                if (topArr != null && x >= 0 && x < topArr.Length) {
                    topArr[x] = row;
                }
            }
            return row;
        }

        //════════ 苇塘 ════════

        //废苇塘：[560,600) 窗内 2-4 格蓄水洼地。围坎淤泥垫高回写；内膛下挖 1-2 格构造性
        //灌水（FillLake 同法，NormalUpdates=false 定型）。水面行≈452-455，高于低潮雾线
        //458（行小=高）：退潮浮出雾面、涨潮沉底，潮汐叙事免费
        private static (int cells, int stalks) BuildReedPond() {
            int cells = 0;
            int stalks = 0;
            int n = WorldGen.genRand.Next(KiyumeMetrics.ReedPondCellsMin, KiyumeMetrics.ReedPondCellsMax + 1);
            int x = KiyumeMetrics.ReedPondLeft + WorldGen.genRand.Next(0, 3);
            while (cells < n) {
                //首格保底密簇（窄步距+足宽），别让整塘凑不出 ≥4 根的藏身簇；后续格四成密
                bool dense = cells == 0 || WorldGen.genRand.NextFloat() < 0.4f;
                int bermW = WorldGen.genRand.Next(1, 3);
                int innerW = dense ? WorldGen.genRand.Next(8, 11) : WorldGen.genRand.Next(6, 11);
                int cellW = bermW * 2 + innerW;
                if (x + cellW > KiyumeMetrics.ReedPondRight) {
                    break;   //窗只有 40 列，装不下就少一格
                }
                stalks += BuildReedCell(x, bermW, innerW, dense);
                cells++;
                x += cellW + WorldGen.genRand.Next(2, 5);
            }
            return (cells, stalks);
        }

        private static int BuildReedCell(int left, int bermW, int innerW, bool dense) {
            int right = left + bermW * 2 + innerW;
            int g = int.MaxValue;
            for (int x = left; x < right; x++) {
                g = Math.Min(g, KiyumePlans.FloorTopAt(x));
            }
            int depth = WorldGen.genRand.Next(1, 3);
            int bermH = WorldGen.genRand.Next(1, 3);
            int[] topArr = KiyumePlans.FloorTop;

            for (int x = left; x < right; x++) {
                int cur = KiyumePlans.FloorTopAt(x);
                if (x < left + bermW || x >= right - bermW) {
                    //围坎：两端淤泥垫高 1-2 并回写
                    KiyumeTileBrush.FillRect(x, g - bermH, x + 1, cur, BermTile);
                    if (topArr != null) {
                        topArr[x] = g - bermH;
                    }
                    continue;
                }
                //内膛：清净空 → 挖 1-2 格灌水 → 坎底封实；FloorTop=坎底（水中可站，踩进去水齐膝）
                KiyumeTileBrush.CarveRect(x, g - 5, x + 1, g);
                for (int y = g; y < g + depth; y++) {
                    KiyumeTileBrush.SetWater(x, y);
                }
                KiyumeTileBrush.FillRect(x, g + depth, x + 1, Math.Max(cur, g + depth + 1), BermTile);
                if (topArr != null) {
                    topArr[x] = g + depth;
                }
            }

            //苇杆：1 宽幽木 3-6 高、步距 2-4 列（间距 1-3），三成杆顶挑 1 格横枝
            //步距 ≤3 的连续 ≥4 根密簇段登记 HideVolume(苇丛)，与签名 ReedsAt(±5 列凑 4 根)同口径
            int stalkCount = 0;
            List<int> run = [];
            void FlushRun() {
                if (run.Count >= 4) {
                    KiyumeStructures.HideVolumes.Add((
                        new Rectangle(run[0], g - 3, run[^1] - run[0] + 1, depth + 3),
                        KiyumeStructures.KindReeds));
                }
                run.Clear();
            }
            int inL = left + bermW;
            int inR = right - bermW;
            int floorRow = g + depth;
            for (int x = inL + WorldGen.genRand.Next(0, 2); x < inR;
                x += dense ? 2 : WorldGen.genRand.Next(KiyumeMetrics.ReedStepMin, KiyumeMetrics.ReedStepMax + 1)) {
                int h = WorldGen.genRand.Next(ReedStalkHMin, ReedStalkHMax + 1);
                KiyumeTileBrush.FillRect(x, floorRow - h, x + 1, floorRow, PlankTile);
                if (WorldGen.genRand.NextFloat() < 0.3f) {
                    KiyumeTileBrush.SetSolid(x + (WorldGen.genRand.NextBool() ? 1 : -1), floorRow - h, PlankTile);
                }
                stalkCount++;
                if (run.Count > 0 && x - run[^1] > 3) {
                    FlushRun();
                }
                run.Add(x);
            }
            FlushRun();

            //整格足印（含苇杆冠）入撒布禁区
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(left - 1, g - 9, right - left + 2, depth + 12));
            return stalkCount;
        }
    }
}
