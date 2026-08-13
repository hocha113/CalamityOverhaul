using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //L3免接线看样入口(镜像DungeonworldPreview/L2Preview惯例):任意世界脚下就地盖
    //不注册GenPass、不影响世界形态;仅单人调试用(联机不发tile同步);
    //触发TestItem片段见交付报告
    internal static class L3Preview
    {
        /// <summary>
        /// 看样1:检索廊+灯房(灭灯回路)+迷宫块(死端奖励)+抄写室(墨渍)。
        /// galleryFloor=玩家脚下地板行,originX=廊左缘。占地约200宽×56高。
        /// </summary>
        internal static void BuildArchiveSample(int originX, int galleryFloor, int seed = 1919) {
            WarnIfMultiplayer();
            L3Lights.ResetCounters();
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 8, galleryFloor - 48, 200, 56);

            Solidify(area);
            var grid = new OccupancyGrid(area);
            TileBrush.CarveRect(area.Left + 2, galleryFloor - 5, area.Right - 2, galleryFloor, L3Palette.WallBase);
            grid.MarkUnchecked(new Rectangle(area.Left, galleryFloor - 6, area.Width, area.Bottom - (galleryFloor - 6)));

            int floorRooms = galleryFloor - 10;
            int cursor = area.Left + 6;

            //灯房:3~5盏全灭+开关,本层独占玩法
            Point lampSize = L3Rooms.LampGalleryInteriorSize(rand);
            RoomNode lamp = RoomPlacer.TryPlace(grid, rand, cursor, cursor + lampSize.X + 8, floorRooms, lampSize, lampSize);
            if (lamp == null) {
                CWRMod.Instance.Logger.Error("[L3Preview] 灯房落位失败,区域被占用?");
                return;
            }
            L3Rooms.Tally lampTally = L3Rooms.BuildLampGallery(lamp, rand);
            cursor = lamp.Bounds.Right + 4;

            //迷宫:两行密架+风险格间,晒死端奖励与夹层书墙
            var mazePlan = new L3MazeBlock.MazePlan {
                RowCount = 2,
                RowHeights = [9, 9],
                Width = 48,
                Forbidden = false,
                RiskCell = true,
                SoggyBottom = false,
            };
            Point mazeSize = L3MazeBlock.InteriorSize(mazePlan);
            RoomNode maze = RoomPlacer.TryPlace(grid, rand, cursor, cursor + mazeSize.X + 8, floorRooms, mazeSize, mazeSize);
            if (maze == null) {
                CWRMod.Instance.Logger.Error("[L3Preview] 迷宫块落位失败(看样区宽度不足?)");
                return;
            }
            L3MazeBlock.MazeReport mazeRep = L3MazeBlock.Build(maze, mazePlan, rand);
            cursor = maze.Bounds.Right + 4;

            //抄写室:墨渍做旧原点
            Point scriptSize = L3Rooms.ScriptoriumInteriorSize(rand);
            RoomNode script = RoomPlacer.TryPlace(grid, rand, cursor, cursor + scriptSize.X + 8, floorRooms, scriptSize, scriptSize);
            if (script == null) {
                CWRMod.Instance.Logger.Error("[L3Preview] 抄写室落位失败");
                return;
            }
            L3Rooms.Tally scriptTally = L3Rooms.BuildScriptorium(script, rand);

            //链边:灯房-迷宫拱/门,迷宫-抄写室
            Chain(lamp, maze, archA: false, archB: true);
            Chain(maze, script, archA: true, archB: false);

            Drop(lamp, DungeonworldMetrics.RoomShellThick, galleryFloor);
            Drop(maze, mazeRep.DropOffset, galleryFloor);
            Drop(script, DungeonworldMetrics.RoomShellThick, galleryFloor);

            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L3Preview] 档案馆看样落成 lamp={lamp.Bounds} maze={maze.Bounds} script={script.Bounds}"
                + $" 灯房家具={lampTally.Placed}成/{lampTally.Rejected}拒"
                + $" 迷宫架={mazeRep.ShelvesPlaced}成/{mazeRep.ShelvesRejected}拒 死端奖={mazeRep.Rewards}"
                + $" 抄写={scriptTally.Placed}成/{scriptTally.Rejected}拒"
                + $" 灯=亮{L3Lights.LampsLit}/灭{L3Lights.LampsOff} 开关={L3Lights.SwitchesPlaced}");
        }

        /// <summary>
        /// 看样2:禁书区+钟声门面封条+灭灯收口。占地约80宽×40高。
        /// </summary>
        internal static void BuildVaultSample(int originX, int galleryFloor, int seed = 2026) {
            WarnIfMultiplayer();
            L3Lights.ResetCounters();
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 8, galleryFloor - 36, 80, 44);

            Solidify(area);
            var grid = new OccupancyGrid(area);
            TileBrush.CarveRect(area.Left + 2, galleryFloor - 5, area.Right - 2, galleryFloor, L3Palette.WallSlab);
            grid.MarkUnchecked(new Rectangle(area.Left, galleryFloor - 6, area.Width, area.Bottom - (galleryFloor - 6)));

            int floorRooms = galleryFloor - 10;
            Point vaultSize = L3Rooms.VaultInteriorSize(rand);
            vaultSize.Y = System.Math.Min(vaultSize.Y, 20);
            RoomNode vault = RoomPlacer.TryPlace(grid, rand, area.Left + 6, area.Right - 16, floorRooms, vaultSize, vaultSize);
            if (vault == null) {
                CWRMod.Instance.Logger.Error("[L3Preview] 禁书区落位失败,区域被占用?");
                return;
            }
            L3Rooms.Tally tally = L3Rooms.BuildVault(vault, rand);

            //右侧短廊+落口井+门面封条(镜像正式EnsureVaultApproach)
            DoorSocket arch = L3Rooms.FloorArch(vault, SocketSide.Right);
            vault.Sockets.Add(arch);
            CorridorRouter.OpenWallSocket(vault, arch, L3Palette.WallSlab);
            int corL = vault.Bounds.Right;
            TileBrush.CarveRect(corL, floorRooms - 4, corL + 5, floorRooms, L3Palette.WallSlab);
            int wellX = corL + 5;
            CorridorRouter.CarveStairWell(wellX, floorRooms, galleryFloor,
                L3Palette.PlatformFrameY, L3Palette.WallSlab);
            TileBrush.PlatformRow(wellX, wellX + DungeonworldMetrics.StairWellWidth, floorRooms,
                L3Palette.PlatformFrameY);
            L3Rooms.SealVaultEntrance(vault, arch);

            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L3Preview] 禁书区看样落成 vault={vault.Bounds} 家具={tally.Placed}成/{tally.Rejected}拒"
                + $" 灯=亮{L3Lights.LampsLit}/灭{L3Lights.LampsOff} 开关={L3Lights.SwitchesPlaced}");
        }

        private static void Solidify(Rectangle area) {
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L3Palette.Brick);
                }
            }
        }

        private static void Chain(RoomNode a, RoomNode b, bool archA, bool archB) {
            DoorSocket sa = archA ? L3Rooms.FloorArch(a, SocketSide.Right) : L3Rooms.FloorDoor(a, SocketSide.Right);
            DoorSocket sb = archB ? L3Rooms.FloorArch(b, SocketSide.Left) : L3Rooms.FloorDoor(b, SocketSide.Left);
            a.Sockets.Add(sa);
            b.Sockets.Add(sb);
            CorridorRouter.RouteDoorToDoor(a, sa, b, sb, L3Palette.WallBase);
        }

        private static void Drop(RoomNode room, int offset, int galleryFloor) {
            var gap = new DoorSocket(SocketSide.Bottom, offset,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            room.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(room, gap, galleryFloor,
                L3Palette.PlatformFrameY, L3Palette.WallBase);
        }

        private static void WarnIfMultiplayer() {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L3Preview] 看样入口仅单人调试用,联机不发tile同步");
            }
        }
    }
}
