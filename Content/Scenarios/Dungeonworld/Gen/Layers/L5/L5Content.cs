using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //层内跨文件共享的生成运行态:撒布lambda(P55时点)读取;
    //每次生成由PlanAndBuild重置(ShouldSave=false回放制,禁止跨生成残留)
    internal static class L5State
    {
        /// <summary>无光深巷带上缘行:骨灯笼/吊笼类撒布在此行以下拒入(INDEX §3无光区裁决)</summary>
        internal static int DarkZoneTop = int.MaxValue;
        /// <summary>墓碑撒布下限行:只散在上带(ROOMS-L5 §2.1去重距≥20)</summary>
        internal static int TombstoneMaxY = int.MinValue;

        internal static void Reset() {
            DarkZoneTop = int.MaxValue;
            TombstoneMaxY = int.MinValue;
        }
    }

    /// <summary>
    /// L5万骨窖内容入口(Wave-2接缝契约,契约全文见 LayerBuildContext 头注释)。
    /// <para/>管线路/父级一行接线(P50调度槽):
    /// <code>Layers.L5.L5Content.PlanAndBuild(LayerPlans.L5);</code>
    /// <para/>前置依赖:P30已建ctx(占用栅格含脊/主竖井/跨层预留足印),本入口只消费
    /// ctx.Grid/ctx.Graph/ctx.Scatter与冻结机器公开API(RoomPlacer/CorridorRouter/TileBrush),
    /// 不触碰 Dungeonworld.cs 与任何pass文件;随机全走WorldGen.genRand(F22)。
    /// <para/>层结构:1400行大带切六地层(上带2层龛廊+骨柱厅/中带集市检查点/下带坑场+深巷),
    /// 地层内横向游走坑道成链、地层间之字斜降坑道+骨井竖连、底层骨室楼梯井回脊
    /// 游走走廊=本层主连接形态(ROOMS-L5 §0连接语法/STRUCTURES §2.1裁决3)。
    /// <para/>凿刻预算心算(R5,全局生成<3min):房约27间x平均2.5k格≈7万,游走坑道约
    /// 25条x均8k格(每步盖章约170格x步数)≈20万~60万,井/坑/横档≈5万;合计≤百万格级,
    /// 每格为常数次字段写,实测量级毫秒~百毫秒,远低于P10全图浇筑(1200万格),安全。
    /// </summary>
    internal static class L5Content
    {
        private enum NodeKind { Hall, Gallery, Market, PitField, Ossuary, BoneCell }

        private sealed class PlacedNode
        {
            internal RoomNode Room;
            internal NodeKind Kind;
            internal L5Rooms.RoomInfo Info;
            internal int Stratum;
            internal int GraphIndex;
            //地板/天花接口位一房各一次(井口/下行/落口互斥占用)
            internal bool FloorSlotUsed;
            internal bool CeilSlotUsed;
            internal int CenterX => Room.Bounds.Left + Room.Bounds.Width / 2;
        }

        //主竖井加固带:即使栅格未标井列也不许侵入(层公共动脉,双保险)
        private static bool HitsShaft(int left, int right)
            => left < DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 3
            && right > DungeonworldMetrics.ShaftLeft - 3;

        /// <summary>层内容主入口:地层布房→游走链边→骨井→斜降连接→脊接驳→深巷藏龛→撒布声明</summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            UnifiedRandom rand = WorldGen.genRand;
            LayerBand band = ctx.Band;
            L5State.Reset();

            //===地层行与x窗(随机消耗点前置且顺序固定,R4纪律)===
            //六地层基准偏移:上带140/300(龛廊+骨柱厅),中带540(集市),下带800/1060/1300(坑场+深巷)
            int[] floors = new int[6];
            int[] baseOffsets = [140, 300, 540, 800, 1060, 1300];
            for (int i = 0; i < 6; i++) {
                floors[i] = band.Top + baseOffsets[i] + rand.Next(-8, 9);
            }
            //x窗交替左右漂移:宏观读法=蜿蜒向下的墓城,全幅覆盖靠地层错位叠加
            int[,] winBase = { { 200, 1150 }, { 850, 1800 }, { 450, 1500 }, { 150, 1050 }, { 900, 1830 }, { 380, 1620 } };
            var windows = new (int lo, int hi)[6];
            for (int i = 0; i < 6; i++) {
                int shift = rand.Next(-60, 61);
                windows[i] = (
                    System.Math.Max(DungeonworldMetrics.PlayLeft + 10, winBase[i, 0] + shift),
                    System.Math.Min(DungeonworldMetrics.PlayRight - 10, winBase[i, 1] + shift));
            }

            //===逐地层布房(镜像L2:掷计划→预留→刻画→装修)===
            var strata = new List<PlacedNode>[6];
            var placed = new List<PlacedNode>();
            int spikeBudget = 3; //刺坑全层预算:总坑约6~10,出现比≤1/3(ROOMS-L5 §1)
            var pitReport = new L5Rooms.PitReport();
            var tally = new L5Rooms.Tally();

            for (int s = 0; s < 6; s++) {
                strata[s] = new List<PlacedNode>();
                int cursor = windows[s].lo + rand.Next(0, 20);
                foreach (NodeKind kind in RollSequence(s, rand)) {
                    PlacedNode node = PlaceAndBuildNode(ctx, rand, kind, s, floors[s],
                        ref cursor, windows[s].hi, ref spikeBudget, ref pitReport, ref tally);
                    if (node == null) {
                        continue;
                    }
                    node.GraphIndex = ctx.Graph.Rooms.Count;
                    ctx.Graph.Rooms.Add(node.Room);
                    strata[s].Add(node);
                    placed.Add(node);
                }
            }

            //===边路由:横向游走链→骨井→跨层斜降→井底续接→脊接驳===
            var crossbars = new List<Point>();
            var alleyMids = new List<Point>();
            int wanderEdges = 0, straightEdges = 0, skippedEdges = 0;
            for (int s = 0; s < 6; s++) {
                for (int g = 0; g + 1 < strata[s].Count; g++) {
                    RouteLateral(ctx, strata[s][g], strata[s][g + 1], s == 5, rand,
                        crossbars, alleyMids, ref wanderEdges, ref straightEdges, ref skippedEdges);
                }
            }

            int wellsBuilt = 0, wellsSkipped = 0;
            (int host, int target)[] wellPlan = [(1, 2), (2, 3), (4, 5)];
            var wellLandings = new List<(PlacedNode landing, int targetStratum)>();
            foreach ((int hostS, int targetS) in wellPlan) {
                PlacedNode host = PickFreeFloorNode(strata[hostS], preferGallery: true);
                if (host == null) {
                    wellsSkipped++;
                    continue;
                }
                PlacedNode landing = BuildBoneWell(ctx, host, rand, ref tally);
                if (landing == null) {
                    wellsSkipped++;
                    continue;
                }
                wellsBuilt++;
                wellLandings.Add((landing, targetS));
            }

            int descents = 0, descentSkips = 0;
            for (int g = 0; g + 1 < 6; g++) {
                descents += RouteStrataConnectors(ctx, strata[g], strata[g + 1], g + 1 == 5,
                    rand, crossbars, ref descentSkips);
            }
            //井底续接:落室→下方地层最近节点(断头井违规=fail loud,ROOMS-L5 §1-4)
            foreach ((PlacedNode landing, int targetS) in wellLandings) {
                if (!RouteDescent(ctx, landing, PickNearestCeil(strata[targetS], landing.CenterX),
                    targetS == 5, rand, crossbars)) {
                    CWRMod.Instance.Logger.Warn(
                        $"[L5Content] 骨井落室续接失败,井底成死端 at {landing.Room.Bounds}(可达性由井链保证,风味降级)");
                }
                else {
                    descents++;
                }
            }

            int drops = RouteSpineDrops(ctx, strata, band);

            //===延迟装配:横档平台(防后续盖章清掉)→深巷藏龛===
            int bars = L5TunnelCarver.FlushCrossbars(crossbars, L5Palette.PlatformBone);
            int pockets = 0;
            foreach (Point mid in alleyMids) {
                if (L5Rooms.PocketReward(mid, rand)) {
                    pockets++;
                }
            }

            //===墙变体混斑:主体Slab里成片切出Tiled(原版粉墙三种,此前主体只用一种)===
            int mixedWalls = L5Palette.MixWallVariants(new Rectangle(
                DungeonworldMetrics.PlayLeft, band.Top,
                DungeonworldMetrics.PlayRight - DungeonworldMetrics.PlayLeft, band.Bottom - band.Top));

            //===撒布声明(P55统一执行,契约纪律5)===
            L5State.DarkZoneTop = floors[5] - 80;   //深巷带上缘(含游走漂移余量):灯类撒布拒入
            L5State.TombstoneMaxY = floors[1] + 20; //墓碑只散上带
            ctx.Scatter.AddRange(L5Scatter.Entries());

            //===孤立节点兜底检查(fail loud交P80复核)===
            foreach (PlacedNode node in placed) {
                if (!node.FloorSlotUsed && !HasAnyEdge(ctx.Graph, node.GraphIndex)) {
                    CWRMod.Instance.Logger.Error(
                        $"[L5Content] 节点{node.Kind}@{node.Room.Bounds}无链边且无落口,预计洪泛不可达,责任=L5规划器");
                }
            }

            CWRMod.Instance.Logger.Info(
                $"[L5Content] 万骨窖落成 nodes={placed.Count}(厅{Count(placed, NodeKind.Hall)}"
                + $" 廊{Count(placed, NodeKind.Gallery)} 集市{Count(placed, NodeKind.Market)}"
                + $" 坑场{Count(placed, NodeKind.PitField)} 圣骨堂{Count(placed, NodeKind.Ossuary)}"
                + $" 骨室{Count(placed, NodeKind.BoneCell)})+井落室{wellsBuilt}"
                + $" 边:游走{wanderEdges}/直线{straightEdges}/跳过{skippedEdges}"
                + $" 斜降{descents}(拒{descentSkips}) 骨井{wellsBuilt}建/{wellsSkipped}弃 脊落口{drops}"
                + $" 坑:骨{pitReport.BonePits}/刺{pitReport.SpikePits}/弃{pitReport.SkippedPits}"
                + $" 横档{bars} 巷龛{pockets} 墙变体混斑{mixedWalls} 家具={tally.Placed}成/{tally.Rejected}拒"
                + $" graphConnected={ctx.Graph.IsConnected()}(分量间由脊/主井桥接,洪泛为准)"
                + $" grid={ctx.Grid.ReserveOk}留/{ctx.Grid.ReserveReject}拒");
        }

        //==================== 地层节点序列(数量档:厅6~8/廊8~10/集市1/坑场5/圣骨堂1/骨室4~5) ====================

        private static List<NodeKind> RollSequence(int stratum, UnifiedRandom rand) {
            List<NodeKind> seq = stratum switch {
                0 => [NodeKind.Gallery, NodeKind.Hall, NodeKind.Gallery, NodeKind.Hall, NodeKind.Gallery],
                1 => [NodeKind.Hall, NodeKind.Gallery, NodeKind.Hall, NodeKind.Gallery],
                2 => [NodeKind.Gallery, NodeKind.Market, NodeKind.Hall, NodeKind.Gallery],
                3 => [NodeKind.PitField, NodeKind.Hall, NodeKind.PitField, NodeKind.Gallery, NodeKind.PitField],
                4 => [NodeKind.PitField, NodeKind.Gallery, NodeKind.Hall, NodeKind.PitField, NodeKind.Ossuary],
                _ => [NodeKind.BoneCell, NodeKind.BoneCell, NodeKind.BoneCell, NodeKind.BoneCell],
            };
            if (stratum == 0 && rand.NextBool(2)) {
                seq.Add(NodeKind.Gallery);
            }
            if (stratum == 5 && rand.NextBool(2)) {
                seq.Add(NodeKind.BoneCell);
            }
            return seq;
        }

        private static int Count(List<PlacedNode> nodes, NodeKind kind) {
            int n = 0;
            foreach (PlacedNode node in nodes) {
                if (node.Kind == kind) {
                    n++;
                }
            }
            return n;
        }

        //==================== 落位+刻画(预留失败=前进重试两轮再弃,镜像L2) ====================

        private static PlacedNode PlaceAndBuildNode(LayerBuildContext ctx, UnifiedRandom rand,
            NodeKind kind, int stratum, int floor, ref int cursor, int xRight,
            ref int spikeBudget, ref L5Rooms.PitReport pitReport, ref L5Rooms.Tally tally) {

            Point size = kind switch {
                NodeKind.Hall => L5Rooms.HallInteriorSize(rand),
                NodeKind.Gallery => L5Rooms.GalleryInteriorSize(rand),
                NodeKind.Market => L5Rooms.MarketInteriorSize(rand),
                NodeKind.PitField => L5Rooms.PitFieldInteriorSize(rand),
                NodeKind.Ossuary => L5Rooms.OssuaryInteriorSize(rand),
                _ => L5Rooms.BoneCellInteriorSize(rand),
            };

            RoomNode room = null;
            for (int wave = 0; wave < 3 && room == null; wave++) {
                int windowMax = System.Math.Min(cursor + size.X + 30, xRight);
                room = RoomPlacer.TryPlace(ctx.Grid, rand, cursor, windowMax, floor, size, size, retries: 8);
                if (room == null) {
                    //窗口被主井/跨层预留占满:前进跨越
                    cursor += wave == 0 ? 26 : 40;
                }
            }
            if (room == null) {
                CWRMod.Instance.Logger.Warn($"[L5Content] {kind}三轮未落位,弃(地层{stratum} cursor={cursor})");
                return null;
            }
            if (HitsShaft(room.Bounds.Left, room.Bounds.Right)) {
                //栅格本应挡住主井列带;真踩进来说明预留缺席,让位并示警(双保险)
                CWRMod.Instance.Logger.Error($"[L5Content] {kind}落进主竖井带{room.Bounds},弃并示警(P30预留缺席?)");
                cursor = room.Bounds.Right + 8;
                return null;
            }
            cursor = room.Bounds.Right + rand.Next(6, 15);

            var node = new PlacedNode { Room = room, Kind = kind, Stratum = stratum };
            switch (kind) {
                case NodeKind.Hall:
                    node.Info = L5Rooms.BuildHall(room, rand);
                    break;
                case NodeKind.Gallery:
                    node.Info = L5Rooms.BuildGallery(room, rand);
                    break;
                case NodeKind.Market:
                    room.Role = RoomRole.Safe; //中途检查点:Safe清场语义归运行时(§4.5)
                    node.Info = L5Rooms.BuildMarket(room, rand);
                    break;
                case NodeKind.PitField:
                    node.Info = L5Rooms.BuildPitField(room, rand, ctx, ref spikeBudget, ref pitReport);
                    break;
                case NodeKind.Ossuary:
                    room.Role = RoomRole.Treasure;
                    node.Info = L5Rooms.BuildOssuary(room, rand);
                    break;
                default:
                    node.Info = L5Rooms.BuildBoneCell(room, rand);
                    break;
            }
            tally.Placed += node.Info.Tally.Placed;
            tally.Rejected += node.Info.Tally.Rejected;
            return node;
        }

        //==================== 横向链边:游走坑道为主形态,窄缝退直线 ====================

        private static void RouteLateral(LayerBuildContext ctx, PlacedNode a, PlacedNode b,
            bool alley, UnifiedRandom rand, List<Point> crossbars, List<Point> alleyMids,
            ref int wander, ref int straight, ref int skipped) {

            int gapL = a.Room.Bounds.Right;
            int gapR = b.Room.Bounds.Left;
            int gap = gapR - gapL;
            int floor = a.Room.FloorTop;
            if (gap > 170 || HitsShaft(gapL, gapR)) {
                skipped++; //主井列带不架门对门长廊:脊与主井即穿越路径(契约纪律4)
                return;
            }

            DoorSocket sa = SideSocket(a, SocketSide.Right);
            DoorSocket sb = SideSocket(b, SocketSide.Left);
            if (gap < 14) {
                //窄缝直线走廊(游走盖章在窄缝里施展不开)
                a.Room.Sockets.Add(sa);
                b.Room.Sockets.Add(sb);
                if (CorridorRouter.RouteDoorToDoor(a.Room, sa, b.Room, sb, L5Palette.WallSlab)) {
                    ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                        SocketKind.Archway, EdgeForm.Horizontal));
                    straight++;
                }
                return;
            }

            //游走包络:竖向上探18行下探8行;栅格预检扣掉两端房间自身padding列
            var env = new Rectangle(gapL - 2, floor - 18, gap + 4, 27);
            env = ClampToBand(ctx.Band, env);
            var check = new Rectangle(gapL + DungeonworldMetrics.RoomPadding, env.Y,
                gap - DungeonworldMetrics.RoomPadding * 2, env.Height);
            if (!ctx.Grid.CanReserve(check, 0)) {
                skipped++; //缝里有跨层预留/井体等宏观足印:让路
                return;
            }

            a.Room.Sockets.Add(sa);
            b.Room.Sockets.Add(sb);
            CorridorRouter.OpenWallSocket(a.Room, sa, L5Palette.WallSlab);
            CorridorRouter.OpenWallSocket(b.Room, sb, L5Palette.WallSlab);

            L5TunnelCarver.TunnelParams p = alley
                ? L5TunnelCarver.Alley(L5Palette.WallTiled)
                : L5TunnelCarver.Lateral(L5Palette.WallSlab);
            var start = new Point(gapL + 5, floor - 4);
            var end = new Point(gapR - 6, floor - 4);
            L5TunnelCarver.TunnelReport report = L5TunnelCarver.Carve(env, start, end, p);
            crossbars.AddRange(report.Crossbars);
            ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                SocketKind.Archway, EdgeForm.Horizontal)); //几何形态=游走(共享enum游走枚举归M3,此处记横向)
            wander++;

            if (alley) {
                //深巷:巷口各1盏"最后的灯"(进入黑暗的阈值),巷中点记藏龛位
                L5Palette.MouthLantern(start.X, start.Y);
                L5Palette.MouthLantern(end.X, end.Y);
                alleyMids.Add(report.Mid);
            }
        }

        //大房用拱5,圣骨堂唯一入口拱3(钟声门门面/门禁TP归机构波),其余拱4
        private static DoorSocket SideSocket(PlacedNode node, SocketSide side) {
            int h = node.Kind is NodeKind.Hall or NodeKind.Market ? 5
                : node.Kind == NodeKind.Ossuary ? 3 : 4;
            return new DoorSocket(side, node.Room.FloorTop - h - node.Room.Bounds.Top, SocketKind.Archway, h);
        }

        //==================== 跨地层斜降:每相邻地层对2条,集市优先保证上下贯通 ====================

        private static int RouteStrataConnectors(LayerBuildContext ctx, List<PlacedNode> upper,
            List<PlacedNode> lower, bool intoDark, UnifiedRandom rand,
            List<Point> crossbars, ref int skips) {

            //候选对:上层可开地板口x下层可开天花口,|Δx|升序;含集市的对优先(检查点必须上下都通)
            var pairs = new List<(PlacedNode u, PlacedNode l, int key)>();
            foreach (PlacedNode u in upper) {
                if (u.Info.FloorGapOffset < 0 || u.FloorSlotUsed) {
                    continue;
                }
                foreach (PlacedNode l in lower) {
                    if (l.Info.CeilGapOffset < 0 || l.CeilSlotUsed) {
                        continue;
                    }
                    int key = System.Math.Abs(u.CenterX - l.CenterX);
                    if (u.Kind == NodeKind.Market || l.Kind == NodeKind.Market) {
                        key -= 100000;
                    }
                    pairs.Add((u, l, key));
                }
            }
            pairs.Sort((x, y) => x.key.CompareTo(y.key));

            int routed = 0;
            foreach ((PlacedNode u, PlacedNode l, _) in pairs) {
                if (routed >= 2) {
                    break;
                }
                if (u.FloorSlotUsed || l.CeilSlotUsed) {
                    continue;
                }
                if (RouteDescent(ctx, u, l, intoDark, rand, crossbars)) {
                    routed++;
                }
                else {
                    skips++;
                }
            }
            return routed;
        }

        //斜降坑道:上房地板口→之字游走→下房天花口(高位PlatformGap落点,ROOMS-L5门插槽)
        private static bool RouteDescent(LayerBuildContext ctx, PlacedNode upper, PlacedNode lower,
            bool intoDark, UnifiedRandom rand, List<Point> crossbars) {
            if (upper == null || lower == null
                || upper.Info.FloorGapOffset < 0 || upper.FloorSlotUsed
                || lower.Info.CeilGapOffset < 0 || lower.CeilSlotUsed) {
                return false;
            }
            RoomNode ur = upper.Room, lr = lower.Room;
            int depX = ur.Bounds.Left + upper.Info.FloorGapOffset;
            int arrX = lr.Bounds.Left + lower.Info.CeilGapOffset;
            var start = new Point(depX + 1, ur.Bounds.Bottom + 6);
            var end = new Point(arrX + 1, lr.Bounds.Top - 6);

            var env = new Rectangle(System.Math.Min(start.X, end.X) - 28, ur.Bounds.Bottom + 1,
                System.Math.Abs(start.X - end.X) + 57, lr.Bounds.Top - ur.Bounds.Bottom - 2);
            env = ClampToBand(ctx.Band, env);
            if (HitsShaft(env.Left, env.Right)) {
                return false;
            }
            var check = new Rectangle(env.X, ur.Bounds.Bottom + 3, env.Width,
                lr.Bounds.Top - 3 - (ur.Bounds.Bottom + 3));
            if (check.Height <= 0 || !ctx.Grid.CanReserve(check, 0)) {
                return false;
            }

            //出发口:地板PlatformGap+竖stub打通壳层到游走起点
            var gap = new DoorSocket(SocketSide.Bottom, upper.Info.FloorGapOffset, SocketKind.PlatformGap, 3);
            ur.Sockets.Add(gap);
            CorridorRouter.OpenPlatformGap(ur, gap, L5Palette.PlatformBone, L5Palette.WallSlab);
            TileBrush.CarveRect(depX, ur.Bounds.Bottom, depX + 3, start.Y, L5Palette.WallSlab);
            //到达口:天花PlatformGap(洞口盖平台+下探横档)+竖stub
            var arr = new DoorSocket(SocketSide.Top, lower.Info.CeilGapOffset, SocketKind.PlatformGap, 3);
            lr.Sockets.Add(arr);
            L5Rooms.OpenCeilingGap(lr, lower.Info.CeilGapOffset, 3, L5Palette.WallSlab, L5Palette.PlatformBone);
            TileBrush.CarveRect(arrX, end.Y, arrX + 3, lr.Bounds.Top, L5Palette.WallSlab);

            L5TunnelCarver.TunnelReport report = L5TunnelCarver.Carve(env, start, end,
                L5TunnelCarver.Descent(L5Palette.WallSlab));
            crossbars.AddRange(report.Crossbars);
            ctx.Graph.Edges.Add(new RoomEdge(upper.GraphIndex, lower.GraphIndex,
                SocketKind.PlatformGap, EdgeForm.StairWell));
            upper.FloorSlotUsed = true;
            lower.CeilSlotUsed = true;

            if (intoDark) {
                //进入无光带的阈值灯:挂在上口(黑暗从这里开始)
                L5Palette.MouthLantern(start.X, start.Y);
            }
            return true;
        }

        //==================== 骨井(#4):宿主房地板井口→竖井(螺旋龛+锚顶锚底绷链)→落室 ====================

        private static PlacedNode PickFreeFloorNode(List<PlacedNode> nodes, bool preferGallery) {
            PlacedNode fallback = null;
            foreach (PlacedNode node in nodes) {
                if (node.Info.FloorGapOffset < 0 || node.FloorSlotUsed) {
                    continue;
                }
                if (preferGallery && node.Kind == NodeKind.Gallery) {
                    return node;
                }
                fallback ??= node;
            }
            return fallback;
        }

        private static PlacedNode PickNearestCeil(List<PlacedNode> nodes, int centerX) {
            PlacedNode best = null;
            int bestKey = int.MaxValue;
            foreach (PlacedNode node in nodes) {
                if (node.Info.CeilGapOffset < 0 || node.CeilSlotUsed) {
                    continue;
                }
                int key = System.Math.Abs(node.CenterX - centerX);
                if (key < bestKey) {
                    bestKey = key;
                    best = node;
                }
            }
            return best;
        }

        //井体一次性整块预留(原子性:失败即整弃,不留半截井);
        //井道语法:5宽竖膛+每5~6行侧壁螺旋龛+双绷链(井口盖平台锚顶,井底落室地板锚底)+歇脚台
        private static PlacedNode BuildBoneWell(LayerBuildContext ctx, PlacedNode host,
            UnifiedRandom rand, ref L5Rooms.Tally tally) {
            RoomNode hr = host.Room;
            int gapX = hr.Bounds.Left + host.Info.FloorGapOffset;
            int shaftLen = rand.Next(70, 111);
            int boreL = gapX - 1;                    //井膛[boreL,boreL+5)
            int landingTop = hr.Bounds.Bottom + shaftLen;
            int landingLeft = gapX + 1 - 8;          //落室16宽居中于井膛
            var reserve = new Rectangle(
                System.Math.Min(boreL - 4, landingLeft - 2), hr.Bounds.Bottom + 2,
                System.Math.Max(boreL + 9, landingLeft + 18) - System.Math.Min(boreL - 4, landingLeft - 2),
                shaftLen + 12 + 2);
            if (reserve.Bottom >= ctx.Band.SpineInteriorTop - 4 || !ctx.Grid.TryReserve(reserve, 0)) {
                CWRMod.Instance.Logger.Warn($"[L5Content] 骨井足印被占/越带,弃 host={hr.Bounds}");
                return null;
            }

            //井口:地板PlatformGap(盖骨平台=链的顶锚面)
            var gap = new DoorSocket(SocketSide.Bottom, host.Info.FloorGapOffset, SocketKind.PlatformGap, 3);
            hr.Sockets.Add(gap);
            CorridorRouter.OpenPlatformGap(hr, gap, L5Palette.PlatformBone, L5Palette.WallTiled);
            host.FloorSlotUsed = true;

            //井膛+落室
            TileBrush.CarveRect(boreL, hr.Bounds.Bottom, boreL + 5, landingTop, L5Palette.WallTiled);
            var landing = new RoomNode { Bounds = new Rectangle(landingLeft, landingTop, 16, 12) };
            L5Rooms.StampAndCarve(landing, L5Palette.WallTiled);
            TileBrush.CarveRect(boreL + 1, landingTop, boreL + 4, landing.InteriorTop, L5Palette.WallTiled);

            //螺旋龛:每5~6行左右交错,2深3高;龛底可站,轮换骨堆/瓮/每第3龛一盏龛灯
            int side = rand.NextBool(2) ? 0 : 1;
            int nicheIdx = 0;
            for (int y = hr.Bounds.Bottom + rand.Next(3, 6); y + 4 < landingTop; y += rand.Next(5, 7)) {
                int nx = side == 0 ? boreL - 2 : boreL + 5;
                TileBrush.CarveRect(nx, y, nx + 2, y + 3, L5Palette.WallTiled);
                if (nicheIdx % 3 == 2) {
                    tally.Add(L5Palette.TryPlaceObject(nx, y, TileID.HangingLanterns,
                        L5Palette.LanternBone), "井龛灯", nx, y);
                }
                else if (nicheIdx % 3 == 0) {
                    L5Palette.PlaceSmallBones(nx, y + 2, rand);
                }
                else {
                    L5Palette.PlaceUrn(nx, y + 2, rand);
                }
                L5Palette.DustWallWash(nx, y, nx + 2, y + 3);
                side = 1 - side;
                nicheIdx++;
            }

            //锚顶锚底绷链x2(可攀主通道)+歇脚台(单格,交错,不压链列)
            int chainRows = landing.FloorTop - (hr.FloorTop + 1);
            L5Palette.TautChain(gapX, hr.FloorTop + 1, chainRows);
            L5Palette.TautChain(gapX + 2, hr.FloorTop + 1, chainRows);
            int restSide = 0;
            for (int y = hr.Bounds.Bottom + 5; y < landingTop - 2; y += 5) {
                int rx = restSide == 0 ? boreL : boreL + 4;
                if (!Main.tile[rx, y].HasTile) {
                    TileBrush.SetPlatform(rx, y, L5Palette.PlatformBone);
                }
                restSide = 1 - restSide;
            }

            //落室陈设:骨堆+瓮+落室灯+尘白
            int lm = (landing.InteriorLeft + landing.InteriorRight) / 2;
            tally.Add(L5Palette.PlaceLargeBones(lm - 3, landing.FloorTop - 1, rand), "落室骨堆", lm - 3, landing.FloorTop - 1);
            tally.Add(L5Palette.PlaceUrn(landing.InteriorRight - 2, landing.FloorTop - 1, rand),
                "落室瓮", landing.InteriorRight - 2, landing.FloorTop - 1);
            tally.Add(L5Palette.TryPlaceObject(lm, landing.InteriorTop, TileID.HangingLanterns,
                L5Palette.LanternBone), "落室灯", lm, landing.InteriorTop);
            L5Palette.DustFloorRun(landing.InteriorLeft, landing.FloorTop, 12);

            var node = new PlacedNode {
                Room = landing, Kind = NodeKind.Gallery, Stratum = host.Stratum,
                Info = new L5Rooms.RoomInfo { FloorGapOffset = 4, CeilGapOffset = -1 },
                GraphIndex = ctx.Graph.Rooms.Count,
            };
            ctx.Graph.Rooms.Add(landing);
            ctx.Graph.Edges.Add(new RoomEdge(host.GraphIndex, node.GraphIndex,
                SocketKind.ShaftMouth, EdgeForm.StairWell));
            return node;
        }

        //==================== 脊接驳:S5骨室(主井两侧各≥1)楼梯井下探层脊 ====================

        private static int RouteSpineDrops(LayerBuildContext ctx, List<PlacedNode>[] strata, LayerBand band) {
            int drops = 0;
            var candidates = new List<PlacedNode>();
            foreach (PlacedNode node in strata[5]) {
                if (node.Info.FloorGapOffset >= 0 && !node.FloorSlotUsed) {
                    candidates.Add(node);
                }
            }
            //兜底:S5一侧无可用骨室时借S4节点(落程更长,楼梯井形态不变)
            foreach (PlacedNode node in strata[4]) {
                if (node.Info.FloorGapOffset >= 0 && !node.FloorSlotUsed) {
                    candidates.Add(node);
                }
            }
            PlacedNode left = null, right = null, mid = null;
            foreach (PlacedNode node in candidates) {
                if (node.CenterX < DungeonworldMetrics.ShaftLeft) {
                    if (left == null || node.Stratum == 5 && left.Stratum != 5) {
                        left = node;
                    }
                    else if (node.Stratum == 5) {
                        mid ??= node;
                    }
                }
                else if (right == null || node.Stratum == 5 && right.Stratum != 5) {
                    right = node;
                }
            }
            foreach (PlacedNode node in new[] { left, right, mid }) {
                if (node == null) {
                    continue;
                }
                var gap = new DoorSocket(SocketSide.Bottom, node.Info.FloorGapOffset,
                    SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                node.Room.Sockets.Add(gap);
                CorridorRouter.RouteToFloorBelow(node.Room, gap, band.SpineFloorTop,
                    L5Palette.PlatformPink, L5Palette.WallSlab);
                node.FloorSlotUsed = true;
                drops++;
            }
            return drops;
        }

        //==================== 公用小件 ====================

        private static Rectangle ClampToBand(LayerBand band, Rectangle rect) {
            int left = System.Math.Max(rect.Left, DungeonworldMetrics.PlayLeft + 2);
            int right = System.Math.Min(rect.Right, DungeonworldMetrics.PlayRight - 2);
            int top = System.Math.Max(rect.Top, band.Top + 4);
            int bottom = System.Math.Min(rect.Bottom, band.SpineInteriorTop - 2);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        private static bool HasAnyEdge(RoomGraph graph, int index) {
            foreach (RoomEdge e in graph.Edges) {
                if (e.A == index || e.B == index) {
                    return true;
                }
            }
            return false;
        }
    }
}
