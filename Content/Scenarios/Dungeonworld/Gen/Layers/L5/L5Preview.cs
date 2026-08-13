using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //L5免接线看样入口(镜像DungeonworldPreview/L2Preview惯例):任意世界脚下就地盖
    //"龛壁廊—游走坑道—骨柱大厅—亡灵集市(熄火)—坑陷阱两型并排",
    //不注册GenPass、不影响世界形态;仅单人调试用(联机不发tile同步);
    //触发TestItem片段见交付报告
    internal static class L5Preview
    {
        /// <summary>
        /// 在(originX, spineFloor)处铺看样条:spineFloor=迷你脊地板行(玩家脚下)。
        /// 占地约232宽×62高,请在平坦测试世界使用。层撒布由P55执行,本看样不含。
        /// </summary>
        internal static void BuildBonevaultSample(int originX, int spineFloor, int seed = 5151) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L5Preview] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 8, spineFloor - 50, 232, 62);
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L5Palette.Brick);
                }
            }

            int floorA = spineFloor - 22;
            var grid = new OccupancyGrid(area);
            //迷你脊只铺到坑场左侧,坑井下探不与脊抢足印
            int pitLeft = area.Left + 186;
            TileBrush.CarveRect(area.Left + 2, spineFloor - 6, pitLeft - 4, spineFloor, L5Palette.WallSlab);
            grid.MarkUnchecked(new Rectangle(area.Left, spineFloor - 7,
                pitLeft - 4 - area.Left, area.Bottom - (spineFloor - 7)));

            Point gSize = new(32, 9);
            int gMin = area.Left + 6;
            RoomNode gallery = RoomPlacer.TryPlace(grid, rand, gMin, gMin + gSize.X + 4, floorA, gSize, gSize);
            if (gallery == null) {
                CWRMod.Instance.Logger.Error("[L5Preview] 龛壁廊落位失败");
                return;
            }
            L5Rooms.RoomInfo gInfo = L5Rooms.BuildGallery(gallery, rand);

            Point hSize = new(40, 16);
            int hMin = gallery.Bounds.Right + 36;
            RoomNode hall = RoomPlacer.TryPlace(grid, rand, hMin, hMin + hSize.X + 4, floorA, hSize, hSize);
            if (hall == null) {
                CWRMod.Instance.Logger.Error("[L5Preview] 骨柱大厅落位失败");
                return;
            }
            L5Rooms.RoomInfo hInfo = L5Rooms.BuildHall(hall, rand);

            Point mSize = new(48, 16);
            int mMin = hall.Bounds.Right + 12;
            RoomNode market = RoomPlacer.TryPlace(grid, rand, mMin, mMin + mSize.X + 4, floorA, mSize, mSize);
            if (market == null) {
                CWRMod.Instance.Logger.Error("[L5Preview] 亡灵集市落位失败");
                return;
            }
            market.Role = RoomRole.Safe;
            L5Rooms.RoomInfo mInfo = L5Rooms.BuildMarket(market, rand);

            Point pSize = new(28, 8);
            int pMin = market.Bounds.Right + 8;
            RoomNode pits = RoomPlacer.TryPlace(grid, rand, pMin, pMin + pSize.X + 4, floorA, pSize, pSize);
            if (pits == null) {
                CWRMod.Instance.Logger.Error("[L5Preview] 坑陷阱场落位失败");
                return;
            }
            L5Rooms.StampAndCarve(pits, L5Palette.WallSlab);
            var pitTally = new L5Rooms.Tally();
            pitTally.Add(L5Palette.TryPlaceObject(pits.InteriorLeft + 2, pits.InteriorTop,
                TileID.HangingLanterns, L5Palette.LanternBone), "场灯", pits.InteriorLeft + 2, pits.InteriorTop);
            pitTally.Add(L5Palette.TryPlaceObject(pits.InteriorRight - 3, pits.InteriorTop,
                TileID.HangingLanterns, L5Palette.LanternBone), "场灯", pits.InteriorRight - 3, pits.InteriorTop);
            L5Rooms.StampShowcasePits(pits, grid, rand, ref pitTally);

            var bars = new List<Point>();
            int wander = RouteWander(gallery, hall, 4, 5, L5Palette.WallSlab, bars);
            int straight = 0;
            if (RouteStraight(hall, market, 5, 5, L5Palette.WallBase)) {
                straight++;
            }
            if (RouteStraight(market, pits, 5, 4, L5Palette.WallSlab)) {
                straight++;
            }
            int laid = L5TunnelCarver.FlushCrossbars(bars, L5Palette.PlatformBone);

            DropToSpine(gallery, gInfo.FloorGapOffset, spineFloor);
            DropToSpine(market, mInfo.FloorGapOffset, spineFloor);

            L5Palette.PlaceTombstone(gallery.InteriorLeft + 8, gallery.FloorTop - 1, rand);
            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L5Preview] 看样落成 gallery={gallery.Bounds} hall={hall.Bounds}"
                + $" market={market.Bounds} pits={pits.Bounds}"
                + $" 边:游走{wander}/直线{straight} 横档{laid}"
                + $" 厅家具={hInfo.Tally.Placed} 廊={gInfo.Tally.Placed}"
                + $" 市={mInfo.Tally.Placed} 坑={pitTally.Placed}");
        }

        private static int RouteWander(RoomNode a, RoomNode b, int archA, int archB,
            ushort wall, List<Point> bars) {
            int gapL = a.Bounds.Right, gapR = b.Bounds.Left;
            int floor = a.FloorTop;
            var sa = new DoorSocket(SocketSide.Right, floor - archA - a.Bounds.Top, SocketKind.Archway, archA);
            var sb = new DoorSocket(SocketSide.Left, floor - archB - b.Bounds.Top, SocketKind.Archway, archB);
            a.Sockets.Add(sa);
            b.Sockets.Add(sb);
            CorridorRouter.OpenWallSocket(a, sa, wall);
            CorridorRouter.OpenWallSocket(b, sb, wall);
            var env = new Rectangle(gapL - 2, floor - 16, gapR - gapL + 4, 24);
            var start = new Point(gapL + 5, floor - 4);
            var end = new Point(gapR - 6, floor - 4);
            L5TunnelCarver.TunnelReport report = L5TunnelCarver.Carve(env, start, end,
                L5TunnelCarver.Lateral(wall));
            bars.AddRange(report.Crossbars);
            return 1;
        }

        private static bool RouteStraight(RoomNode a, RoomNode b, int archA, int archB, ushort wall) {
            var sa = new DoorSocket(SocketSide.Right, a.FloorTop - archA - a.Bounds.Top, SocketKind.Archway, archA);
            var sb = new DoorSocket(SocketSide.Left, b.FloorTop - archB - b.Bounds.Top, SocketKind.Archway, archB);
            a.Sockets.Add(sa);
            b.Sockets.Add(sb);
            return CorridorRouter.RouteDoorToDoor(a, sa, b, sb, wall);
        }

        private static void DropToSpine(RoomNode room, int floorGapOffset, int spineFloor) {
            if (floorGapOffset < 0) {
                return;
            }
            var gap = new DoorSocket(SocketSide.Bottom, floorGapOffset,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            room.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(room, gap, spineFloor, L5Palette.PlatformPink, L5Palette.WallSlab);
        }
    }
}
