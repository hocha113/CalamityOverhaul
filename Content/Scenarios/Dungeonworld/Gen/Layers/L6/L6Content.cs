using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    /// <summary>
    /// L6铸造机关层内容入口(Wave-2接缝契约,契约全文见 LayerBuildContext 头注释)。
    /// <para/>管线路/父级一行接线(P50调度槽):
    /// <code>Layers.L6.L6Content.PlanAndBuild(LayerPlans.L6);</code>
    /// <para/>前置依赖:P30已建ctx(占用栅格含脊/主竖井/跨层预留足印),本入口只消费
    /// ctx.Grid/ctx.Graph/ctx.Scatter与冻结机器公开API,不触碰 Dungeonworld.cs
    /// 与任何pass文件;随机全走WorldGen.genRand(F22)。
    /// <para/>层结构:Z字下降6~7折(ROOMS-L6 §0)。主竖井左侧走Z字主链
    /// (折偶数L→R、奇数R→L,折间齿轮井/检修井竖降);井右侧补一条短链回脊,
    /// 不跨井架廊(契约纪律4)。密度按折序递增,末折走廊达峰,主控室之后
    /// 静默楼梯井落脊(L6→L7隔离带的层内半段)。
    /// </summary>
    internal static class L6Content
    {
        private enum NodeKind
        {
            CorrA, CorrB, Hall, Workshop, Vault, Control, Slag, Well,
        }

        private sealed class PlacedNode
        {
            internal RoomNode Room;
            internal NodeKind Kind;
            internal int GraphIndex;
            internal int Fold;
            internal int GateOffset = -1;
            internal int CenterX => Room.Bounds.Left + Room.Bounds.Width / 2;
        }

        private sealed class Caps
        {
            internal int Corridors, Halls, Workshops, Vaults, Wells, Gears, Controls, Slags;
        }

        /// <summary>层内容主入口:折规划→Z字布房→折间井→链边→脊接驳→右翼→撒布声明</summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            UnifiedRandom rand = WorldGen.genRand;
            LayerBand band = ctx.Band;
            L6MachineSlots.Reset();

            int shaftL = DungeonworldMetrics.ShaftLeft - 6;
            int shaftR = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 6;
            int xLeft = System.Math.Max(DungeonworldMetrics.PlayLeft + 8,
                DungeonworldMetrics.SpawnX - 780);
            int xRight = System.Math.Min(DungeonworldMetrics.PlayRight - 8,
                DungeonworldMetrics.SpawnX + 780);

            int foldCount = rand.Next(6, 8);
            int firstFloor = band.Top + 48;
            int lastFloor = band.SpineInteriorTop - 16;
            int gap = (lastFloor - firstFloor) / (foldCount - 1);
            var floors = new int[foldCount];
            for (int i = 0; i < foldCount; i++) {
                floors[i] = firstFloor + i * gap;
            }

            var caps = new Caps();
            var placed = new List<PlacedNode>();
            int furnPlaced = 0, furnRejected = 0;
            int chains = 0, wellsBuilt = 0, upperLinks = 0;

            PlacedNode prevWell = null;
            for (int f = 0; f < foldCount; f++) {
                bool leftToRight = (f & 1) == 0;
                bool last = f == foldCount - 1;
                int tier = ThreatTier(f, foldCount);
                var foldNodes = new List<PlacedNode>();
                if (prevWell != null) {
                    foldNodes.Add(prevWell);
                }

                int cursor = leftToRight ? xLeft + rand.Next(0, 10) : shaftL - rand.Next(0, 10);
                int dir = leftToRight ? 1 : -1;
                if (prevWell != null) {
                    cursor = leftToRight
                        ? prevWell.Room.Bounds.Right + rand.Next(4, 8)
                        : prevWell.Room.Bounds.Left - rand.Next(4, 8);
                }

                PlacedNode chainEnd = prevWell;
                foreach (NodeKind kind in RollFoldSequence(f, last, caps, rand)) {
                    PlacedNode node = PlaceAndBuildNode(ctx, rand, kind, f, tier,
                        ref cursor, dir, xLeft, shaftL, floors[f], caps,
                        ref furnPlaced, ref furnRejected);
                    if (node == null) {
                        continue;
                    }
                    node.GraphIndex = ctx.Graph.Rooms.Count;
                    ctx.Graph.Rooms.Add(node.Room);
                    foldNodes.Add(node);
                    placed.Add(node);
                    chainEnd = node;
                }

                chains += RouteChain(ctx, foldNodes);

                if (last || chainEnd == null) {
                    prevWell = null;
                    continue;
                }

                bool gear = (f & 1) == 0 && caps.Gears < 4;
                PlacedNode well = PlaceWell(ctx, rand, chainEnd, leftToRight,
                    xLeft, shaftL, floors[f], floors[f + 1], gear, caps,
                    ref furnPlaced, ref furnRejected);
                if (well == null) {
                    CWRMod.Instance.Logger.Error(
                        $"[L6Content] 折{f}→{f + 1}竖井落位失败,Z字在此断开,责任=L6规划器");
                    prevWell = null;
                    continue;
                }
                well.GraphIndex = ctx.Graph.Rooms.Count;
                ctx.Graph.Rooms.Add(well.Room);
                placed.Add(well);
                wellsBuilt++;
                if (ConnectUpper(ctx, chainEnd, well, floors[f], leftToRight)) {
                    upperLinks++;
                }
                prevWell = well;
            }

            int drops = RouteSpineDrops(ctx, placed, band);
            int rightWing = PlaceRightWing(ctx, rand, shaftR, xRight,
                floors[foldCount / 2], band, caps, placed, ref furnPlaced, ref furnRejected);

            //巨像装配湾:层流末端、RustWash之前接线(WAVE2-BUILDINGS §3.4,湾体吃全带锈橙层染)
            L6Colossus.TryBuild(ctx, floors, xLeft, xRight, rand);

            //墙变体收尾:蓝基约5%的小点缀盘。§0声明的Tiled75/Slab20/Base5三档里,
            //Base那一档此前从来没落地过,墙面只有两种在轮换
            int bandLeft = DungeonworldMetrics.PlayLeft;
            int bandRight = DungeonworldMetrics.PlayRight;
            for (int d = 0; d < 16; d++) {
                L6Palette.WallDisk(rand.Next(bandLeft, bandRight),
                    rand.Next(band.Top + 20, band.SpineInteriorTop - 4),
                    rand.Next(5, 12), L6Palette.WallBase);
            }

            //基调层染:炉锈橙洗全带,Cog机件刷亮橙。
            //ROOMS-L6 §0把锈橙身份交给了paint,此前只实装了黑灰焦油,整层读起来跟L3/L7一样是素蓝
            (LayerTint.TintReport rust, int hotCogs) = L6Palette.RustWash(
                new Rectangle(bandLeft, band.Top, bandRight - bandLeft, band.Bottom - band.Top));

            ctx.Scatter.AddRange(L6Scatter.Entries());
            L6MachineSlots.LogAll();

            foreach (PlacedNode node in placed) {
                if (!HasAnyEdge(ctx.Graph, node.GraphIndex) && node.Kind != NodeKind.Well) {
                    CWRMod.Instance.Logger.Error(
                        $"[L6Content] 节点{node.Kind}@{node.Room.Bounds}无链边且无落口,预计洪泛不可达,责任=L6规划器");
                }
            }
            if (caps.Corridors < 10 || caps.Halls < 2 || caps.Vaults < 3 || caps.Controls < 1) {
                CWRMod.Instance.Logger.Warn(
                    $"[L6Content] 数量档低于花名册下限:廊{caps.Corridors}/10 厅{caps.Halls}/2"
                    + $" 库{caps.Vaults}/3 主控{caps.Controls}/1,查占用栅格拒绝量");
            }

            CWRMod.Instance.Logger.Info(
                $"[L6Content] 铸造场落成 folds={foldCount} nodes={placed.Count}"
                + $"(廊{caps.Corridors} 厅{caps.Halls} 车间{caps.Workshops} 库{caps.Vaults}"
                + $" 井{caps.Wells}(齿轮{caps.Gears}) 渣{caps.Slags} 主控{caps.Controls})"
                + $" chains={chains} 折间上口={upperLinks} 竖井={wellsBuilt} 脊落口={drops} 右翼={rightWing}"
                + $" 炉锈橙层染={rust} 热机件={hotCogs}"
                + $" 家具={furnPlaced}成/{furnRejected}拒 留位={L6MachineSlots.Slots.Count}"
                + $" graphConnected={ctx.Graph.IsConnected()}(分量间由脊/主井桥接,洪泛为准)"
                + $" grid={ctx.Grid.ReserveOk}留/{ctx.Grid.ReserveReject}拒");
        }

        //末两折达峰;折0教学档,折1解禁交叉射界/活塞
        private static int ThreatTier(int fold, int foldCount)
            => fold == 0 ? 0 : fold == 1 ? 1 : fold >= foldCount - 2 ? 3 : 2;

        //每折心愿单(井另放)。#8井站/#9忏悔室=公共构件,本层不做;#11巡逻二现待定跳过
        private static List<NodeKind> RollFoldSequence(int fold, bool last, Caps caps, UnifiedRandom rand) {
            var seq = new List<NodeKind>();
            if (fold == 0) {
                seq.Add(NodeKind.Slag);
                seq.Add(NodeKind.Workshop);
                seq.Add(NodeKind.CorrA);
                seq.Add(NodeKind.CorrA);
                seq.Add(NodeKind.Workshop);
                return seq;
            }
            if (last) {
                seq.Add(NodeKind.CorrA);
                seq.Add(NodeKind.CorrB);
                seq.Add(NodeKind.Vault);
                seq.Add(NodeKind.Control);
                return seq;
            }
            if (fold == 2 || (fold == 4 && caps.Halls < 3)) {
                seq.Add(NodeKind.Hall);
            }
            seq.Add(NodeKind.CorrA);
            if (fold >= 2) {
                seq.Add(NodeKind.CorrB);
            }
            else {
                seq.Add(NodeKind.CorrA);
            }
            if (fold >= 2 && caps.Vaults < 5) {
                seq.Add(NodeKind.Vault);
            }
            if (fold is 1 or 3 || rand.NextBool(3)) {
                seq.Add(NodeKind.Workshop);
            }
            if (fold == 4) {
                seq.Add(NodeKind.CorrB);
            }
            return seq;
        }

        //==================== 落位+刻画 ====================

        private static PlacedNode PlaceAndBuildNode(LayerBuildContext ctx, UnifiedRandom rand,
            NodeKind kind, int fold, int tier, ref int cursor, int dir, int xLo, int xHi,
            int floor, Caps caps, ref int furnPlaced, ref int furnRejected) {

            L6Rooms.CorridorPlanA planA = default;
            L6Rooms.CorridorPlanB planB = default;
            Point size = kind switch {
                NodeKind.CorrA => L6Rooms.CorridorAInteriorSize(planA = L6Rooms.RollCorridorA(rand, tier)),
                NodeKind.CorrB => L6Rooms.CorridorBInteriorSize(planB = L6Rooms.RollCorridorB(rand, tier)),
                NodeKind.Hall => L6Rooms.HallInteriorSize(rand),
                NodeKind.Workshop => L6Rooms.WorkshopInteriorSize(rand),
                NodeKind.Vault => L6Rooms.VaultInteriorSize(rand),
                NodeKind.Control => L6Rooms.ControlInteriorSize(rand),
                NodeKind.Slag => L6Rooms.SlagInteriorSize(rand),
                _ => new Point(8, 8),
            };
            int shell = DungeonworldMetrics.RoomShellThick;
            int totalW = size.X + shell * 2;
            int xMin = dir > 0 ? cursor : cursor - totalW;
            int xMax = dir > 0 ? cursor + totalW : cursor;
            xMin = System.Math.Max(xMin, xLo);
            xMax = System.Math.Min(xMax, xHi);

            RoomNode room = null;
            for (int wave = 0; wave < 3 && room == null; wave++) {
                if (xMax - xMin < totalW) {
                    cursor += dir * (wave == 0 ? 20 : 36);
                    xMin = dir > 0 ? cursor : cursor - totalW;
                    xMax = dir > 0 ? cursor + totalW : cursor;
                    xMin = System.Math.Max(xMin, xLo);
                    xMax = System.Math.Min(xMax, xHi);
                    continue;
                }
                room = RoomPlacer.TryPlace(ctx.Grid, rand, xMin, xMax, floor, size, size, retries: 8);
                if (room == null) {
                    cursor += dir * (wave == 0 ? 20 : 36);
                    xMin = dir > 0 ? cursor : cursor - totalW;
                    xMax = dir > 0 ? cursor + totalW : cursor;
                    xMin = System.Math.Max(xMin, xLo);
                    xMax = System.Math.Min(xMax, xHi);
                }
            }
            if (room == null) {
                CWRMod.Instance.Logger.Warn($"[L6Content] {kind}三轮未落位,弃(折{fold} cursor={cursor})");
                return null;
            }
            cursor = dir > 0 ? room.Bounds.Right + rand.Next(4, 8) : room.Bounds.Left - rand.Next(4, 8);

            var node = new PlacedNode { Room = room, Kind = kind, Fold = fold };
            L6Rooms.Tally tally;
            switch (kind) {
                case NodeKind.CorrA:
                    tally = L6Rooms.BuildCorridorA(room, planA, rand);
                    caps.Corridors++;
                    break;
                case NodeKind.CorrB:
                    tally = L6Rooms.BuildCorridorB(room, planB, rand);
                    caps.Corridors++;
                    break;
                case NodeKind.Hall:
                    tally = L6Rooms.BuildFoundryHall(room, rand);
                    caps.Halls++;
                    break;
                case NodeKind.Workshop: {
                    string sign = fold == 0 && caps.Workshops == 0 ? L6Rooms.SignEpitaph : null;
                    tally = L6Rooms.BuildWorkshop(room, rand, sign);
                    caps.Workshops++;
                    break;
                }
                case NodeKind.Vault:
                    room.Role = RoomRole.Treasure;
                    tally = L6Rooms.BuildTrialVault(room, rand);
                    caps.Vaults++;
                    break;
                case NodeKind.Control:
                    room.Role = RoomRole.Exit;
                    tally = L6Rooms.BuildControlRoom(room, rand, out node.GateOffset);
                    caps.Controls++;
                    break;
                default:
                    tally = L6Rooms.BuildSlagHall(room, rand);
                    caps.Slags++;
                    break;
            }
            furnPlaced += tally.Placed;
            furnRejected += tally.Rejected;
            return node;
        }

        private static PlacedNode PlaceWell(LayerBuildContext ctx, UnifiedRandom rand,
            PlacedNode chainEnd, bool placeToRight, int xLo, int xHi,
            int upperFloor, int lowerFloor, bool gear, Caps caps,
            ref int furnPlaced, ref int furnRejected) {

            int drop = lowerFloor - upperFloor;
            Point size = L6Rooms.WellInteriorSize(gear, drop);
            int shell = DungeonworldMetrics.RoomShellThick;
            int totalW = size.X + shell * 2;
            int cursor = placeToRight ? chainEnd.Room.Bounds.Right + 4 : chainEnd.Room.Bounds.Left - 4;
            int xMin = placeToRight ? cursor : cursor - totalW;
            int xMax = placeToRight ? cursor + totalW : cursor;
            xMin = System.Math.Max(xMin, xLo);
            xMax = System.Math.Min(xMax, xHi);
            if (xMax - xMin < totalW) {
                return null;
            }
            RoomNode room = RoomPlacer.TryPlace(ctx.Grid, rand, xMin, xMax, lowerFloor, size, size, retries: 8);
            if (room == null) {
                return null;
            }
            L6Rooms.Tally tally = L6Rooms.BuildWell(room, upperFloor, gear, rand);
            furnPlaced += tally.Placed;
            furnRejected += tally.Rejected;
            caps.Wells++;
            if (gear) {
                caps.Gears++;
            }
            return new PlacedNode { Room = room, Kind = NodeKind.Well, Fold = chainEnd.Fold };
        }

        //==================== 链边 / 折间上口 / 脊接驳 ====================

        private static int RouteChain(LayerBuildContext ctx, List<PlacedNode> foldNodes) {
            if (foldNodes.Count < 2) {
                return 0;
            }
            foldNodes.Sort((a, b) => a.Room.Bounds.Left.CompareTo(b.Room.Bounds.Left));
            int routed = 0;
            for (int i = 0; i + 1 < foldNodes.Count; i++) {
                PlacedNode a = foldNodes[i];
                PlacedNode b = foldNodes[i + 1];
                if (a.Room.FloorTop != b.Room.FloorTop) {
                    continue;
                }
                int gapL = a.Room.Bounds.Right;
                int gapR = b.Room.Bounds.Left;
                if (gapR - gapL > 40 || HitsShaft(gapL, gapR)) {
                    continue;
                }
                bool archA = a.Kind is NodeKind.Hall or NodeKind.Slag or NodeKind.Well;
                bool archB = b.Kind is NodeKind.Hall or NodeKind.Slag or NodeKind.Well;
                DoorSocket sa = archA ? L6Rooms.FloorArch(a.Room, SocketSide.Right)
                    : L6Rooms.FloorDoor(a.Room, SocketSide.Right);
                DoorSocket sb = archB ? L6Rooms.FloorArch(b.Room, SocketSide.Left)
                    : L6Rooms.FloorDoor(b.Room, SocketSide.Left);
                a.Room.Sockets.Add(sa);
                b.Room.Sockets.Add(sb);
                if (!CorridorRouter.RouteDoorToDoor(a.Room, sa, b.Room, sb, L6Palette.WallTiled)) {
                    continue;
                }
                //机关廊两端上门板控节奏(ROOMS-L6 §1)
                if (a.Kind is NodeKind.CorrA or NodeKind.CorrB) {
                    L6Palette.PlaceDoorPlate(a.Room.Bounds.Right - 2, a.Room.FloorTop - 1);
                }
                if (b.Kind is NodeKind.CorrA or NodeKind.CorrB) {
                    L6Palette.PlaceDoorPlate(b.Room.Bounds.Left + 1, b.Room.FloorTop - 1);
                }
                ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                    archA || archB ? SocketKind.Archway : SocketKind.Door, EdgeForm.Horizontal));
                routed++;
            }
            return routed;
        }

        //折间井上口:行走行=上折地板,与井的FloorTop(下折)不同,不能走RouteDoorToDoor
        private static bool ConnectUpper(LayerBuildContext ctx, PlacedNode room, PlacedNode well,
            int walkFloor, bool roomOnLeft) {
            int gapL = roomOnLeft ? room.Room.Bounds.Right : well.Room.Bounds.Right;
            int gapR = roomOnLeft ? well.Room.Bounds.Left : room.Room.Bounds.Left;
            if (gapR - gapL > 40 || HitsShaft(gapL, gapR)) {
                CWRMod.Instance.Logger.Warn($"[L6Content] 折间上口缝过宽/撞井,弃 {room.Room.Bounds}→{well.Room.Bounds}");
                return false;
            }
            SocketSide roomSide = roomOnLeft ? SocketSide.Right : SocketSide.Left;
            SocketSide wellSide = roomOnLeft ? SocketSide.Left : SocketSide.Right;
            DoorSocket sa = L6Rooms.DoorAtFloor(room.Room, roomSide, walkFloor);
            DoorSocket sb = L6Rooms.DoorAtFloor(well.Room, wellSide, walkFloor);
            room.Room.Sockets.Add(sa);
            well.Room.Sockets.Add(sb);
            CorridorRouter.OpenWallSocket(room.Room, sa, L6Palette.WallTiled);
            CorridorRouter.OpenWallSocket(well.Room, sb, L6Palette.WallTiled);
            RoomNode leftRoom = roomOnLeft ? room.Room : well.Room;
            RoomNode rightRoom = roomOnLeft ? well.Room : room.Room;
            CorridorRouter.CarveHorizontal(leftRoom.Bounds.Right, rightRoom.Bounds.Left,
                walkFloor, L6Palette.WallTiled);
            if (room.Kind is NodeKind.CorrA or NodeKind.CorrB) {
                int doorX = roomOnLeft ? room.Room.Bounds.Right - 2 : room.Room.Bounds.Left + 1;
                L6Palette.PlaceDoorPlate(doorX, walkFloor - 1);
            }
            ctx.Graph.Edges.Add(new RoomEdge(room.GraphIndex, well.GraphIndex,
                SocketKind.Door, EdgeForm.StairWell));
            return true;
        }

        //主控室静默落脊(机关骤降为零的下行通路)+孤立节点兜底
        private static int RouteSpineDrops(LayerBuildContext ctx, List<PlacedNode> placed, LayerBand band) {
            int drops = 0;
            foreach (PlacedNode node in placed) {
                if (node.Kind != NodeKind.Control) {
                    continue;
                }
                int offset = node.Room.Bounds.Width / 2 - 1;
                if (node.GateOffset >= 0 && System.Math.Abs(offset - node.GateOffset) < 6) {
                    offset = DungeonworldMetrics.RoomShellThick + 1;
                }
                var gap = new DoorSocket(SocketSide.Bottom, offset,
                    SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                node.Room.Sockets.Add(gap);
                CorridorRouter.RouteToFloorBelow(node.Room, gap, band.SpineFloorTop,
                    L6Palette.PlatformFrameY, L6Palette.WallTiled);
                ctx.Graph.Edges.Add(new RoomEdge(node.GraphIndex, node.GraphIndex,
                    SocketKind.PlatformGap, EdgeForm.StairWell));
                drops++;
            }
            return drops;
        }

        //右翼:主竖井右侧短链(大厅+车间),楼梯井回脊,不跨井(契约纪律4)
        private static int PlaceRightWing(LayerBuildContext ctx, UnifiedRandom rand,
            int xLo, int xHi, int floor, LayerBand band, Caps caps, List<PlacedNode> placed,
            ref int furnPlaced, ref int furnRejected) {

            int cursor = xLo + rand.Next(4, 12);
            int dir = 1;
            int added = 0;
            foreach (NodeKind kind in new[] { NodeKind.Hall, NodeKind.Workshop }) {
                if (kind == NodeKind.Hall && caps.Halls >= 3) {
                    continue;
                }
                PlacedNode node = PlaceAndBuildNode(ctx, rand, kind, fold: -1, tier: 1,
                    ref cursor, dir, xLo, xHi, floor, caps, ref furnPlaced, ref furnRejected);
                if (node == null) {
                    continue;
                }
                node.GraphIndex = ctx.Graph.Rooms.Count;
                ctx.Graph.Rooms.Add(node.Room);
                placed.Add(node);
                added++;
                var gap = new DoorSocket(SocketSide.Bottom, DungeonworldMetrics.RoomShellThick,
                    SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                node.Room.Sockets.Add(gap);
                CorridorRouter.RouteToFloorBelow(node.Room, gap, band.SpineFloorTop,
                    L6Palette.PlatformFrameY, L6Palette.WallTiled);
            }
            if (added >= 2) {
                RouteChain(ctx, placed.GetRange(placed.Count - added, added));
            }
            return added;
        }

        private static bool HitsShaft(int left, int right)
            => left < DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 3
            && right > DungeonworldMetrics.ShaftLeft - 3;

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
