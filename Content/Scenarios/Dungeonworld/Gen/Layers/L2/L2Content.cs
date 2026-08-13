using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    /// <summary>
    /// L2牢狱层内容入口(Wave-1接缝契约,契约全文见 LayerBuildContext 头注释)。
    /// <para/>A路一行接线(替换 LayerContentPass 的 L2 TODO 段):
    /// <code>Layers.L2.L2Content.PlanAndBuild(LayerPlans.L2);</code>
    /// <para/>前置依赖:P30 已建 ctx(占用栅格含脊/主竖井/禁室足印预留),
    /// 本入口只消费 ctx.Grid/ctx.Graph/ctx.Scatter 与冻结的机器公开API,
    /// 不触碰 Dungeonworld.cs 与任何 pass 文件;随机全走 WorldGen.genRand(F22)。
    /// </summary>
    internal static class L2Content
    {
        //节点类型(ROOMS-L2 §1花名册;井站/忏悔室=跨层公共构件,归公共构件波,本入口不做)
        private enum NodeKind { CellRow, Guard, Gallery, Hall, Registry, TrapCorridor }

        private sealed class PlacedNode
        {
            internal RoomNode Room;
            internal NodeKind Kind;
            //尽端排:右侧不再接链边(拷问室/牢栅藏物室收尾)
            internal bool DeadEndRight;
            //可开脊接驳口的落口列偏移(距Bounds.Left),-1=该房型不开
            internal int DropOffset = -1;
        }

        /// <summary>层内容主入口:规划→落位→刻画→装修→链边→脊接驳→撒布声明</summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            UnifiedRandom rand = WorldGen.genRand;
            LayerBand band = ctx.Band;

            //挂房地板:脊内膛顶上收5行,含壳+padding后恰好贴住P30预留的脊缓冲带
            int floorA = band.SpineInteriorTop - 5;
            //叠排上层地板:下排最大排高7+双侧壳4+双侧padding4之上
            int floorB = floorA - 15;

            //活跃宽度:中央1000~1200(ROOMS-INDEX §5),边缘留白全实心
            int xLeft = System.Math.Max(DungeonworldMetrics.BorderThick + 6, DungeonworldMetrics.SpawnX - 600);
            int xRight = System.Math.Min(DungeonworldMetrics.Width - DungeonworldMetrics.BorderThick - 6,
                DungeonworldMetrics.SpawnX + 600);

            List<NodeKind> sequence = RollSequence(rand, out int rowBudget);
            var placed = new List<PlacedNode>();
            int cursor = xLeft;
            int rowsBuilt = 0, doorsPlaced = 0, doorsFailed = 0, furnPlaced = 0, furnRejected = 0;
            int stackedPairs = 0;

            foreach (NodeKind kind in sequence) {
                if (cursor > xRight - 46) {
                    CWRMod.Instance.Logger.Warn($"[L2Content] 活跃区右缘耗尽,剩余节点跳过(cursor={cursor})");
                    break;
                }
                PlacedNode node = PlaceAndBuildNode(ctx, rand, kind, ref cursor, xRight, floorA,
                    ref doorsPlaced, ref doorsFailed, ref furnPlaced, ref furnRejected);
                if (node == null) {
                    continue;
                }
                placed.Add(node);
                ctx.Graph.Rooms.Add(node.Room);

                if (kind == NodeKind.CellRow) {
                    rowsBuilt++;
                    //双排层变体:活跃区中段允许上下两排叠放,楼梯井短接(ROOMS-L2 §1)
                    if (stackedPairs == 0 && rowsBuilt >= 2 && rand.NextBool(2)) {
                        PlacedNode upper = TryBuildStackedRow(ctx, rand, node, floorB,
                            ref furnPlaced, ref furnRejected, ref doorsPlaced, ref doorsFailed);
                        if (upper != null) {
                            placed.Add(upper);
                            ctx.Graph.Rooms.Add(upper.Room);
                            //上排→下排的层内楼梯井即一条图边
                            ctx.Graph.Edges.Add(new RoomEdge(ctx.Graph.Rooms.Count - 1,
                                ctx.Graph.Rooms.IndexOf(node.Room), SocketKind.PlatformGap, EdgeForm.StairWell));
                            stackedPairs++;
                            rowsBuilt++;
                        }
                    }
                }
            }

            int edges = RouteChainEdges(ctx, placed, floorA);
            int drops = RouteSpineDrops(ctx, placed, band);

            //层撒布装修数据声明,P55统一执行(契约纪律5)
            ctx.Scatter.AddRange(L2Scatter.Entries());

            CWRMod.Instance.Logger.Info(
                $"[L2Content] 牢狱层落成 nodes={placed.Count}(排x{rowsBuilt} 叠排x{stackedPairs})"
                + $" edges={edges} drops={drops} graphConnected={ctx.Graph.IsConnected()}(分量间由脊桥接,洪泛为准)"
                + $" 门={doorsPlaced}成/{doorsFailed}拒 家具={furnPlaced}成/{furnRejected}拒"
                + $" grid={ctx.Grid.ReserveOk}留/{ctx.Grid.ReserveReject}拒");
        }

        //==================== 节点序列(数量档:排6~8/看守3/长廊1~2/刑场1/登记1~2/机关1) ====================

        private static List<NodeKind> RollSequence(UnifiedRandom rand, out int rowBudget) {
            rowBudget = rand.Next(6, 9);
            var seq = new List<NodeKind> {
                NodeKind.CellRow,
                NodeKind.Guard,
                NodeKind.CellRow,
                NodeKind.Hall,
                NodeKind.CellRow,
                NodeKind.Gallery,
                NodeKind.Guard,
                NodeKind.TrapCorridor,
                NodeKind.Registry,
                NodeKind.CellRow,
                NodeKind.Guard,
                NodeKind.CellRow,
            };
            if (rand.NextBool(2)) {
                seq.Add(NodeKind.Gallery);
            }
            seq.Add(NodeKind.CellRow);
            if (rand.NextBool(2)) {
                seq.Add(NodeKind.Registry);
            }
            for (int extra = rowBudget - 6; extra > 0; extra--) {
                seq.Add(NodeKind.CellRow);
            }
            return seq;
        }

        //==================== 落位+刻画(预留失败=前进重试两轮,再失败弃节点fail loud) ====================

        private static PlacedNode PlaceAndBuildNode(LayerBuildContext ctx, UnifiedRandom rand,
            NodeKind kind, ref int cursor, int xRight, int floorA,
            ref int doorsPlaced, ref int doorsFailed, ref int furnPlaced, ref int furnRejected) {

            //先掷计划(尺寸冻结),再拿尺寸去预留
            L2CellRow.RowPlan rowPlan = default;
            Point size = kind switch {
                NodeKind.CellRow => L2CellRow.InteriorSize(rowPlan = L2CellRow.Roll(rand, allowTail: true)),
                NodeKind.Guard => L2Rooms.GuardInteriorSize(rand),
                NodeKind.Gallery => L2Rooms.GalleryInteriorSize(rand),
                NodeKind.Hall => L2Rooms.HallInteriorSize(rand),
                NodeKind.Registry => L2Rooms.RegistryInteriorSize(rand),
                _ => L2Rooms.TrapCorridorInteriorSize(rand),
            };

            RoomNode room = null;
            for (int wave = 0; wave < 3 && room == null; wave++) {
                int windowMax = System.Math.Min(cursor + size.X + 34, xRight);
                room = RoomPlacer.TryPlace(ctx.Grid, rand, cursor, windowMax, floorA, size, size, retries: 8);
                if (room == null) {
                    //窗口被禁室足印/竖井占满:前进跨越(足印宽66+padding,两轮共前进72)
                    cursor += wave == 0 ? 30 : 42;
                }
            }
            if (room == null) {
                CWRMod.Instance.Logger.Warn($"[L2Content] {kind}三轮未落位,弃(cursor={cursor})");
                return null;
            }
            cursor = room.Bounds.Right + rand.Next(4, 9);

            var node = new PlacedNode { Room = room, Kind = kind };
            switch (kind) {
                case NodeKind.CellRow: {
                    L2CellRow.RowReport r = L2CellRow.Build(room, rowPlan, rand);
                    doorsPlaced += r.DoorsPlaced;
                    doorsFailed += r.DoorsFailed;
                    furnPlaced += r.FurniturePlaced;
                    furnRejected += r.FurnitureRejected;
                    node.DeadEndRight = rowPlan.Tail != L2CellRow.TailKind.None;
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    break;
                }
                case NodeKind.Guard:
                    Tally(L2Rooms.BuildGuard(room, rand), ref furnPlaced, ref furnRejected);
                    break;
                case NodeKind.Gallery:
                    Tally(L2Rooms.BuildGallery(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    break;
                case NodeKind.Hall:
                    Tally(L2Rooms.BuildHall(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    break;
                case NodeKind.Registry:
                    Tally(L2Rooms.BuildRegistry(room, rand), ref furnPlaced, ref furnRejected);
                    break;
                default:
                    Tally(L2Rooms.BuildTrapCorridor(room, rand), ref furnPlaced, ref furnRejected);
                    //落口开在下厅右侧(梯口/罐位都在左与中段)
                    node.DropOffset = room.Bounds.Width - 5;
                    break;
            }
            return node;
        }

        private static void Tally(L2Rooms.Tally t, ref int placed, ref int rejected) {
            placed += t.Placed;
            rejected += t.Rejected;
        }

        //叠排:与下排左缘对齐落位,楼梯井穿下排天花短接(门厅列对门厅列)
        private static PlacedNode TryBuildStackedRow(LayerBuildContext ctx, UnifiedRandom rand,
            PlacedNode lower, int floorB,
            ref int furnPlaced, ref int furnRejected, ref int doorsPlaced, ref int doorsFailed) {

            L2CellRow.RowPlan plan = L2CellRow.Roll(rand, allowTail: false);
            plan.CellCount = System.Math.Min(plan.CellCount, 5);
            Point size = L2CellRow.InteriorSize(plan);
            //xMin==xMax-总宽:左缘强制对齐下排,保证楼梯井垂直落进下排门厅
            int totalW = size.X + DungeonworldMetrics.RoomShellThick * 2;
            RoomNode room = RoomPlacer.TryPlace(ctx.Grid, rand,
                lower.Room.Bounds.Left, lower.Room.Bounds.Left + totalW, floorB, size, size, retries: 4);
            if (room == null) {
                return null;
            }
            L2CellRow.RowReport r = L2CellRow.Build(room, plan, rand);
            doorsPlaced += r.DoorsPlaced;
            doorsFailed += r.DoorsFailed;
            furnPlaced += r.FurniturePlaced;
            furnRejected += r.FurnitureRejected;

            //上排门厅地板开口→楼梯井→下排门厅(穿下排天花属设计内短接)
            var gap = new DoorSocket(SocketSide.Bottom, DungeonworldMetrics.RoomShellThick,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            room.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(room, gap, lower.Room.FloorTop,
                L2Palette.PlatformFrameY, L2Palette.WallBase);
            return new PlacedNode { Room = room, Kind = NodeKind.CellRow, DropOffset = -1 };
        }

        //==================== 链边:相邻节点门对门/拱对拱,跨宏观足印的长廊禁走(契约纪律4) ====================

        private static int RouteChainEdges(LayerBuildContext ctx, List<PlacedNode> placed, int floorA) {
            int routed = 0;
            //只在地面层节点间配对(叠排悬在半空,由楼梯井短接,不入水平链)
            var ground = new List<int>();
            for (int i = 0; i < placed.Count; i++) {
                if (placed[i].Room.FloorTop == floorA) {
                    ground.Add(i);
                }
            }
            for (int g = 0; g + 1 < ground.Count; g++) {
                int i = ground[g];
                PlacedNode a = placed[i];
                PlacedNode b = placed[ground[g + 1]];
                //尽端排右侧封死(拷问室/藏物室收尾即死端)
                if (a.DeadEndRight) {
                    continue;
                }
                int gapL = a.Room.Bounds.Right;
                int gapR = b.Room.Bounds.Left;
                if (gapR - gapL > 40 || GapBlockedByMacro(gapL, gapR)) {
                    continue;
                }
                //大房用拱洞,小房用标准门(§2.1门插槽四类)
                bool archA = a.Kind is NodeKind.Hall or NodeKind.Gallery;
                bool archB = b.Kind is NodeKind.Hall or NodeKind.Gallery;
                DoorSocket sa = EdgeSocket(a.Room, SocketSide.Right, archA);
                DoorSocket sb = EdgeSocket(b.Room, SocketSide.Left, archB);
                a.Room.Sockets.Add(sa);
                b.Room.Sockets.Add(sb);
                if (!CorridorRouter.RouteDoorToDoor(a.Room, sa, b.Room, sb, L2Palette.WallBase)) {
                    continue;
                }
                //连接段地板/顶板换粉砖,消除M0蓝底接缝(带砖色A路换色后自然统一)
                for (int x = gapL; x < gapR; x++) {
                    TileBrush.SetSolid(x, floorA, L2Palette.Brick);
                    TileBrush.SetSolid(x, floorA - DungeonworldMetrics.CorridorClearance - 1, L2Palette.Brick);
                }
                ctx.Graph.Edges.Add(new RoomEdge(i, ground[g + 1], SocketKind.Door, EdgeForm.Horizontal));
                routed++;
            }
            return routed;
        }

        private static DoorSocket EdgeSocket(RoomNode room, SocketSide side, bool archway)
            => archway
                ? new DoorSocket(side, room.FloorTop - 4 - room.Bounds.Top, SocketKind.Archway, 4)
                : new DoorSocket(side, room.FloorTop - 3 - room.Bounds.Top, SocketKind.Door, 3);

        //宏观足印检测:主竖井列带与禁室包络(选址定点见GaolBossRoomSiting,P30已定)
        private static bool GapBlockedByMacro(int gapL, int gapR) {
            int shaftL = DungeonworldMetrics.ShaftLeft - 2;
            int shaftR = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 2;
            if (gapL < shaftR && gapR > shaftL) {
                return true;
            }
            if (GaolBossRoomSiting.LastOrigin is Point origin) {
                Rectangle boss = BossRooms.GaolBossRoom.Bounds(origin);
                if (gapL < boss.Right + 2 && gapR > boss.Left - 2) {
                    return true;
                }
            }
            return false;
        }

        //==================== 脊接驳:门厅地板楼梯井下探层脊(爬升11>坡道上限,规划期即选井形态) ====================

        private static int RouteSpineDrops(LayerBuildContext ctx, List<PlacedNode> placed, LayerBand band) {
            int drops = 0;
            var hasLeftEdge = new HashSet<int>();
            foreach (RoomEdge e in ctx.Graph.Edges) {
                if (e.Form == EdgeForm.Horizontal) {
                    hasLeftEdge.Add(System.Math.Max(e.A, e.B));
                }
            }
            for (int i = 0; i < placed.Count; i++) {
                PlacedNode node = placed[i];
                if (node.DropOffset < 0) {
                    continue;
                }
                //链头(左侧无边)必开;链中按节拍每3节点补一口,保证脊↔排的往返密度
                bool isChainHead = !hasLeftEdge.Contains(ctx.Graph.Rooms.IndexOf(node.Room));
                if (!isChainHead && i % 3 != 2) {
                    continue;
                }
                var gap = new DoorSocket(SocketSide.Bottom, node.DropOffset,
                    SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                node.Room.Sockets.Add(gap);
                CorridorRouter.RouteToFloorBelow(node.Room, gap, band.SpineFloorTop,
                    L2Palette.PlatformFrameY, L2Palette.WallBase);
                drops++;
            }
            //孤立分量兜底检查:无落口且无链边的节点=不可达,fail loud交P80复核
            for (int i = 0; i < placed.Count; i++) {
                PlacedNode node = placed[i];
                int idx = ctx.Graph.Rooms.IndexOf(node.Room);
                bool linked = node.DropOffset >= 0 || HasAnyEdge(ctx.Graph, idx);
                if (!linked) {
                    CWRMod.Instance.Logger.Error(
                        $"[L2Content] 节点{node.Kind}@{node.Room.Bounds}无链边且无落口,预计洪泛不可达,责任=L2规划器");
                }
            }
            return drops;
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
