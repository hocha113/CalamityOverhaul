using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //====================================================================
    //L4水牢内容入口(Wave-2接缝契约,契约全文见 LayerBuildContext 头注释)。
    //管线路/父级一行接线(LayerContentPass的L4槽位):
    //  Layers.L4.L4Content.PlanAndBuild(LayerPlans.L4);
    //
    //===干→湿→井 五组(ROOMS-L4 §0,1000行大带)===
    //每组一条干层(阀室/泵房/堰闸,地板齐平)+一条共享水线的湿层(管廊/囚室/蓄水厅/深潜井)。
    //组间两条服务楼梯井(主竖井左右翼各一)归纳到下一组干层,最底组落层脊。
    //越深湿层越重:末组出现干涸舱段(L4→L5骨/水互斥预告)。
    //井站/忏悔室=跨层公共构件,本入口不做。
    //
    //===水体时机(本层自包含,管线现无P70)===
    //几何+家具冻结 → FillState(满水) → SettleBand → AssertBandWater → PaintAging。
    //撒布经ctx.Scatter声明,P55执行(沉链依赖此时已写入的液体)。
    //两态切换:生成期用ApplyState(带settle),运行期用ApplyStateRuntime(纯重写);
    //阀杆接线在Machines\DungeonworldWaterGate,联机由服务端裁决并回播区块。
    //随机全走WorldGen.genRand(F22);fail loud(纪律6)。
    //====================================================================
    internal static class L4Content
    {
        private const int GroupCount = 5;
        //干房地板相对组原点;水线=干地板+16(Y不重叠:干房底<湿房顶)
        private const int DryHang = 24;
        private const int WaterlineGap = 16;

        private enum NodeKind
        {
            Gallery, Valve, Reservoir, Sunken, Plunge,
            PumpMain, PumpSec, Gate, Splash,
        }

        private sealed class PlacedNode
        {
            internal RoomNode Room;
            internal NodeKind Kind;
            internal int GraphIndex;
            internal bool Drained;
            internal int Waterline;
        }

        private sealed class Group
        {
            internal int Index;
            internal int DryFloor;
            internal int Waterline;
            internal int NextFloor;          //下一组干地板,或层脊
            internal int ServiceLeft = -1;
            internal int ServiceRight = -1;
            internal readonly List<PlacedNode> Dry = [];
            internal readonly List<PlacedNode> Wet = [];
            internal readonly List<PlacedNode> DrainedGalleries = [];
        }

        private sealed class Caps
        {
            internal int Galleries, Valves, Reservoirs, Sunken, Plunges;
            internal int PumpMain, PumpSec, Gates, Splash;
        }

        /// <summary>层内容主入口:组规划→服务井预留→落房→链边/湿port→井网→注水settle→做旧→撒布声明</summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            UnifiedRandom rand = WorldGen.genRand;
            LayerBand band = ctx.Band;
            L4WaterWorks.Reset();

            int xLeft = DungeonworldMetrics.PlayLeft + 8;
            int xRight = DungeonworldMetrics.PlayRight - 8;
            int usableTop = band.Top + 14;
            int bottomLimit = band.SpineInteriorTop - 6;
            int span = bottomLimit - usableTop;
            if (span < GroupCount * 120) {
                throw new System.InvalidOperationException(
                    $"[L4Content] 层带{band.Top}~{band.Bottom}可用行{span}<{GroupCount}*120,层带预算被改动?");
            }
            int pitch = span / GroupCount;

            var groups = new List<Group>(GroupCount);
            for (int g = 0; g < GroupCount; g++) {
                int dry = usableTop + DryHang + g * pitch;
                int next = g + 1 < GroupCount
                    ? usableTop + DryHang + (g + 1) * pitch
                    : band.SpineFloorTop;
                groups.Add(new Group {
                    Index = g,
                    DryFloor = dry,
                    Waterline = dry + WaterlineGap,
                    NextFloor = next,
                });
            }

            //1) 服务井足印先占(房间构造性避开;左右翼各一,跳开主竖井)
            foreach (Group group in groups) {
                int yTop = group.DryFloor - 18;
                int yBot = group.NextFloor + 2;
                group.ServiceLeft = ReserveServiceWell(ctx, xLeft + 2, xLeft + 80, yTop, yBot);
                group.ServiceRight = ReserveServiceWell(ctx,
                    DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 6,
                    xRight - 8, yTop, yBot);
            }

            //2) 逐组落房
            var caps = new Caps();
            var placed = new List<PlacedNode>();
            int furnPlaced = 0, furnRejected = 0;
            foreach (Group group in groups) {
                PlaceGroup(ctx, group, caps, placed, rand, xLeft, xRight,
                    ref furnPlaced, ref furnRejected);
            }

            //3) 干链边+湿port+注水坑
            int dryLinks = 0, wetPorts = 0, pits = 0;
            foreach (Group group in groups) {
                dryLinks += RouteDryLinks(ctx, group, ref pits);
                wetPorts += RouteWetPorts(ctx, group);
            }

            //4) 服务井刻画+干/湿接驳
            int wells = 0, wellLinks = 0;
            foreach (Group group in groups) {
                wells += CarveGroupWells(group);
                wellLinks += LinkWells(ctx, group);
            }

            //5) 孤立节点兜底(fail loud交P80)
            foreach (PlacedNode node in placed) {
                if (!HasAnyEdge(ctx.Graph, node.GraphIndex)) {
                    CWRMod.Instance.Logger.Error(
                        $"[L4Content] 节点{node.Kind}@{node.Room.Bounds}无链边,预计洪泛不可达,责任=L4规划器");
                }
            }

            //6) 水体:几何家具已冻结。满水态写入→settle→静定断言→双水线/分带墙
            int wetCells = L4WaterWorks.FillState(high: true);
            L4WaterWorks.SettleBand(band);
            int asserted = L4WaterWorks.AssertBandWater(band);
            L4WaterWorks.PaintAging();
            PaintDrainedMemory(groups);

            //7) 撒布声明(P55,契约纪律5)
            ctx.Scatter.AddRange(L4Scatter.Entries());

            if (caps.Galleries < 12 || caps.Valves < 3 || caps.PumpMain < 1 || caps.Splash < 1) {
                CWRMod.Instance.Logger.Warn(
                    $"[L4Content] 数量档低于花名册下限:管廊{caps.Galleries}/12 阀{caps.Valves}/3"
                    + $" 主泵{caps.PumpMain}/1 落水{caps.Splash}/1,查占用栅格拒绝量");
            }

            CWRMod.Instance.Logger.Info(
                $"[L4Content] 水牢落成 groups={groups.Count} nodes={placed.Count}"
                + $"(廊{caps.Galleries} 阀{caps.Valves} 厅{caps.Reservoirs} 囚{caps.Sunken}"
                + $" 井{caps.Plunges} 主泵{caps.PumpMain} 次泵{caps.PumpSec} 闸{caps.Gates} 落水{caps.Splash})"
                + $" 干链{dryLinks} 湿port{wetPorts} 坑{pits} 服务井{wells} 井接{wellLinks}"
                + $" 舱段={L4WaterWorks.Compartments.Count} 注水={wetCells} 断言水格={asserted}"
                + $" 泵锚={L4WaterWorks.PumpMachineAnchor} 家具={furnPlaced}成/{furnRejected}拒"
                + $" grid={ctx.Grid.ReserveOk}留/{ctx.Grid.ReserveReject}拒"
                + $" graphConnected={ctx.Graph.IsConnected()}(分量间由服务井/层脊桥接,洪泛为准)");
        }

        //==================== 组内落房 ====================

        private static void PlaceGroup(LayerBuildContext ctx, Group group, Caps caps,
            List<PlacedNode> placed, UnifiedRandom rand, int xLeft, int xRight,
            ref int furnPlaced, ref int furnRejected) {

            int start = xLeft + 12 + rand.Next(0, 16);
            int cursor = start;
            foreach (NodeKind kind in DrySequence(group.Index)) {
                PlacedNode node = TryBuild(ctx, group, kind, ref cursor, xRight, rand,
                    drained: false, ref furnPlaced, ref furnRejected);
                if (node == null) {
                    continue;
                }
                AddNode(ctx, group, node, placed, caps);
            }

            cursor = start + rand.Next(0, 10);
            int galSeen = 0;
            foreach (NodeKind kind in WetSequence(group.Index)) {
                bool drained = group.Index == GroupCount - 1 && kind == NodeKind.Gallery && galSeen < 2;
                if (kind == NodeKind.Gallery) {
                    galSeen++;
                }
                PlacedNode node = TryBuild(ctx, group, kind, ref cursor, xRight, rand,
                    drained, ref furnPlaced, ref furnRejected);
                if (node == null) {
                    continue;
                }
                AddNode(ctx, group, node, placed, caps);
            }
        }

        private static List<NodeKind> DrySequence(int g) => g switch {
            0 => [NodeKind.Splash, NodeKind.Valve, NodeKind.Gate],
            1 => [NodeKind.Valve, NodeKind.PumpSec],
            2 => [NodeKind.Valve, NodeKind.PumpMain],
            3 => [NodeKind.Valve],
            _ => [NodeKind.Valve, NodeKind.PumpSec, NodeKind.Gate],
        };

        private static List<NodeKind> WetSequence(int g) => g switch {
            0 => [NodeKind.Gallery, NodeKind.Gallery, NodeKind.Gallery, NodeKind.Gallery],
            1 => [NodeKind.Gallery, NodeKind.Gallery, NodeKind.Sunken, NodeKind.Gallery, NodeKind.Gallery, NodeKind.Plunge],
            2 => [NodeKind.Gallery, NodeKind.Reservoir, NodeKind.Gallery, NodeKind.Gallery, NodeKind.Gallery, NodeKind.Plunge],
            3 => [NodeKind.Gallery, NodeKind.Gallery, NodeKind.Sunken, NodeKind.Gallery, NodeKind.Reservoir, NodeKind.Gallery],
            _ => [NodeKind.Gallery, NodeKind.Gallery, NodeKind.Sunken, NodeKind.Gallery, NodeKind.Plunge],
        };

        private static void AddNode(LayerBuildContext ctx, Group group, PlacedNode node,
            List<PlacedNode> placed, Caps caps) {
            node.GraphIndex = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(node.Room);
            placed.Add(node);
            bool dry = node.Kind is NodeKind.Valve or NodeKind.PumpMain or NodeKind.PumpSec
                or NodeKind.Gate or NodeKind.Splash;
            if (dry) {
                group.Dry.Add(node);
            }
            else {
                group.Wet.Add(node);
                if (node.Drained) {
                    group.DrainedGalleries.Add(node);
                }
            }
            switch (node.Kind) {
                case NodeKind.Gallery: caps.Galleries++; break;
                case NodeKind.Valve: caps.Valves++; break;
                case NodeKind.Reservoir: caps.Reservoirs++; break;
                case NodeKind.Sunken: caps.Sunken++; break;
                case NodeKind.Plunge: caps.Plunges++; break;
                case NodeKind.PumpMain: caps.PumpMain++; break;
                case NodeKind.PumpSec: caps.PumpSec++; break;
                case NodeKind.Gate: caps.Gates++; break;
                case NodeKind.Splash: caps.Splash++; break;
            }
        }

        private static PlacedNode TryBuild(LayerBuildContext ctx, Group group, NodeKind kind,
            ref int cursor, int xRight, UnifiedRandom rand, bool drained,
            ref int furnPlaced, ref int furnRejected) {

            Point size = kind switch {
                NodeKind.Gallery => L4Rooms.GalleryInteriorSize(rand),
                NodeKind.Valve => L4Rooms.ValveRoomInteriorSize(rand),
                NodeKind.Reservoir => L4Rooms.ReservoirInteriorSize(rand),
                NodeKind.Sunken => L4Rooms.SunkenCellInteriorSize(rand),
                NodeKind.Plunge => L4Rooms.PlungeWellInteriorSize(),
                NodeKind.PumpMain => L4Rooms.PumpHouseInteriorSize(rand, main: true),
                NodeKind.PumpSec => L4Rooms.PumpHouseInteriorSize(rand, main: false),
                NodeKind.Gate => L4Rooms.GateCorridorInteriorSize(rand),
                _ => L4Rooms.SplashHallInteriorSize(rand),
            };
            int floor = kind switch {
                NodeKind.Gallery => group.Waterline + 4,
                NodeKind.Sunken => group.Waterline + 10,
                NodeKind.Reservoir => group.Waterline + 20,
                NodeKind.Plunge => group.Waterline + 24,
                NodeKind.Splash => group.DryFloor + L4Rooms.SplashPoolDrop,
                _ => group.DryFloor,
            };

            RoomNode room = null;
            for (int wave = 0; wave < 3 && room == null; wave++) {
                SkipShaft(ref cursor);
                int windowMax = System.Math.Min(cursor + size.X + 36, xRight);
                if (windowMax - cursor < size.X + DungeonworldMetrics.RoomShellThick * 2) {
                    if (cursor < ShaftRight() && xRight > ShaftRight() + 8) {
                        cursor = ShaftRight() + 8;
                        continue;
                    }
                    break;
                }
                room = RoomPlacer.TryPlace(ctx.Grid, rand, cursor, windowMax, floor, size, size, retries: 8);
                if (room == null) {
                    cursor += wave == 0 ? 28 : 40;
                }
            }
            if (room == null) {
                CWRMod.Instance.Logger.Warn(
                    $"[L4Content] {kind}组{group.Index}三轮未落位,弃(cursor={cursor})");
                return null;
            }
            if (HitsShaft(room.Bounds.Left, room.Bounds.Right)) {
                CWRMod.Instance.Logger.Warn($"[L4Content] {kind}落进主竖井列,弃@{room.Bounds}");
                return null;
            }
            cursor = room.Bounds.Right + rand.Next(5, 10);

            var node = new PlacedNode {
                Room = room, Kind = kind, Drained = drained, Waterline = group.Waterline,
            };
            bool sunkenChest = kind == NodeKind.Gallery && !drained && rand.NextBool(3);
            string valveSign = group.Index == GroupCount - 1 && kind == NodeKind.Valve
                ? L4Palette.DryApproachSignText : null;
            L4Rooms.Tally tally = kind switch {
                NodeKind.Gallery => L4Rooms.BuildGallery(room, group.Waterline, rand, drained, sunkenChest),
                NodeKind.Valve => L4Rooms.BuildValveRoom(room, rand, valveSign),
                NodeKind.Reservoir => L4Rooms.BuildReservoir(room, group.Waterline, rand),
                NodeKind.Sunken => L4Rooms.BuildSunkenCells(room, group.Waterline, rand),
                NodeKind.Plunge => L4Rooms.BuildPlungeWell(room, group.Waterline, rand),
                NodeKind.PumpMain => L4Rooms.BuildPumpHouse(room, rand, main: true),
                NodeKind.PumpSec => L4Rooms.BuildPumpHouse(room, rand, main: false),
                NodeKind.Gate => L4Rooms.BuildGateCorridor(room, rand),
                _ => L4Rooms.BuildSplashHall(room, rand),
            };
            furnPlaced += tally.Placed;
            furnRejected += tally.Rejected;
            return node;
        }

        //==================== 链边 / 湿port / 坑 ====================

        private static int RouteDryLinks(LayerBuildContext ctx, Group group, ref int pits) {
            int routed = 0;
            var ground = new List<PlacedNode>();
            foreach (PlacedNode n in group.Dry) {
                if (n.Kind != NodeKind.Splash && n.Room.FloorTop == group.DryFloor) {
                    ground.Add(n);
                }
            }
            ground.Sort((a, b) => a.Room.Bounds.Left.CompareTo(b.Room.Bounds.Left));
            for (int i = 0; i + 1 < ground.Count; i++) {
                PlacedNode a = ground[i];
                PlacedNode b = ground[i + 1];
                int gapL = a.Room.Bounds.Right;
                int gapR = b.Room.Bounds.Left;
                if (gapR - gapL > 40 || HitsShaft(gapL, gapR)) {
                    continue;
                }
                var tally = new L4Rooms.Tally();
                if (!L4Rooms.LinkDryRooms(a.Room, b.Room, group.DryFloor, L4Palette.WallBase, ref tally)) {
                    continue;
                }
                ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                    SocketKind.Door, EdgeForm.Horizontal));
                routed++;
                //注水坑:全世界注水坑收归本层(INDEX §3);只落在下方无湿房的缝里
                if (gapR - gapL >= 10 && !WetUnder(group, gapL + 2, gapL + 6)) {
                    L4Rooms.CarveWaterPit(gapL + 3, group.DryFloor, L4Palette.WallSlab);
                    pits++;
                }
            }
            //落水厅台肩接最近干房(台肩地板=干层)
            foreach (PlacedNode splash in group.Dry) {
                if (splash.Kind != NodeKind.Splash) {
                    continue;
                }
                PlacedNode near = Nearest(ground, splash.Room.Bounds.Left);
                if (near == null) {
                    continue;
                }
                int spanL = System.Math.Min(splash.Room.Bounds.Left, near.Room.Bounds.Left);
                int spanR = System.Math.Max(splash.Room.Bounds.Right, near.Room.Bounds.Right);
                if (HitsShaft(spanL, spanR)) {
                    continue;
                }
                int ledge = splash.Room.FloorTop - L4Rooms.SplashPoolDrop;
                if (near.Room.FloorTop != ledge) {
                    continue;
                }
                var tally = new L4Rooms.Tally();
                if (L4Rooms.LinkDryRooms(splash.Room, near.Room, ledge, L4Palette.WallBase, ref tally)) {
                    ctx.Graph.Edges.Add(new RoomEdge(splash.GraphIndex, near.GraphIndex,
                        SocketKind.Door, EdgeForm.Horizontal));
                    routed++;
                }
            }
            return routed;
        }

        private static int RouteWetPorts(LayerBuildContext ctx, Group group) {
            int routed = 0;
            var wet = new List<PlacedNode>(group.Wet);
            wet.Sort((a, b) => a.Room.Bounds.Left.CompareTo(b.Room.Bounds.Left));
            for (int i = 0; i + 1 < wet.Count; i++) {
                PlacedNode a = wet[i];
                PlacedNode b = wet[i + 1];
                if (a.Drained || b.Drained) {
                    continue;
                }
                int gapL = a.Room.Bounds.Right;
                int gapR = b.Room.Bounds.Left;
                if (gapR - gapL > 40 || HitsShaft(gapL, gapR)) {
                    continue;
                }
                L4Rooms.CarveWetPort(a.Room.InteriorRight, b.Room.InteriorLeft,
                    group.Waterline, L4Palette.WallSlab);
                ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                    SocketKind.Archway, EdgeForm.Horizontal));
                routed++;
            }
            return routed;
        }

        //==================== 服务井 ====================

        private static int ReserveServiceWell(LayerBuildContext ctx, int xMin, int xMax, int yTop, int yBot) {
            int w = DungeonworldMetrics.StairWellWidth + 2;
            int h = yBot - yTop;
            if (h < 8) {
                return -1;
            }
            for (int x = xMin; x + w <= xMax; x += 6) {
                if (HitsShaft(x - 1, x + w + 1)) {
                    continue;
                }
                var strip = new Rectangle(x, yTop, w, h);
                if (!ctx.Grid.CanReserve(strip, 0)) {
                    continue;
                }
                ctx.Grid.MarkUnchecked(strip);
                return x + 1;
            }
            CWRMod.Instance.Logger.Warn($"[L4Content] 服务井在[{xMin},{xMax}) y={yTop}~{yBot}未抢到列");
            return -1;
        }

        private static int CarveGroupWells(Group group) {
            int n = 0;
            foreach (int x in new[] { group.ServiceLeft, group.ServiceRight }) {
                if (x < 0) {
                    continue;
                }
                CorridorRouter.CarveStairWell(x, group.DryFloor, group.NextFloor,
                    L4Palette.PlatformFrameY, L4Palette.WallBase);
                TileBrush.PlatformRow(x, x + DungeonworldMetrics.StairWellWidth,
                    group.DryFloor, L4Palette.PlatformFrameY);
                //井壁检修龛:贴中段平台行(§2.5)
                int alcoveY = (group.DryFloor + group.NextFloor) / 2;
                alcoveY -= alcoveY % DungeonworldMetrics.ShaftStepRows;
                if (alcoveY > group.DryFloor + 8 && alcoveY < group.NextFloor - 8) {
                    L4Rooms.CarveShaftAlcove(x, alcoveY, leftSide: true, WorldGen.genRand);
                }
                n++;
            }
            return n;
        }

        private static int LinkWells(LayerBuildContext ctx, Group group) {
            int links = 0;
            links += LinkWellSide(ctx, group, group.ServiceLeft);
            links += LinkWellSide(ctx, group, group.ServiceRight);
            return links;
        }

        private static int LinkWellSide(LayerBuildContext ctx, Group group, int wellX) {
            if (wellX < 0) {
                return 0;
            }
            int wellR = wellX + DungeonworldMetrics.StairWellWidth;
            int linked = 0;

            //干层:井口接到最近齐平干房(4高走廊,不开湿port：湿port会在井内铺堰坎堵竖向)
            PlacedNode dry = NearestFloor(group.Dry, wellX, group.DryFloor);
            if (dry != null && !HitsShaft(System.Math.Min(dry.Room.Bounds.Left, wellX),
                System.Math.Max(dry.Room.Bounds.Right, wellR))) {
                bool wellOnLeft = wellR <= dry.Room.Bounds.Left;
                int corL = wellOnLeft ? wellR : dry.Room.Bounds.Right;
                int corR = wellOnLeft ? dry.Room.Bounds.Left : wellX;
                if (corR > corL && corR - corL <= 40) {
                    int floor = group.DryFloor;
                    if (wellOnLeft) {
                        TileBrush.CarveRect(dry.Room.Bounds.Left, floor - 3, dry.Room.InteriorLeft, floor,
                            L4Palette.WallBase);
                    }
                    else {
                        TileBrush.CarveRect(dry.Room.InteriorRight, floor - 3, dry.Room.Bounds.Right, floor,
                            L4Palette.WallBase);
                    }
                    for (int x = corL; x < corR; x++) {
                        TileBrush.SetSolid(x, floor, L4Palette.Brick);
                        TileBrush.SetSolid(x, floor - 5, L4Palette.Brick);
                    }
                    TileBrush.CarveRect(corL, floor - 4, corR, floor, L4Palette.WallBase);
                    ctx.Graph.Edges.Add(new RoomEdge(dry.GraphIndex, dry.GraphIndex,
                        SocketKind.PlatformGap, EdgeForm.StairWell));
                    linked++;
                }
            }

            //湿层:井柱不入湿port包络(会在井内铺堰坎堵竖向),从井缘接到最近湿房的近侧内缘
            PlacedNode wet = NearestWet(group, wellX);
            if (wet != null && !wet.Drained) {
                bool wellOnLeft = wellR <= wet.Room.Bounds.Left;
                int wellEdge = wellOnLeft ? wellR : wellX;
                int roomEdge = wellOnLeft ? wet.Room.InteriorLeft : wet.Room.InteriorRight;
                if (System.Math.Abs(roomEdge - wellEdge) <= 40 && !HitsShaft(wellEdge, roomEdge)) {
                    L4Rooms.CarveWetPort(wellEdge, roomEdge, group.Waterline, L4Palette.WallSlab);
                    ctx.Graph.Edges.Add(new RoomEdge(wet.GraphIndex, wet.GraphIndex,
                        SocketKind.Archway, EdgeForm.StairWell));
                    linked++;
                }
            }
            return linked;
        }

        //==================== 做旧补笔 / 几何助手 ====================

        //干涸舱段不入舱段表,仍刷历史水线(灰/黑)：玩家读线知道"水曾经到过这"
        private static void PaintDrainedMemory(List<Group> groups) {
            foreach (Group group in groups) {
                foreach (PlacedNode n in group.DrainedGalleries) {
                    int l = n.Room.InteriorLeft + 2;
                    int r = n.Room.InteriorRight - 2;
                    L4Palette.PaintWaterlineRow(l, r, group.Waterline, L4Palette.HighLinePaint);
                    L4Palette.PaintWaterlineRow(l, r, n.Room.FloorTop - 1, L4Palette.LowLinePaint);
                    L4Palette.BandWalls(l, r, n.Room.InteriorTop, n.Room.FloorTop, group.Waterline);
                }
            }
        }

        private static bool WetUnder(Group group, int left, int right) {
            foreach (PlacedNode n in group.Wet) {
                if (n.Room.Bounds.Left < right && n.Room.Bounds.Right > left) {
                    return true;
                }
            }
            return false;
        }

        private static PlacedNode Nearest(List<PlacedNode> list, int x) {
            PlacedNode best = null;
            int bestD = int.MaxValue;
            foreach (PlacedNode n in list) {
                int d = System.Math.Abs(n.Room.Bounds.Left + n.Room.Bounds.Width / 2 - x);
                if (d < bestD) {
                    bestD = d;
                    best = n;
                }
            }
            return best;
        }

        private static PlacedNode NearestFloor(List<PlacedNode> list, int x, int floor) {
            PlacedNode best = null;
            int bestD = int.MaxValue;
            foreach (PlacedNode n in list) {
                if (n.Room.FloorTop != floor) {
                    continue;
                }
                int d = System.Math.Abs(n.Room.Bounds.Left + n.Room.Bounds.Width / 2 - x);
                if (d < bestD) {
                    bestD = d;
                    best = n;
                }
            }
            return best;
        }

        private static PlacedNode NearestWet(Group group, int x) {
            PlacedNode best = null;
            int bestD = int.MaxValue;
            foreach (PlacedNode n in group.Wet) {
                if (n.Drained) {
                    continue;
                }
                int d = System.Math.Abs(n.Room.Bounds.Left + n.Room.Bounds.Width / 2 - x);
                if (d < bestD) {
                    bestD = d;
                    best = n;
                }
            }
            return best;
        }

        private static bool HasAnyEdge(RoomGraph graph, int index) {
            foreach (RoomEdge e in graph.Edges) {
                if (e.A == index || e.B == index) {
                    return true;
                }
            }
            return false;
        }

        private static int ShaftRight()
            => DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth;

        private static bool HitsShaft(int left, int right)
            => left < ShaftRight() + 4 && right > DungeonworldMetrics.ShaftLeft - 4;

        private static void SkipShaft(ref int cursor) {
            if (cursor < ShaftRight() + 8 && cursor + 20 > DungeonworldMetrics.ShaftLeft - 4) {
                cursor = ShaftRight() + 8;
            }
        }
    }
}
