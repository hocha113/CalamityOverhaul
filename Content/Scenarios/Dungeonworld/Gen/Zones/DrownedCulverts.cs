using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones
{
    //====================================================================
    //L4 沉没暗渠带(Drowned Culverts,WAVE2-ENVIRONMENTS §4)。
    //
    //干房地板的检修口→跳水井→入水盆→整段全淹的黑暗横渠:氙苔当灯、
    //气钟龛换气、段间堰锁隔断、死端沉没圣物龛(潜水钟式气室)与水下箱。
    //
    //水体纪律(全部复用 L4WaterWorks 公开 API):每条暗渠登记为若干舱段,
    //HighSurfaceRow==LowSurfaceRow==盆水面行——水门今后无论开关、
    //ApplyStateRuntime 重写多少次,暗渠水位恒定;气钟/井上段靠"开口只在水面上
    //或与同一水体连续"的堰坎公理构造密封。填水后跑一次限带 settle 作构造
    //bug 保险,再以 AssertBandWater 全带断言收口(时序:L4Content 的水在 P50
    //已静定,本 pass 的水写在其后,互不污染)。
    //
    //===箱段纵剖(BT=箱顶行,箱高 18)===
    //  [BT   ,BT+3 ) 顶壳(圣物龛顶盖 ≥2)
    //  [BT+3 ,BT+7 ) 圣物龛内膛(死端上方气室,仅龛端)
    //  [BT+4 ,BT+7 ) 入水盆气段(仅盆列);S=BT+7=水面行
    //  [BT+7 ,BT+9 ) 渠顶板(气钟龛嵌此两行,AirPockets 豁免)
    //  [BT+9 ,BT+14) 全淹横渠(净高 5)
    //  [BT+14,BT+16) 渠底板;盆列水体下探至 BT+16
    //  [BT+16,BT+18) 盆底板
    //====================================================================
    internal static class DrownedCulverts
    {
        private const int BoxRows = 18;
        //相对箱顶的行锚(见纵剖)
        private const int SurfaceRow = 7;
        private const int CanalTopRow = 9;
        private const int CanalNet = 5;
        private const int MinBeltCols = 120;
        private const int MaxBeltCols = 500;
        private const int BasinHalfWidth = 7;
        private const int ShrineCols = 8;

        internal static void PlanAndBuild(LayerBuildContext ctx, UnifiedRandom rand) {
            LayerBand band = ctx.Band;
            int bottomLimit = band.SpineInteriorTop - 4;
            int built = 0;
            var beltRects = new List<Rectangle>();
            //宿主快照(镜像 IntersticePlanner):只认既有房,本器自己新增的暗渠节点
            //不再当宿主——否则暗渠1的井会把暗渠0的盆底板当"地板"凿穿,settle 即漏
            var hostPool = new List<RoomNode>(ctx.Graph.Rooms);

            //中部/下部各一条:行窗按带高比例切分,数据全由 FreeSpans 现场探,不硬编码组坐标
            int midSplit = band.Top + (band.Bottom - band.Top) * 62 / 100;
            (int lo, int hi)[] windows = [
                (band.Top + 300, midSplit),
                (midSplit, bottomLimit - BoxRows),
            ];
            for (int w = 0; w < windows.Length; w++) {
                if (BuildBelt(ctx, rand, windows[w].lo, windows[w].hi, w, beltRects, hostPool)) {
                    built++;
                }
                else {
                    CWRMod.Instance.Logger.Error(
                        $"[DrownedCulverts] 暗渠{w}(行窗{windows[w].lo}~{windows[w].hi})弃建,"
                        + "责任=死带碎片化或锚点缺席(降级:少一条带)");
                }
            }
            if (built == 0) {
                return;
            }

            //===水体收口:一次重写全部舱段→限带 settle 保险→全带静定断言===
            int wet = L4WaterWorks.FillState(high: true);
            int top = int.MaxValue, bottom = int.MinValue;
            foreach (Rectangle rect in beltRects) {
                top = System.Math.Min(top, rect.Top);
                bottom = System.Math.Max(bottom, rect.Bottom);
            }
            var settleBand = new LayerBand("暗渠settle", top - 2, bottom - top + 4,
                L4Palette.Brick, L4Palette.WallSlab);
            L4WaterWorks.SettleBand(settleBand);
            int asserted = L4WaterWorks.AssertBandWater(band);
            CWRMod.Instance.Logger.Info(
                $"[DrownedCulverts] 暗渠带落成 {built}/2 条 舱段总数={L4WaterWorks.Compartments.Count}"
                + $" 重写水格={wet} 全带断言水格={asserted}");
        }

        //==================== 单条暗渠 ====================

        private static bool BuildBelt(LayerBuildContext ctx, UnifiedRandom rand,
            int rowLo, int rowHi, int beltIndex, List<Rectangle> beltRects, List<RoomNode> hostPool) {
            if (rowHi - rowLo < BoxRows + 4) {
                return false;
            }
            //1) 行窗内探最长空闲横段(避主竖井列带,碎段按 120 列门槛丢弃)
            if (!FindBeltBox(ctx, rowLo, rowHi, out Rectangle box)) {
                return false;
            }
            //2) 入口锚点:箱上方干房,井列过家具避让+全程列空检;
            //井列硬界=入水盆必须整只落进渠列区间(井不在盆上=井底闷在渠顶板里,死井)
            RoomNode entryHost = PickHost(ctx, hostPool, box.Left + 22, box.Right - 22, box.Top,
                exclude: null, box.Left + 10, box.Right - 12, out int wellX);
            if (entryHost == null) {
                return false;
            }
            //3) 足印落账:箱体+入口井柱(先规划后刻画)
            if (!ctx.Grid.TryReserve(box, 0)) {
                return false;
            }
            int entryProbeTop = entryHost.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
            ctx.Grid.MarkUnchecked(new Rectangle(wellX - 1, entryProbeTop, 5, box.Top - entryProbeTop));

            int bt = box.Top;
            int surface = bt + SurfaceRow;
            int canalTop = bt + CanalTopRow;
            int canalL = box.Left + 2;
            int canalR = box.Right - 2;
            int basinL = System.Math.Clamp(wellX + 1 - BasinHalfWidth, canalL + 2, canalR - 2 - BasinHalfWidth * 2);
            int basinR = basinL + BasinHalfWidth * 2;
            bool shrineOnLeft = wellX - canalL > canalR - wellX;
            int shrineL = shrineOnLeft ? canalL : canalR - ShrineCols;
            int chestX = shrineOnLeft ? canalR - 4 : canalL + 2;

            //4) 副出口井 0~2(渠三分位上方另找干房,井底并进渠顶;避圣物龛与既有井)
            var exitWells = new List<(RoomNode host, int x)>();
            for (int i = 0; i < 2; i++) {
                int cx = box.Left + box.Width * (i + 1) / 3;
                RoomNode host = PickHost(ctx, hostPool, cx - 70, cx + 70, bt, entryHost,
                    canalL + 4, canalR - 7, out int ex);
                if (host == null || System.Math.Abs(ex - wellX) < 14 || NearAny(exitWells, ex, 14)
                    || ex + 3 > shrineL - 2 && ex < shrineL + ShrineCols + 2) {
                    continue;
                }
                int probeTop = host.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
                ctx.Grid.MarkUnchecked(new Rectangle(ex - 1, probeTop, 5, bt - probeTop));
                exitWells.Add((host, ex));
            }

            //===5) 刻画(全部 TileBrush 受约束写入,壳厚由箱段纵剖构造保证)===
            //全淹横渠(净高5)
            TileBrush.CarveRect(canalL, canalTop, canalR, canalTop + CanalNet, L4Palette.WallSlab);
            //入水盆:气段+加深水柱
            TileBrush.CarveRect(basinL, bt + 4, basinR, bt + 16, L4Palette.WallSlab);
            //跳水井:宿主地板落口→之字井直落盆气段,水面上加一块歇脚台(出水一跳可及)
            ZoneWorks.OpenHostFloorGap(entryHost, wellX, L4Palette.PlatformFrameY, L4Palette.WallSlab);
            CorridorRouter.CarveStairWell(wellX, entryHost.Bounds.Bottom, bt + 5,
                L4Palette.PlatformFrameY, L4Palette.WallSlab);
            TileBrush.PlatformRow(wellX, wellX + 3, bt + 5, L4Palette.PlatformFrameY);
            //副出口井:井底并进渠内膛顶部,井内水柱与渠同水体(舱段另行登记)
            foreach ((RoomNode host, int ex) in exitWells) {
                ZoneWorks.OpenHostFloorGap(host, ex, L4Palette.PlatformFrameY, L4Palette.WallSlab);
                CorridorRouter.CarveStairWell(ex, host.Bounds.Bottom, canalTop + 3,
                    L4Palette.PlatformFrameY, L4Palette.WallSlab);
            }

            //堰锁隔断:每 40~90 列一道 2 厚,底部留 3x3 潜行口(同水面故水体连续,合法)
            var bulkheads = new List<int>();
            int cursor = canalL + rand.Next(40, 91);
            while (cursor < canalR - 44) {
                if (cursor > basinL - 4 && cursor < basinR + 4
                    || cursor > shrineL - 4 && cursor < shrineL + ShrineCols + 4
                    || NearWell(exitWells, wellX, cursor, 6)) {
                    cursor += 12;
                    continue;
                }
                for (int i = 0; i < 2; i++) {
                    for (int y = canalTop; y < canalTop + 2; y++) {
                        TileBrush.SetSolid(cursor + i, y, L4Palette.Brick);
                    }
                }
                bulkheads.Add(cursor);
                cursor += rand.Next(40, 91);
            }

            //圣物龛:死端上方的潜水钟气室(入口只朝下开,水面下气室靠 AirPockets 豁免+
            //"水不上行"的静定几何,settle 也灌不进来)
            int shrineGapL = shrineL + ShrineCols / 2 - 1;
            TileBrush.CarveRect(shrineL, bt + 3, shrineL + ShrineCols, bt + 7, L4Palette.WallTiled);
            TileBrush.CarveRect(shrineGapL, bt + 7, shrineGapL + 3, bt + 9, L4Palette.WallTiled);
            int shrineStand = bt + 6;
            WorldGen.PlacePot(shrineL + 1, shrineStand, TileID.Pots,
                rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax));
            WorldGen.PlacePot(shrineL + ShrineCols - 2, shrineStand, TileID.Pots,
                rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax));
            if (!L4Palette.TryPlaceTile(shrineL + ShrineCols / 2 + 1, shrineStand,
                TileID.Candelabras, L4Palette.CandelabraStyle)) {
                //烛台落点(+5,+6)横跨龛口凿空列(+3~+5)与右瓮(+6),当前布局必失败——
                //fail loud 记账(放置助手契约),布局修正待协调者裁决(二审 R-B 报告)
                CWRMod.Instance.Logger.Warn(
                    $"[DrownedCulverts] 圣物龛烛台放置失败 at ({shrineL + ShrineCols / 2 + 1},{shrineStand}),龛口/瓮占位冲突");
            }

            //气钟龛:渠顶板嵌 3x2,每 25~40 列一口(井列/堰锁/盆/龛让位)
            var bells = new List<Rectangle>();
            int bellCursor = canalL + rand.Next(12, 26);
            while (bellCursor < canalR - 5) {
                if (!(bellCursor > basinL - 5 && bellCursor < basinR + 2)
                    && !(bellCursor > shrineL - 5 && bellCursor < shrineL + ShrineCols + 2)
                    && !NearBulkhead(bulkheads, bellCursor, 5)
                    && !NearWell(exitWells, wellX, bellCursor, 6)) {
                    var bell = new Rectangle(bellCursor, bt + SurfaceRow, 3, 2);
                    TileBrush.CarveRect(bell.Left, bell.Top, bell.Right, bell.Bottom, L4Palette.WallSlab);
                    bells.Add(bell);
                }
                bellCursor += rand.Next(25, 41);
            }

            //苔光段:渠底/渠顶板面等价交换氙苔(冷青自发光;随机更新停摆=苔不蔓延,
            //长哪算哪),每 10~20 列一段 3~6 长,渠壳绿砖大盘保群系计数
            int mossCells = 0;
            for (int x = canalL + rand.Next(4, 10); x < canalR - 6; x += rand.Next(10, 21)) {
                int len = rand.Next(3, 7);
                bool ceiling = rand.NextBool(3);
                for (int i = 0; i < len && x + i < canalR - 2; i++) {
                    int y = ceiling ? canalTop - 1 : canalTop + CanalNet;
                    if (!WorldGen.InWorld(x + i, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x + i, y];
                    if (t.HasTile && t.TileType == L4Palette.Brick) {
                        ZoneWorks.SwapSolidType(x + i, y, TileID.XenonMoss);
                        mossCells++;
                    }
                }
            }

            //死端水下箱(水纹箱样式,层级对齐 L4 既有箱表)
            bool chestOk = WorldGen.PlaceChest(chestX, canalTop + CanalNet - 1, TileID.Containers,
                notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0;
            if (!chestOk) {
                CWRMod.Instance.Logger.Warn($"[DrownedCulverts] 死端箱放置失败 at ({chestX},{canalTop + CanalNet - 1})");
            }

            //盆壁水线痕(恒定水位的"这条线不会动"记号)
            L4Palette.PaintWaterlineRow(basinL, basinR, surface, L4Palette.HighLinePaint);

            //===6) 舱段登记(名字带段号;两态同水面行=阀门无关)===
            var cuts = new List<int> { canalL };
            cuts.AddRange(bulkheads);
            cuts.Add(canalR);
            int segIndex = 0;
            for (int i = 0; i + 1 < cuts.Count; i++) {
                int segL = cuts[i];
                int segR = System.Math.Min(cuts[i + 1] + 2, canalR);
                var comp = L4WaterWorks.Register($"暗渠{beltIndex}-段{segIndex++}",
                    new Rectangle(segL, surface, segR - segL, bt + 16 - surface), surface, surface);
                foreach (Rectangle bell in bells) {
                    if (bell.Left >= segL && bell.Left < segR) {
                        comp.AirPockets.Add(bell);
                    }
                }
            }
            //副出口井水柱无需另立舱段:井列落在渠列区间内,段矩形已覆盖其水面下格
            //(井底与渠连续=同一水体,水面同 S)

            //===7) 入图:整条暗渠一个名义节点,挂在入口锚点上===
            var beltNode = new RoomNode { Bounds = box };
            int hostIdx = ZoneWorks.IndexOf(ctx.Graph, entryHost);
            int nodeIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(beltNode);
            ctx.Graph.Edges.Add(new RoomEdge(hostIdx, nodeIdx, SocketKind.ShaftMouth, EdgeForm.StairWell));
            foreach ((RoomNode host, int _) in exitWells) {
                ctx.Graph.Edges.Add(new RoomEdge(ZoneWorks.IndexOf(ctx.Graph, host), nodeIdx,
                    SocketKind.ShaftMouth, EdgeForm.StairWell));
            }

            ZoneRegistry.Register(ZoneKind.DrownedCulvert, box);
            beltRects.Add(box);
            CWRMod.Instance.Logger.Info(
                $"[DrownedCulverts] 暗渠{beltIndex} box=({box.X},{box.Y},{box.Width}x{box.Height})"
                + $" 渠长{canalR - canalL} 堰锁{bulkheads.Count} 气钟{bells.Count}"
                + $" 副出口{exitWells.Count} 苔光{mossCells} 箱={(chestOk ? "成" : "拒")}");
            return true;
        }

        //==================== 探空与锚点 ====================

        //行窗内滑动探最长空闲横段;主竖井列带构造性跳过(镜像 L4Content.HitsShaft 语义)
        private static bool FindBeltBox(LayerBuildContext ctx, int rowLo, int rowHi, out Rectangle box) {
            box = Rectangle.Empty;
            int shaftL = DungeonworldMetrics.ShaftLeft - 6;
            int shaftR = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 6;
            int bestW = 0;
            Rectangle bestBox = Rectangle.Empty;
            void Consider(int l, int r, int top) {
                if (r - l >= MinBeltCols && r - l > bestW) {
                    bestW = r - l;
                    bestBox = new Rectangle(l, top, System.Math.Min(r - l, MaxBeltCols), BoxRows);
                }
            }
            for (int top = rowLo; top + BoxRows <= rowHi; top += 6) {
                List<(int left, int right)> spans = ctx.Grid.FreeSpans(top, BoxRows,
                    DungeonworldMetrics.PlayLeft + 8, DungeonworldMetrics.PlayRight - 8, MinBeltCols);
                foreach ((int left, int right) in spans) {
                    //跨主竖井的段裁成两半各自参选(镜像 L4Content.HitsShaft 的让位语义)
                    if (right > shaftL && left < shaftR) {
                        Consider(left, System.Math.Min(right, shaftL), top);
                        Consider(System.Math.Max(left, shaftR), right, top);
                    }
                    else {
                        Consider(left, right, top);
                    }
                }
            }
            box = bestBox;
            return bestW >= MinBeltCols;
        }

        //箱上方的干房锚点(只从宿主快照选):井列(带家具避让)全程空到箱顶、
        //且落进[wellLo,wellHi]才算数;取最低者(离渠最近,井最短)。返回井左列
        private static RoomNode PickHost(LayerBuildContext ctx, List<RoomNode> hostPool,
            int xLo, int xHi, int boxTop,
            RoomNode exclude, int wellLo, int wellHi, out int wellX) {
            wellX = -1;
            RoomNode best = null;
            int bestX = -1;
            foreach (RoomNode room in hostPool) {
                if (ReferenceEquals(room, exclude)) {
                    continue;
                }
                int cx = room.Bounds.Left + room.Bounds.Width / 2;
                if (cx < xLo || cx >= xHi
                    || room.Bounds.Bottom > boxTop - 6 || room.Bounds.Bottom < boxTop - 280
                    || room.InteriorRight - room.InteriorLeft < 10
                    || ZoneWorks.HoldsLiquid(room)) {
                    continue;
                }
                if (best != null && room.Bounds.Bottom <= best.Bounds.Bottom) {
                    continue;
                }
                int gx = ZoneWorks.PlanHostFloorGap(room, System.Math.Clamp(cx - 1, wellLo, wellHi));
                if (gx < wellLo || gx > wellHi) {
                    continue;
                }
                int probeTop = room.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
                if (!ctx.Grid.CanReserve(new Rectangle(gx - 1, probeTop, 5, boxTop - probeTop), 0)) {
                    continue;
                }
                best = room;
                bestX = gx;
            }
            wellX = bestX;
            return best;
        }

        private static bool NearBulkhead(List<int> bulkheads, int x, int dist) {
            foreach (int b in bulkheads) {
                if (System.Math.Abs(b - x) < dist) {
                    return true;
                }
            }
            return false;
        }

        private static bool NearWell(List<(RoomNode host, int x)> wells, int entryWellX, int x, int dist) {
            if (System.Math.Abs(entryWellX - x) < dist) {
                return true;
            }
            return NearAny(wells, x, dist);
        }

        private static bool NearAny(List<(RoomNode host, int x)> wells, int x, int dist) {
            foreach ((RoomNode _, int wx) in wells) {
                if (System.Math.Abs(wx - x) < dist) {
                    return true;
                }
            }
            return false;
        }
    }
}
