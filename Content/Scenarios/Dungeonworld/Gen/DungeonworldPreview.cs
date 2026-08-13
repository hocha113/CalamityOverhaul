using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //M1机器的免接线看样入口:在任意世界就地盖出机器产物供预览,
    //不注册任何GenPass,不影响M0世界形态;仅单人调试用(联机不发tile同步)
    //触发方式见TestItem片段(交付报告),镜像"脚下就地构建"的调试惯例
    internal static class DungeonworldPreview
    {
        private const ushort Brick = TileID.BlueDungeonBrick;
        private const ushort Wall = WallID.BlueDungeonUnsafe;

        //===看样1:教堂草案prefab+其垂直镜像对照(左正右倒),验证§2.3镜像五层法===
        //验证点:镜像后D插槽跟随翻转、slope对偶(1↔3,2↔4)、吊灯↔烛台对偶换槽、
        //长椅/祭坛/钟锚被对偶表判删除(看MirrorDropped日志)、门板槽原样保留(F4对称)
        internal static void BuildChapelMirrorPair(int left, int bottom) {
            WarnIfMultiplayer();
            Prefab chapel = ChapelDraftPrefabs.Chapel;
            Prefab inverted = chapel.FlipY();
            int top = bottom - chapel.Height;
            int rightLeft = left + chapel.Width + 6;

            chapel.StampGeometry(left, top, Brick, Wall, DungeonworldMetrics.PlatformFrameY);
            inverted.StampGeometry(rightLeft, top, Brick, Wall, DungeonworldMetrics.PlatformFrameY);
            //几何冻结后再落家具(§3.1-3装修单向性,与正式pipeline同序)
            FrameArea(new Rectangle(left, top, chapel.Width * 2 + 6, chapel.Height));
            FurnishReport normal = chapel.PlaceFurniture(left, top);
            FurnishReport mirror = inverted.PlaceFurniture(rightLeft, top);

            CWRMod.Instance.Logger.Info(
                $"[DungeonworldPreview] 教堂草案对照 正:placed={normal.Placed} rejected={normal.Rejected}"
                + $" markers={normal.Markers} | 倒:placed={mirror.Placed} rejected={mirror.Rejected}"
                + $" markers={mirror.Markers} mirrorDropped={inverted.MirrorDroppedSlots}"
                + $" sockets(正)={chapel.Sockets.Count} sockets(倒)={inverted.Sockets.Count}");
        }

        //===看样2:样例房间图一角(脊走廊+三间梳齿挂房+三形态走廊)===
        //房1:PlatformGap→楼梯井(爬升18);房2:侧壁Door→坡道(爬升6);
        //房2↔房3:门对门水平走廊;图=链+环边,数据层连通自检落日志
        internal static void BuildSampleRooms(int originX, int spineFloor, int seed = 1337) {
            WarnIfMultiplayer();
            //预览用独立种子保证复现;正式gen pass一律WorldGen.genRand(F22)
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX, spineFloor - 34, 120, 42);

            //模拟gen前提:整块区域先浇实,机器在实心里开凿
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, Brick);
                }
            }

            var grid = new OccupancyGrid(area);
            CorridorRouter.ResetCounters();

            //脊走廊段(净高6镜像主脊规格)+占用登记
            var spineRect = new Rectangle(area.Left + 2, spineFloor - 6, area.Width - 4, 6);
            TileBrush.CarveRect(spineRect.Left, spineRect.Top, spineRect.Right, spineRect.Bottom, Wall);
            grid.MarkUnchecked(new Rectangle(spineRect.X, spineRect.Y, spineRect.Width, spineRect.Height + 2));

            //x窗口彼此隔开:房1竖井/房2左坡道/房2右走廊的刻画区不交叉
            var graph = new RoomGraph();
            RoomNode r1 = PlaceAndCarve(grid, rand, area.Left + 6, area.Left + 32,
                spineFloor - 18, new Point(10, 6), new Point(14, 8));
            RoomNode r2 = PlaceAndCarve(grid, rand, area.Left + 46, area.Left + 60,
                spineFloor - 6, new Point(8, 5), new Point(12, 6));
            RoomNode r3 = PlaceAndCarve(grid, rand, area.Left + 82, area.Left + 104,
                spineFloor - 6, new Point(8, 5), new Point(12, 6));
            if (r1 == null || r2 == null || r3 == null) {
                CWRMod.Instance.Logger.Error("[DungeonworldPreview] 样例房间落位失败,区域被占用?");
                return;
            }
            graph.Rooms.Add(r1);
            graph.Rooms.Add(r2);
            graph.Rooms.Add(r3);
            graph.ConnectAsChain(SocketKind.Door, EdgeForm.Horizontal);
            int loops = graph.AddLoopEdges(rand, 1);

            //房1:地板PlatformGap下楼梯井到脊
            var gap = new DoorSocket(SocketSide.Bottom,
                (r1.Bounds.Width - DungeonworldMetrics.StairWellWidth) / 2,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            r1.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(r1, gap, spineFloor, DungeonworldMetrics.PlatformFrameY, Wall);

            //房2:左壁Door坡道下到脊(爬升6≤RampMaxRise);
            //右壁留给通往房3的水平走廊,避免坡道在走廊地板上开坑断路
            DoorSocket leftDoor = FloorDoor(r2, SocketSide.Left);
            r2.Sockets.Add(leftDoor);
            CorridorRouter.RouteToFloorBelow(r2, leftDoor, spineFloor, DungeonworldMetrics.PlatformFrameY, Wall);

            //房2↔房3:门对门水平走廊(地板齐平,链边的直线形态)
            DoorSocket r2Out = FloorDoor(r2, SocketSide.Right);
            DoorSocket r3In = FloorDoor(r3, SocketSide.Left);
            r2.Sockets.Add(r2Out);
            r3.Sockets.Add(r3In);
            CorridorRouter.RouteDoorToDoor(r2, r2Out, r3, r3In, Wall);

            FrameArea(area);
            CWRMod.Instance.Logger.Info(
                $"[DungeonworldPreview] 样例房间图 rooms={graph.Rooms.Count} edges={graph.Edges.Count}"
                + $"(含环边{loops}) connected={graph.IsConnected()}"
                + $" reserveOk={grid.ReserveOk} reject={grid.ReserveReject}"
                + $" 路由 H={CorridorRouter.RoutedHorizontal} Ramp={CorridorRouter.RoutedRamp}"
                + $" Stair={CorridorRouter.RoutedStairWell}");
        }

        //落位+开凿内膛(壳保持实心,§2.1矩形包络)
        private static RoomNode PlaceAndCarve(OccupancyGrid grid, UnifiedRandom rand,
            int xMin, int xMax, int floorTop, Point interiorMin, Point interiorMax) {
            RoomNode room = RoomPlacer.TryPlace(grid, rand, xMin, xMax, floorTop, interiorMin, interiorMax);
            if (room != null) {
                TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, Wall);
            }
            return room;
        }

        //地板级标准门插槽(开口3高,底与地板齐平,§2.5接缝规则1)
        private static DoorSocket FloorDoor(RoomNode room, SocketSide side)
            => new(side, room.FloorTop - 3 - room.Bounds.Top, SocketKind.Door, 3);

        private static void FrameArea(Rectangle area)
            => WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);

        private static void WarnIfMultiplayer() {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[DungeonworldPreview] 看样入口仅单人调试用,联机不发tile同步");
            }
        }
    }
}
