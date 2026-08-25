using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones
{
    //====================================================================
    //L5 落灰场(骨灰沉积带,WAVE2-ENVIRONMENTS §5)。
    //
    //万骨窖中下带的一片"下过灰雪"的区:核心区 Slab96 全量换 Tiled97(过界即换怪,
    //F28 派系杠杆的招牌演示),地表行等价交换灰烬块+白漆压明度,半埋墓碑/骨灰瓮,
    //上缘垂两口灰口竖窖(井+沉降室,灰丘埋箱+天花粉砂淤层)。
    //
    //纪律:选址数据驱动(扫 ctx.Graph 房簇取最密质心,不硬编码坐标);
    //含裂纹粉砖(483)的房间整间跳过(L5"裂=危险"的 palette 级可读性,灰不许盖坑口预告);
    //既有区只做 wall/paint/表面等价交换/家具,零几何改动;新凿空间全走 infill 语法。
    //粉砂塌方在 NormalUpdates=false 下的运行时表现待游戏内核实,
    //淤层落位刻意避开箱与井(≥3 列),即便生成期帧修触发沉降也只落在灰丘上,不砸家具。
    //====================================================================
    internal static class AshfallStratum
    {
        //地带矩形:核心全换墙+两侧各 60 列参差过渡(数值草案:420=300+60x2)
        private const int RectWidth = 420;
        private const int RectHeight = 110;
        private const int TransitionCols = 60;
        //选址退化档(数值草案 §5.5-5:簇太稀先缩,仍不足弃建)
        private const int ShrunkWidth = 300;
        private const int ShrunkHeight = 90;
        //选址行窗:L5 地层3/4 基准行(计划 §5.2"行约 3556±8 与 3816±8",即带顶+800/+1060)
        private static readonly int[] StrataOffsets = [800, 1060];
        private const int StrataHalfWindow = 80;
        //过渡带块盐(与 L5Palette.MixWallVariants 的 0x2D6F 错开,斑形不撞)
        private const int WallSalt = 0x7A5C;

        internal static void PlanAndBuild(LayerBuildContext ctx, UnifiedRandom rand) {
            LayerBand band = ctx.Band;

            //===选址:两行窗各扫一遍,取最密房簇质心(确定性,零随机消耗)===
            Rectangle rect = PickSite(ctx, band, RectWidth, RectHeight, minRooms: 3);
            if (rect.IsEmpty) {
                rect = PickSite(ctx, band, ShrunkWidth, ShrunkHeight, minRooms: 2);
            }
            if (rect.IsEmpty) {
                CWRMod.Instance.Logger.Error(
                    "[AshfallStratum] 地层3/4行窗内房簇过稀,落灰场弃建(不硬造),责任=选址窗口");
                return;
            }

            //===裂砖房排除:整间跳过,灰不许盖坑口预告===
            List<Rectangle> exclusions = CollectCrackedRooms(ctx, rect);

            //===墙面换派+灰壳+负空间脚印(全部 wall/paint/等价交换,零几何)===
            int walls = SwapWalls(rect, exclusions);
            int ash = AshFloor(rect, exclusions);
            int prints = Footprints(rect, rand);

            //===陈设:零件级三段撒布在本 pass 内做(带级 ctx.Scatter 会被命中率饿死)===
            int tombs = ScatterPieces(rect, exclusions, rand, rand.Next(6, 11), 12,
                (x, y) => PlaceHalfBuriedTombstone(x, y, rand));
            int urns = ScatterPieces(rect, exclusions, rand, rand.Next(10, 17), 8,
                (x, y) => L5Palette.PlaceUrn(x, y, rand));

            //===灰口竖窖 2 簇(新凿,infill 语法:锚点房→探空→预留→刻画→落口)===
            int chutes = 0, silts = 0, chests = 0;
            for (int half = 0; half < 2; half++) {
                int xLo = half == 0 ? rect.Left : rect.Left + rect.Width / 2;
                int xHi = half == 0 ? rect.Left + rect.Width / 2 : rect.Right;
                if (BuildChute(ctx, rand, rect, xLo, xHi, ref silts, ref chests)) {
                    chutes++;
                }
            }
            if (chutes == 0) {
                CWRMod.Instance.Logger.Warn("[AshfallStratum] 两簇灰口竖窖均未落位,主地带保留(仅失埋藏窖)");
            }

            ZoneRegistry.Register(ZoneKind.AshfallStratum, rect);
            CWRMod.Instance.Logger.Info(
                $"[AshfallStratum] 落灰场落成 rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})"
                + $" 换墙{walls} 灰壳{ash} 脚印带{prints} 墓碑{tombs} 瓮{urns}"
                + $" 竖窖{chutes} 淤层{silts} 埋箱{chests}");
        }

        //==================== 选址 ====================

        //两行窗内扫房簇:滑动 x 窗计数,最密者胜;质心=簇内房心均值
        private static Rectangle PickSite(LayerBuildContext ctx, LayerBand band,
            int width, int height, int minRooms) {
            int bestCount = 0, bestRow = 0;
            long bestCentroid = 0;
            foreach (int offset in StrataOffsets) {
                int rowC = band.Top + offset;
                var centers = new List<int>();
                foreach (RoomNode room in ctx.Graph.Rooms) {
                    if (room.Bounds.Bottom < rowC - StrataHalfWindow
                        || room.Bounds.Top > rowC + StrataHalfWindow) {
                        continue;
                    }
                    centers.Add(room.Bounds.Left + room.Bounds.Width / 2);
                }
                if (centers.Count < minRooms) {
                    continue;
                }
                centers.Sort();
                for (int xLo = DungeonworldMetrics.PlayLeft + 10;
                    xLo + width <= DungeonworldMetrics.PlayRight - 10; xLo += 20) {
                    int count = 0;
                    long sum = 0;
                    foreach (int cx in centers) {
                        if (cx >= xLo && cx < xLo + width) {
                            count++;
                            sum += cx;
                        }
                    }
                    if (count > bestCount) {
                        bestCount = count;
                        bestRow = rowC;
                        bestCentroid = sum / count;
                    }
                }
            }
            if (bestCount < minRooms) {
                return Rectangle.Empty;
            }
            int left = (int)bestCentroid - width / 2;
            left = System.Math.Clamp(left, DungeonworldMetrics.PlayLeft + 6,
                DungeonworldMetrics.PlayRight - 6 - width);
            int top = System.Math.Clamp(bestRow - height / 2, band.Top + 20,
                band.SpineInteriorTop - 8 - height);
            return new Rectangle(left, top, width, height);
        }

        //矩形内含裂纹粉砖(483)的房间:整间(含壳外扩1)进排除表
        private static List<Rectangle> CollectCrackedRooms(LayerBuildContext ctx, Rectangle rect) {
            var exclusions = new List<Rectangle>();
            foreach (RoomNode room in ctx.Graph.Rooms) {
                if (!room.Bounds.Intersects(rect)) {
                    continue;
                }
                bool cracked = false;
                for (int x = room.Bounds.Left; x < room.Bounds.Right && !cracked; x++) {
                    for (int y = room.Bounds.Top; y < room.Bounds.Bottom && !cracked; y++) {
                        if (!WorldGen.InWorld(x, y, 5)) {
                            continue;
                        }
                        Tile t = Main.tile[x, y];
                        cracked = t.HasTile && t.TileType == L5Palette.CrackedBrick;
                    }
                }
                if (cracked) {
                    exclusions.Add(new Rectangle(room.Bounds.X - 1, room.Bounds.Y - 1,
                        room.Bounds.Width + 2, room.Bounds.Height + 2));
                }
            }
            return exclusions;
        }

        private static bool InExclusion(List<Rectangle> exclusions, int x, int y) {
            foreach (Rectangle rect in exclusions) {
                if (rect.Contains(x, y)) {
                    return true;
                }
            }
            return false;
        }

        //==================== 墙面换派 / 灰壳 / 脚印 ====================

        //核心区全量 Slab→Tiled(补丁式混斑会造成派系闪烁,全量才有"过界即换怪");
        //两侧 60 列覆盖率线性衰减+块散列啃边,出参差过渡而不是直线断口
        private static int SwapWalls(Rectangle rect, List<Rectangle> exclusions) {
            int swapped = 0;
            for (int x = rect.Left; x < rect.Right; x++) {
                int edge = System.Math.Min(x - rect.Left, rect.Right - 1 - x);
                int coverage = edge >= TransitionCols ? 100 : 8 + (85 - 8) * edge / TransitionCols;
                for (int y = rect.Top; y < rect.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (t.HasTile || t.WallType != L5Palette.WallSlab
                        || InExclusion(exclusions, x, y)) {
                        continue;
                    }
                    if (coverage < 100 && !LayerTint.BlockPatch(x, y, coverage, WallSalt)) {
                        continue;
                    }
                    t.WallType = L5Palette.WallTiled;
                    swapped++;
                }
            }
            return swapped;
        }

        //灰壳:暴露地表行(实心粉砖/骨块且上格无物)等价交换灰烬块57+白漆,
        //裂砖483构造性不碰(类型过滤),碰撞与净高零变化(F31 同款语法)
        private static int AshFloor(Rectangle rect, List<Rectangle> exclusions) {
            int count = 0;
            for (int x = rect.Left; x < rect.Right; x++) {
                for (int y = rect.Top + 1; y < rect.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (!t.HasTile || (t.TileType != L5Palette.Brick && t.TileType != L5Palette.Bone)) {
                        continue;
                    }
                    if (Main.tile[x, y - 1].HasTile || InExclusion(exclusions, x, y)) {
                        continue;
                    }
                    ZoneWorks.SwapSolidType(x, y, TileID.Ash);
                    WorldGen.paintTile(x, y, PaintID.WhitePaint);
                    count++;
                }
            }
            return count;
        }

        //脚印带:沿灰面抹掉白漆成裸灰点列(负空间做旧,踩碎灰壳的读法)
        private static int Footprints(Rectangle rect, UnifiedRandom rand) {
            int lines = 0;
            for (int attempt = 0; attempt < 24 && lines < 3; attempt++) {
                int x = rand.Next(rect.Left + 20, rect.Right - 40);
                int y = FindAshSurface(x, rect.Top, rect.Bottom);
                if (y < 0) {
                    continue;
                }
                int steps = rand.Next(6, 13);
                int px = x;
                for (int s = 0; s < steps && px < rect.Right - 2; s++) {
                    int sy = FindAshSurface(px, System.Math.Max(rect.Top, y - 4),
                        System.Math.Min(rect.Bottom, y + 5));
                    if (sy < 0) {
                        break;
                    }
                    Tile step = Main.tile[px, sy];
                    step.TileColor = PaintID.None;
                    y = sy;
                    px += rand.Next(2, 4);
                }
                lines++;
            }
            return lines;
        }

        //列内自上而下找第一个"上空下灰"的地表灰格
        private static int FindAshSurface(int x, int yFrom, int yTo) {
            for (int y = yFrom + 1; y < yTo; y++) {
                Tile t = Main.tile[x, y];
                if (t.HasTile && t.TileType == TileID.Ash && !Main.tile[x, y - 1].HasTile) {
                    return y;
                }
            }
            return -1;
        }

        //==================== 陈设撒布(零件级三段:撒点→验证→保底退出,F30) ====================

        private static int ScatterPieces(Rectangle rect, List<Rectangle> exclusions,
            UnifiedRandom rand, int target, int dedupe, System.Func<int, int, bool> place) {
            var placedPts = new List<Point>();
            int placed = 0, attempts = 0;
            while (placed < target && attempts < target * 12) {
                attempts++;
                int x = rand.Next(rect.Left + 4, rect.Right - 4);
                int y = rand.Next(rect.Top + 4, rect.Bottom - 4);
                Tile below = Main.tile[x, y + 1];
                if (Main.tile[x, y].HasTile || !below.HasTile || below.TileType != TileID.Ash) {
                    continue;
                }
                if (InExclusion(exclusions, x, y) || TooClose(placedPts, x, y, dedupe)) {
                    continue;
                }
                if (!place(x, y)) {
                    continue;
                }
                placedPts.Add(new Point(x, y));
                placed++;
            }
            return placed;
        }

        //墓碑落成后两翼各拢一格灰:只露上半截的"半埋"读法
        private static bool PlaceHalfBuriedTombstone(int x, int standRow, UnifiedRandom rand) {
            if (!L5Palette.PlaceTombstone(x, standRow, rand)) {
                return false;
            }
            BuryFlank(x - 1, standRow);
            BuryFlank(x + 2, standRow);
            return true;
        }

        private static void BuryFlank(int x, int standRow) {
            if (WorldGen.InWorld(x, standRow, 5) && !Main.tile[x, standRow].HasTile
                && Main.tile[x, standRow + 1].HasTile) {
                TileBrush.SetSolid(x, standRow, TileID.Ash);
                WorldGen.paintTile(x, standRow, PaintID.WhitePaint);
            }
        }

        private static bool TooClose(List<Point> pts, int x, int y, int dist) {
            foreach (Point p in pts) {
                if (System.Math.Abs(p.X - x) < dist && System.Math.Abs(p.Y - y) < dist) {
                    return true;
                }
            }
            return false;
        }

        //==================== 灰口竖窖(井+沉降室) ====================

        private static bool BuildChute(LayerBuildContext ctx, UnifiedRandom rand,
            Rectangle rect, int xLo, int xHi, ref int silts, ref int chests) {
            //锚点:矩形上缘上方 0~260 行内、横向落在半窗内的最低既有房(离灰场最近)
            RoomNode host = null;
            foreach (RoomNode room in ctx.Graph.Rooms) {
                int cx = room.Bounds.Left + room.Bounds.Width / 2;
                if (cx < xLo + 8 || cx >= xHi - 8
                    || room.Bounds.Bottom > rect.Top + 6 || room.Bounds.Bottom < rect.Top - 260
                    || room.InteriorRight - room.InteriorLeft < 10
                    || ZoneWorks.HoldsLiquid(room)) {
                    continue;
                }
                if (host == null || room.Bounds.Bottom > host.Bounds.Bottom) {
                    host = room;
                }
            }
            if (host == null) {
                CWRMod.Instance.Logger.Warn($"[AshfallStratum] 半窗[{xLo},{xHi})无可用竖窖锚点房,该簇缺席");
                return false;
            }

            //落口列先规划(带家具避让),井柱贴身探空(镜像 IntersticePlanner.TryProbeGap)
            int shaftX = ZoneWorks.PlanHostFloorGap(host, host.Bounds.Left + host.Bounds.Width / 2 - 1);
            if (shaftX < 0) {
                CWRMod.Instance.Logger.Warn($"[AshfallStratum] 锚点房{host.Bounds}地板无处开落口,该簇缺席");
                return false;
            }
            int probeTop = host.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
            int chamberH = rand.Next(10, 13);
            int chamberW = rand.Next(26, 41);
            List<(int top, int bottom)> gaps = ctx.Grid.FreeGaps(shaftX - 1, 5,
                probeTop, System.Math.Min(rect.Top + 60, ctx.Band.SpineInteriorTop - 6), chamberH + 16);
            if (gaps.Count == 0 || gaps[0].top > probeTop + 4) {
                CWRMod.Instance.Logger.Warn($"[AshfallStratum] 锚点房{host.Bounds}下方无贴身空档,该簇缺席");
                return false;
            }
            (int gapTop, int gapBottom) = gaps[0];

            int shaftLen = rand.Next(8, 19);
            int chamberTop = System.Math.Min(gapTop + shaftLen, gapBottom - chamberH - 4);
            //沉降室居中于井,横向钳回带内膛
            int chamberL = shaftX + 1 - (chamberW + 4) / 2;
            chamberL = System.Math.Clamp(chamberL, DungeonworldMetrics.PlayLeft + 4,
                DungeonworldMetrics.PlayRight - 4 - (chamberW + 4));
            var chamberBox = new Rectangle(chamberL, chamberTop, chamberW + 4, chamberH + 4);
            //井必须落进室内膛(钳制后可能偏出:偏出即弃,不硬修)
            if (shaftX - 1 < chamberBox.Left + 2 || shaftX + 4 > chamberBox.Right - 2) {
                CWRMod.Instance.Logger.Warn("[AshfallStratum] 竖窖井列偏出沉降室内膛,该簇缺席");
                return false;
            }
            if (!ctx.Grid.TryReserve(chamberBox, 0)) {
                CWRMod.Instance.Logger.Warn($"[AshfallStratum] 沉降室足印被占{chamberBox},该簇缺席");
                return false;
            }
            //井柱落账(镜像 AnnexPlanner.DropShaft:不落账后来的探空会把井当实心壳)
            ctx.Grid.MarkUnchecked(new Rectangle(shaftX - 1, probeTop, 5, chamberTop + 2 - probeTop));

            //===刻画:落口→之字井→沉降室内膛===
            ZoneWorks.OpenHostFloorGap(host, shaftX, L5Palette.PlatformBone, L5Palette.WallTiled);
            int intL = chamberBox.Left + 2;
            int intR = chamberBox.Right - 2;
            int intT = chamberBox.Top + 2;
            int floorTop = chamberBox.Bottom - 2;
            CorridorRouter.CarveStairWell(shaftX, host.Bounds.Bottom, intT,
                L5Palette.PlatformBone, L5Palette.WallTiled);
            TileBrush.CarveRect(intL, intT, intR, floorTop, L5Palette.WallTiled);

            //井壁灰烬贴皮(等价交换,壁厚不变)
            for (int y = host.Bounds.Bottom + 2; y < intT; y++) {
                SwapIfBrick(shaftX - 1, y);
                SwapIfBrick(shaftX + 3, y);
            }
            //室地板整行换灰
            for (int x = intL; x < intR; x++) {
                ZoneWorks.SwapSolidType(x, floorTop, TileID.Ash);
                WorldGen.paintTile(x, floorTop, PaintID.WhitePaint);
            }

            //===灰丘(阶梯收分,|Δ|≤1)+埋藏箱(箱顶露出,四周灰围拢)===
            int chestX = System.Math.Clamp(intL + chamberW / 2 + rand.Next(-5, 6), intL + 2, intR - 4);
            if (chestX + 2 > shaftX - 2 && chestX < shaftX + 5) {
                chestX = shaftX + 6 <= intR - 4 ? shaftX + 6 : shaftX - 8;
                chestX = System.Math.Clamp(chestX, intL + 2, intR - 4);
            }
            int cur = rand.Next(0, 2);
            var moundH = new int[intR - intL];
            for (int i = 0; i < moundH.Length; i++) {
                cur = System.Math.Clamp(cur + rand.Next(-1, 2), 0, 3);
                int x = intL + i;
                //箱位与井口正下不堆丘:箱要"刨出来",井口要能落人
                if ((x >= chestX - 1 && x < chestX + 3) || (x >= shaftX - 1 && x < shaftX + 4)) {
                    moundH[i] = 0;
                    continue;
                }
                moundH[i] = cur;
                for (int k = 0; k < cur; k++) {
                    TileBrush.SetSolid(x, floorTop - 1 - k, TileID.Ash);
                    WorldGen.paintTile(x, floorTop - 1 - k, PaintID.WhitePaint);
                }
            }
            bool chestOk = WorldGen.PlaceChest(chestX, floorTop - 1, TileID.Containers,
                notNearOtherChests: false, L5Palette.ChestLockedGold) >= 0;
            if (chestOk) {
                chests++;
                //两翼拢灰到箱下半:箱顶露出的"半埋"读法
                BuryFlank(chestX - 1, floorTop - 1);
                BuryFlank(chestX + 2, floorTop - 1);
            }
            else {
                CWRMod.Instance.Logger.Warn($"[AshfallStratum] 埋藏箱放置失败 at ({chestX},{floorTop - 1})");
            }

            //===天花粉砂淤层 4~6 处(3x2 嵌进自建天花)+漏灰预告===
            //避井避箱各留 3 列:即便生成期帧修触发沉降,砂也只落在灰丘上,不砸家具
            int siltWant = rand.Next(4, 7);
            int placedSilt = 0;
            for (int attempt = 0; attempt < siltWant * 6 && placedSilt < siltWant; attempt++) {
                int px = rand.Next(intL + 2, intR - 5);
                if ((px + 3 > shaftX - 3 && px < shaftX + 6)
                    || (px + 3 > chestX - 3 && px < chestX + 5)) {
                    continue;
                }
                bool clear = true;
                for (int i = -2; i < 5 && clear; i++) {
                    Tile t = Main.tile[px + i, chamberBox.Top];
                    clear = !t.HasTile || t.TileType != TileID.Silt;
                }
                if (!clear) {
                    continue;
                }
                for (int i = 0; i < 3; i++) {
                    ZoneWorks.SwapSolidType(px + i, chamberBox.Top, TileID.Silt);
                    ZoneWorks.SwapSolidType(px + i, chamberBox.Top + 1, TileID.Silt);
                }
                //淤层正下垂灰痕:玩家读"这条缝在漏灰"
                WorldGen.paintWall(px + 1, intT, PaintID.GrayPaint);
                WorldGen.paintWall(px + 1, intT + 1, PaintID.GrayPaint);
                placedSilt++;
            }
            silts += placedSilt;

            //===绷直吊链降到丘面(锚顶=井内最下一块横档正下,锚底=灰丘,承重悬链合规)+丘面杂物===
            L5Palette.TautChain(shaftX + 1, intT - 3, chamberH + 10);
            for (int i = 0; i < 3; i++) {
                int ux = rand.Next(intL + 1, intR - 1);
                if ((ux >= chestX - 1 && ux < chestX + 3) || (ux >= shaftX - 1 && ux < shaftX + 4)) {
                    continue;
                }
                int standRow = floorTop - 1 - moundH[ux - intL];
                if (rand.NextBool(2)) {
                    L5Palette.PlaceUrn(ux, standRow, rand);
                }
                else {
                    L5Palette.PlaceSmallBones(ux, standRow, rand);
                }
            }

            //===入图:P80 nodes 计数与洪泛断言认得它===
            var node = new RoomNode { Bounds = chamberBox };
            int hostIdx = ZoneWorks.IndexOf(ctx.Graph, host);
            int nodeIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(node);
            ctx.Graph.Edges.Add(new RoomEdge(hostIdx, nodeIdx,
                SocketKind.PlatformGap, EdgeForm.StairWell));
            return true;
        }

        private static void SwapIfBrick(int x, int y) {
            if (!WorldGen.InWorld(x, y, 5)) {
                return;
            }
            Tile t = Main.tile[x, y];
            if (t.HasTile && (t.TileType == L5Palette.Brick || t.TileType == L5Palette.Bone)) {
                ZoneWorks.SwapSolidType(x, y, TileID.Ash);
            }
        }
    }
}
