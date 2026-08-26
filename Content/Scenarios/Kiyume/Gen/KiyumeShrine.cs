using CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Prefabs;
using System;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    /// <summary>
    /// 信仰轴线（P3-C）：鸟居×3 / 村社 compound / 路边祠×3-4 / 山道石阶 / 山顶祠。<br/>
    /// 由 <see cref="Passes.KiyumeStructurePass"/>（P40）调用，晚于村落 Build——
    /// 村社段靠 <see cref="KiyumeStructures.PlanReservations"/> 预留，若预留未被村落读表
    /// 仍照常建造（清场 + 记冲突日志，待 W4 核）。<br/>
    /// 签名建筑全走字符画预制件；石阶程序化切台（远山 FloorTop 大段改写 + 回写，级差 ≤3）。
    /// 全部足印登记 ScatterExclusions，鸟居/社祠矩形入 ToriiGates/Shrines。
    /// </summary>
    internal static class KiyumeShrine
    {
        //════════ 材质表（血暮下读作暗红木/灰石/白壁） ════════

        private const ushort BeamTile = TileID.WoodenBeam;          //柱身（非实心，门内可穿行）
        private const ushort ShingleTile = TileID.RedDynastyShingles;
        private const ushort StoneTile = TileID.GrayBrick;          //柱脚石/祠身/石灯
        private const ushort SlabTile = TileID.StoneSlab;           //台基/石阶面层
        private const ushort HallTile = TileID.DynastyWood;         //拜殿殿身
        private const ushort HallWall = WallID.WhiteDynasty;        //拜殿内墙：全村唯一白（签名唯一性）
        private const ushort NicheWall = WallID.StoneSlab;          //祠龛衬墙

        //道祖神石像池（对源 Item.cs：438 StarStatue→style2 顺推）：女/石像鬼/幽/十字/柱
        private static readonly int[] StatueStyles = [12, 14, 15, 22, 36];

        //════════ 预制件（解析即校验，进程内一次解析跨次复用） ════════

        //共用语义槽：R=笠木瓦 G=柱脚石 s=石像(退蜡烛) n=赛钱箱(酒桶) B=佛坛烛 L=吊灯
        private static KiyumePrefabSlotDef SlotRoof => new() {
            Ch = 'R', Name = "笠木瓦",
            Place = (x, y) => { KiyumeTileBrush.SetSolid(x, y, ShingleTile); return true; },
        };
        private static KiyumePrefabSlotDef SlotFoot => new() {
            Ch = 'G', Name = "柱脚石",
            Place = (x, y) => { KiyumeTileBrush.SetSolid(x, y, StoneTile); return true; },
        };
        private static KiyumePrefabSlotDef SlotStatue => new() {
            Ch = 's', Name = "道祖神像",
            //石像 2×3 底左原点（对源 Place2xX）；锚定失败退「平台供台+烛」——
            //烛台只认 tileTable 锚，直接落实心龛底会被收尾 CheckOnTable1x1 杀掉
            Place = (x, y) => {
                if (KiyumeTileBrush.TryPlaceTile(x, y, TileID.Statues,
                        StatueStyles[WorldGen.genRand.Next(StatueStyles.Length)])) {
                    return true;
                }
                KiyumeTileBrush.SetPlatform(x, y, KiyumeMetrics.PlatformFrameY);
                return KiyumeTileBrush.TryPlaceTile(x, y - 1, TileID.Candles, 0);
            },
        };
        private static KiyumePrefabSlotDef SlotOffertory => new() {
            Ch = 'n', Name = "赛钱箱",
            //酒桶 2×2 顶左原点在槽位上一行（TryPlaceObject 自带纵向容错）
            Place = (x, y) => KiyumeTileBrush.TryPlaceObject(x, y - 1, TileID.Kegs, 0),
        };
        private static KiyumePrefabSlotDef SlotAltar => new() {
            Ch = 'B', Name = "佛坛烛",
            //烛台只认 tileTable 锚（实心殿地板收尾即毁）：垫平台佛坛再落烛，高挂同民居成规
            Place = (x, y) => {
                KiyumeTileBrush.SetPlatform(x, y - 1, KiyumeMetrics.PlatformFrameY);
                return KiyumeTileBrush.TryPlaceTile(x, y - 2, TileID.Candles, 0);
            },
        };
        private static KiyumePrefabSlotDef SlotLantern => new() {
            Ch = 'L', Name = "殿内吊灯",
            //铁链灯（HangingLanterns 样式 0），悬于顶行实心之下
            Place = (x, y) => KiyumeTileBrush.TryPlaceObject(x, y, TileID.HangingLanterns, 0),
        };

        //村口红鸟居 14×13：笠木出挑 2 / 岛木 / 贯 / 柱宽 2 内净 6 / 柱脚石落地
        private static KiyumePrefab _toriiRed;
        private static KiyumePrefab ToriiRed => _toriiRed ??= KiyumePrefab.Parse("红鸟居", [
            "RRRRRRRRRRRRRR",
            " ############ ",
            "  ##      ##  ",
            "  ##      ##  ",
            " ############ ",
            "  ##      ##  ",
            "  ##      ##  ",
            "  ##      ##  ",
            "  ##      ##  ",
            "  ##      ##  ",
            "  ##      ##  ",
            "  ##      ##  ",
            "  GG      GG  ",
        ], new KiyumePrefabLegend().Add(SlotRoof).Add(SlotFoot));

        //送葬道口素鸟居 12×10：无瓦全素木
        private static KiyumePrefab _toriiPlain;
        private static KiyumePrefab ToriiPlain => _toriiPlain ??= KiyumePrefab.Parse("素鸟居", [
            "############",
            "  ##    ##  ",
            " ########## ",
            "  ##    ##  ",
            "  ##    ##  ",
            "  ##    ##  ",
            "  ##    ##  ",
            "  ##    ##  ",
            "  ##    ##  ",
            "  GG    GG  ",
        ], new KiyumePrefabLegend().Add(SlotFoot));

        //村社拜殿 26×12：脊高 6 出檐 2，双门洞（3 高，与台基顶齐平直入），
        //佛坛烛 B / 赛钱箱 n 落在板间；内墙 WhiteDynasty
        private static KiyumePrefab _hall;
        private static KiyumePrefab Hall => _hall ??= KiyumePrefab.Parse("村社拜殿", [
            "          RRRRRR          ",
            "        RRRRRRRRRR        ",
            "      RRRRRRRRRRRRRR      ",
            "    RRRRRRRRRRRRRRRRRR    ",
            "  RRRRRRRRRRRRRRRRRRRRRR  ",
            "RRRRRRRRRRRRRRRRRRRRRRRRRR",
            "  ######################  ",
            "  #....................#  ",
            "  D....................D  ",
            "  D....................D  ",
            "  D......B......n......D  ",
            "  ######################  ",
        ], new KiyumePrefabLegend().Add(SlotRoof).Add(SlotAltar).Add(SlotOffertory));

        //路边祠·完好 4×6：石龛全框，龛内道祖神对着路
        private static KiyumePrefab _waysideIntact;
        private static KiyumePrefab WaysideIntact => _waysideIntact ??= KiyumePrefab.Parse("路祠完好", [
            "####",
            "#..#",
            "#..#",
            "#..#",
            "#s.#",
            "####",
        ], new KiyumePrefabLegend().Add(SlotStatue));

        //路边祠·塌顶 4×6：顶行缺角 + 斜切残端（字符即 SlopeType 枚举值）
        private static KiyumePrefab _waysideRuined;
        private static KiyumePrefab WaysideRuined => _waysideRuined ??= KiyumePrefab.Parse("路祠塌顶", [
            "2   ",
            "#1  ",
            "#..#",
            "#..#",
            "#s.#",
            "####",
        ], new KiyumePrefabLegend().Add(SlotStatue));

        //山顶祠 6×7：路祠放大 + 西侧敞口（回望雾海）+ 殿内吊灯常明
        private static KiyumePrefab _summit;
        private static KiyumePrefab Summit => _summit ??= KiyumePrefab.Parse("山顶祠", [
            "######",
            "#L...#",
            "#....#",
            "D....#",
            "D....#",
            "D.s..#",
            "######",
        ], new KiyumePrefabLegend().Add(SlotLantern).Add(SlotStatue));

        //════════ 入口 ════════

        internal static void Build(GenerationProgress progress) {
            KiyumePlans.Report(progress, "参道还记得每一双鞋...");

            //红鸟居 14 宽柱脚在 2..3 / 10..11；素鸟居 12 宽柱脚在 2..3 / 8..9
            BuildTorii(ToriiRed, KiyumeMetrics.ToriiWestX, KiyumeMetrics.ToriiWestJitterCols, 2, 10);
            BuildTorii(ToriiRed, KiyumeMetrics.ToriiEastX, KiyumeMetrics.ToriiEastJitterCols, 2, 10);
            BuildTorii(ToriiPlain, KiyumeMetrics.ToriiFuneralX, KiyumeMetrics.ToriiFuneralJitterCols, 2, 8);

            BuildShrineCompound();
            BuildWaysideShrines();
            BuildStairsAndSummit();

            CWRMod.Instance.Logger.Info(
                $"[Kiyume] 信仰轴线 鸟居={KiyumeStructures.ToriiGates.Count}"
                + $" 社祠={KiyumeStructures.Shrines.Count}");
        }

        //════════ 鸟居 ════════

        private static void BuildTorii(KiyumePrefab prefab, int anchorX, int jitter, int footL, int footR) {
            //先抽签落点；足印被既有结构占用（民居等）则在抖动窗内向两侧找空位，
            //找不到就原位建并记冲突（预留/避让失效待 W4 核）
            int center = anchorX + WorldGen.genRand.Next(-jitter, jitter + 1);
            int left = center - prefab.Width / 2;
            if (SpanOccupied(left, left + prefab.Width, prefab.Height + 3)) {
                bool found = false;
                for (int d = 1; d <= jitter * 2 && !found; d++) {
                    foreach (int cand in new[] { anchorX - d, anchorX + d }) {
                        int candLeft = cand - prefab.Width / 2;
                        if (!SpanOccupied(candLeft, candLeft + prefab.Width, prefab.Height + 3)) {
                            left = candLeft;
                            found = true;
                            break;
                        }
                    }
                }
                if (!found) {
                    CWRMod.Instance.Logger.Warn($"[Kiyume] 鸟居@{anchorX} 落点全被占,原位强建(冲突待核)");
                }
            }

            //柱脚两段 2×2 列削垫到同一行（两脚间地面不动）
            int gL = left + footL;
            int gR = left + footR;
            int ground = Math.Min(
                Math.Min(KiyumePlans.FloorTopAt(gL), KiyumePlans.FloorTopAt(gL + 1)),
                Math.Min(KiyumePlans.FloorTopAt(gR), KiyumePlans.FloorTopAt(gR + 1)));
            FlattenSpan(gL, gL + 2, ground);
            FlattenSpan(gR, gR + 2, ground);

            //柱脚石落在 ground-1（预制件底行），FloorTop 回写到石顶
            int top = ground - prefab.Height;
            prefab.StampGeometry(left, top, BeamTile, WallID.None, KiyumeMetrics.PlatformFrameY);
            prefab.PlaceSlots(left, top);
            RewriteFloorTop(gL, gL + 2, ground - 1);
            RewriteFloorTop(gR, gR + 2, ground - 1);

            Rectangle area = prefab.Area(left, top);
            KiyumeStructures.ToriiGates.Add(area);
            KiyumeStructures.ScatterExclusions.Add(area);
        }

        //════════ 村社 compound ════════

        private static void BuildShrineCompound() {
            //台基靠西落位，东端 ≥12 列后院空地留给 E 包社后井（E 按 Shrines 矩形定位）
            int baseL = KiyumeMetrics.ShrineSpanL + 2 + WorldGen.genRand.Next(3);
            int baseR = baseL + KiyumeMetrics.ShrineBaseCols;
            int ground = HighestGround(baseL, baseR);

            //预留未被村落读表时段内会有民居：清场并记冲突
            if (SpanOccupied(baseL, baseR, 22)) {
                CWRMod.Instance.Logger.Warn("[Kiyume] 村社段预留未生效,清场强建(冲突待W4核)");
            }
            KiyumeTileBrush.CarveRect(baseL, ground - 22, baseR, ground);

            //台基：44 列削垫回写 + StoneSlab 3 行，回写到台基顶（第一格实心）
            FlattenSpan(baseL, baseR, ground);
            int baseTop = ground - KiyumeMetrics.ShrineBaseRows;
            KiyumeTileBrush.FillRect(baseL, baseTop, baseR, ground, SlabTile);
            RewriteFloorTop(baseL, baseR, baseTop);

            //台基两端收口斜切（东升/西降各一格，上台不必贴脸跳）
            KiyumeTileBrush.SetSloped(baseL - 1, ground - 1, SlabTile, SlopeType.SlopeDownLeft);
            KiyumeTileBrush.SetSloped(baseR, ground - 1, SlabTile, SlopeType.SlopeDownRight);

            //拜殿居中（两端各出 9），底行沉进台基顶行：殿内地板与前庭齐平直入
            int hallL = baseL + (KiyumeMetrics.ShrineBaseCols - Hall.Width) / 2;
            int hallTop = baseTop - Hall.Height + 1;
            Hall.StampGeometry(hallL, hallTop, HallTile, HallWall, KiyumeMetrics.PlatformFrameY);
            Hall.PlaceSlots(hallL, hallTop);

            //前庭对称陈设：石灯对（外端）+ 注连柱对（近殿，WoodenBeam 2×7 非实心可穿）
            int stand = baseTop - 1;
            StoneLantern(baseL + 1, stand);
            StoneLantern(baseR - 2, stand);
            KiyumeTileBrush.FillRect(baseL + 5, baseTop - 7, baseL + 7, baseTop, BeamTile);
            KiyumeTileBrush.FillRect(baseR - 7, baseTop - 7, baseR - 5, baseTop, BeamTile);

            //登记：compound 矩形（含拜殿），E 包社后井以此定位后院
            var area = new Rectangle(baseL, hallTop, KiyumeMetrics.ShrineBaseCols, ground - hallTop);
            KiyumeStructures.Shrines.Add(area);
            KiyumeStructures.ScatterExclusions.Add(area);
        }

        //════════ 路边祠 ════════

        private static void BuildWaysideShrines() {
            int count = WorldGen.genRand.Next(KiyumeMetrics.WaysideCountMin, KiyumeMetrics.WaysideCountMax + 1);
            int placed = 0;
            //村缘一座：东口鸟居与送葬鸟居之间的空当
            if (TryWayside(1682, 1696)) {
                placed++;
            }
            //枯林其余：两段窗（避开 E 包墓园窗 [1980,2240] 与送葬鸟居），彼此间距 ≥30 列
            int guard = 0;
            var used = new System.Collections.Generic.List<int>();
            while (placed < count && guard++ < 40) {
                bool west = WorldGen.genRand.NextBool();
                int x = west ? WorldGen.genRand.Next(1740, 1972) : WorldGen.genRand.Next(2250, 2456);
                bool tooClose = false;
                foreach (int u in used) {
                    if (Math.Abs(u - x) < 30) {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose || !TryWayside(x, x + 4)) {
                    continue;
                }
                used.Add(x);
                placed++;
            }
            if (placed < KiyumeMetrics.WaysideCountMin) {
                CWRMod.Instance.Logger.Warn($"[Kiyume] 路祠只落成{placed}座(<{KiyumeMetrics.WaysideCountMin})");
            }
        }

        //在 [left,right) 内落一座路祠（两态抽签）；占用即失败
        private static bool TryWayside(int left, int right) {
            int x = left + (right - left > 4 ? WorldGen.genRand.Next(right - left - 4) : 0);
            KiyumePrefab prefab = WorldGen.genRand.NextFloat() < 0.4f ? WaysideRuined : WaysideIntact;
            if (SpanOccupied(x - 1, x + 5, prefab.Height + 3)) {
                return false;
            }
            //4 列削垫回写，龛底行沉进地表：祠脚不悬空
            int ground = HighestGround(x, x + 4);
            FlattenSpan(x, x + 4, ground);
            int top = ground - prefab.Height + 1;
            prefab.StampGeometry(x, top, StoneTile, NicheWall, KiyumeMetrics.PlatformFrameY);
            prefab.PlaceSlots(x, top);

            Rectangle area = prefab.Area(x, top);
            KiyumeStructures.Shrines.Add(area);
            KiyumeStructures.ScatterExclusions.Add(area);
            return true;
        }

        //════════ 山道石阶 + 山顶祠 ════════

        private static void BuildStairsAndSummit() {
            int x = KiyumeMetrics.StairStartX;
            int level = KiyumePlans.FloorTopAt(x);
            int startLevel = level;
            int stepsToRest = WorldGen.genRand.Next(KiyumeMetrics.StairRestStepsMin, KiyumeMetrics.StairRestStepsMax + 1);
            int restIdx = 0;

            while (x < KiyumeMetrics.SummitL) {
                bool rest = stepsToRest <= 0;
                int w = rest
                    ? WorldGen.genRand.Next(KiyumeMetrics.StairRestColsMin, KiyumeMetrics.StairRestColsMax + 1)
                    : WorldGen.genRand.Next(KiyumeMetrics.StairSegColsMin, KiyumeMetrics.StairSegColsMax + 1);
                if (x + w > KiyumeMetrics.SummitL) {
                    w = KiyumeMetrics.SummitL - x;
                }

                //级面行：向基准曲线收敛，级差 ≤StairDropMax 硬约束，只升不降；歇脚平台级差 0
                int prev = level;
                if (!rest) {
                    int ideal = (int)Math.Round(KiyumeMetrics.BaseFloorAt(x + w));
                    level = Math.Min(level, Math.Max(ideal, level - KiyumeMetrics.StairDropMax));
                }
                StampStep(x, w, level, prev);

                if (rest) {
                    stepsToRest = WorldGen.genRand.Next(KiyumeMetrics.StairRestStepsMin, KiyumeMetrics.StairRestStepsMax + 1);
                    //歇脚平台隔级立石灯
                    if (restIdx++ % 2 == 0) {
                        StoneLantern(x + w / 2, level - 1);
                    }
                }
                else {
                    stepsToRest--;
                }
                x += w;
            }

            //山顶平台：行 ~310（基准曲线现值），永远在雾线高潮 402 之上（雾上回望成立）
            int summitRow = (int)Math.Round(KiyumeMetrics.BaseFloorAt((KiyumeMetrics.SummitL + KiyumeMetrics.SummitR) / 2));
            if (level - summitRow > KiyumeMetrics.StairDropMax) {
                summitRow = level - KiyumeMetrics.StairDropMax;
            }
            summitRow = Math.Min(summitRow, level);
            StampStep(KiyumeMetrics.SummitL, KiyumeMetrics.SummitR - KiyumeMetrics.SummitL, summitRow, level);

            //山顶祠居中，龛底行沉进平台面
            int shrineL = (KiyumeMetrics.SummitL + KiyumeMetrics.SummitR) / 2 - Summit.Width / 2;
            int shrineTop = summitRow - Summit.Height + 1;
            Summit.StampGeometry(shrineL, shrineTop, StoneTile, NicheWall, KiyumeMetrics.PlatformFrameY);
            Summit.PlaceSlots(shrineL, shrineTop);
            KiyumeStructures.Shrines.Add(Summit.Area(shrineL, shrineTop));

            //石阶大段整体登记禁区（面层带 + 山顶祠上空）
            KiyumeStructures.ScatterExclusions.Add(new Rectangle(
                KiyumeMetrics.StairStartX, summitRow - Summit.Height - 2,
                KiyumeMetrics.SummitR - KiyumeMetrics.StairStartX,
                startLevel - summitRow + Summit.Height + 5));
        }

        //一级台面：削垫回写 + StoneSlab 面层 + 西缘升级斜切收口（Z3 惯例：低柱顶上一格 SlopeDownLeft）
        private static void StampStep(int x, int w, int level, int prevLevel) {
            FlattenSpan(x, x + w, level);
            for (int c = x; c < x + w; c++) {
                KiyumeTileBrush.SetSolid(c, level, SlabTile);
            }
            int rise = prevLevel - level;
            if (rise >= 1 && rise <= KiyumeMetrics.StairDropMax && !Main.tile[x - 1, prevLevel - 1].HasTile) {
                KiyumeTileBrush.SetSloped(x - 1, prevLevel - 1, SlabTile, SlopeType.SlopeDownLeft);
            }
        }

        //════════ 公用小工 ════════

        //石灯：GrayBrick 柱 2 高 + 平台帽 + 帽上蜡烛；standRow = 灯柱脚所在的空行
        //烛台只认 tileTable 锚，直接放实心柱顶会被收尾 CheckOnTable1x1 杀成暗柱（W4 实锤，E 包同款帽成规）
        private static void StoneLantern(int x, int standRow) {
            KiyumeTileBrush.SetSolid(x, standRow, StoneTile);
            KiyumeTileBrush.SetSolid(x, standRow - 1, StoneTile);
            KiyumeTileBrush.SetPlatform(x, standRow - 2, KiyumeMetrics.PlatformFrameY);
            KiyumeTileBrush.TryPlaceTile(x, standRow - 3, TileID.Candles, 0);
        }

        //足印上方空域内已有实心（民居/别包结构）即视为占用
        private static bool SpanOccupied(int left, int right, int probeUpRows) {
            for (int x = left; x < right; x++) {
                int top = KiyumePlans.FloorTopAt(x);
                for (int y = top - probeUpRows; y < top; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && Main.tileSolid[t.TileType]) {
                        return true;
                    }
                }
            }
            return false;
        }

        //区间内最高地面行（镜像 KiyumeVillage 手法）
        private static int HighestGround(int left, int right) {
            int best = int.MaxValue;
            for (int x = left; x < right; x++) {
                best = Math.Min(best, KiyumePlans.FloorTopAt(x));
            }
            return best == int.MaxValue ? (int)KiyumeMetrics.BaseFloorAt(left) : best;
        }

        //削高垫低到同一行并回写 FloorTop（Flatten 同款双动作）；垫料按带表取地砖
        private static void FlattenSpan(int left, int right, int row) {
            int[] top = KiyumePlans.FloorTop;
            for (int x = left; x < right; x++) {
                if (x < 0 || x >= Main.maxTilesX) {
                    continue;
                }
                int cur = KiyumePlans.FloorTopAt(x);
                if (cur > row) {
                    ushort fill = KiyumeMetrics.BandForColumn(x)?.GroundTile ?? TileID.Stone;
                    KiyumeTileBrush.FillRect(x, row, x + 1, cur, fill);
                }
                else if (cur < row) {
                    KiyumeTileBrush.CarveRect(x, cur, x + 1, row);
                }
                if (top != null && x < top.Length) {
                    top[x] = row;
                }
            }
        }

        /// <summary>FloorTop 定向回写（不动地形，柱脚石/台基顶等「结构即地面」的列用）</summary>
        private static void RewriteFloorTop(int left, int right, int row) {
            int[] top = KiyumePlans.FloorTop;
            if (top == null) {
                return;
            }
            for (int x = left; x < right; x++) {
                if (x >= 0 && x < top.Length) {
                    top[x] = row;
                }
            }
        }
    }
}
