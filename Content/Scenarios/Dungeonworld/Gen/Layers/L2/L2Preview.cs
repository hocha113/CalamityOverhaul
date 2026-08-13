using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    //L2免接线看样入口(镜像DungeonworldPreview惯例):任意世界脚下就地盖
    //"囚室排一段(含牢栅藏物尾段)+教学机关廊一段+迷你脊走廊",不注册GenPass、
    //不影响世界形态;仅单人调试用(联机不发tile同步);触发TestItem片段见交付报告
    internal static class L2Preview
    {
        /// <summary>
        /// 在(originX, spineFloor)处铺看样条:spineFloor=迷你脊地板行(玩家脚下),
        /// 上方悬挂囚室排与教学机关廊,楼梯井下探回脊,垂直关系1:1镜像正式管线。
        /// </summary>
        internal static void BuildPrisonSample(int originX, int spineFloor, int seed = 1919) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L2Preview] 看样入口仅单人调试用,联机不发tile同步");
            }
            //预览用独立种子保证复现;正式gen走WorldGen.genRand(F22)
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 8, spineFloor - 38, 136, 46);

            //整块浇实粉砖,机器在实心里开凿(模拟gen前提)
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L2Palette.Brick);
                }
            }
            //迷你脊走廊(净高6镜像主脊)+占用登记(脊带+顶板缓冲1行)
            var grid = new OccupancyGrid(area);
            TileBrush.CarveRect(area.Left + 2, spineFloor - 6, area.Right - 2, spineFloor, L2Palette.WallBase);
            grid.MarkUnchecked(new Rectangle(area.Left, spineFloor - 7, area.Width, area.Bottom - (spineFloor - 7)));

            //挂房地板与正式管线同差:脊内膛顶(spineFloor-6)上收5行→爬升11走楼梯井
            int floorA = spineFloor - 11;

            //--教学机关廊在左(囚室排的藏物尾段贴右壳,机关廊必须接在排的左侧才不破开密室)
            Point trapSize = L2Rooms.TrapCorridorInteriorSize(rand);
            RoomNode trap = RoomPlacer.TryPlace(grid, rand, area.Left + 4,
                area.Left + 4 + trapSize.X + 6, floorA, trapSize, trapSize);
            if (trap == null) {
                CWRMod.Instance.Logger.Error("[L2Preview] 机关廊落位失败,区域被占用?");
                return;
            }
            L2Rooms.Tally trapTally = L2Rooms.BuildTrapCorridor(trap, rand);

            //--囚室排在右:固定5室+牢栅藏物尾段(把最难的口形态全部晒出来)
            L2CellRow.RowPlan plan = L2CellRow.Roll(rand, allowTail: false);
            plan.CellCount = 5;
            plan.RowHeight = System.Math.Max(plan.RowHeight, 6);
            plan.Tail = L2CellRow.TailKind.Showcase;
            Point rowSize = L2CellRow.InteriorSize(plan);
            RoomNode row = RoomPlacer.TryPlace(grid, rand, trap.Bounds.Right + 4,
                trap.Bounds.Right + 4 + rowSize.X + 6, floorA, rowSize, rowSize);
            if (row == null) {
                CWRMod.Instance.Logger.Error("[L2Preview] 囚室排落位失败(看样区宽度不足?)");
                return;
            }
            L2CellRow.RowReport rowReport = L2CellRow.Build(row, plan, rand);

            //链边:机关廊右门↔排左门,门对门水平走廊(地板齐平)
            var sa = new DoorSocket(SocketSide.Right, trap.FloorTop - 3 - trap.Bounds.Top, SocketKind.Door, 3);
            var sb = new DoorSocket(SocketSide.Left, row.FloorTop - 3 - row.Bounds.Top, SocketKind.Door, 3);
            trap.Sockets.Add(sa);
            row.Sockets.Add(sb);
            CorridorRouter.RouteDoorToDoor(trap, sa, row, sb, L2Palette.WallBase);

            //脊接驳:机关廊下厅右端+排左门厅各开一口楼梯井(与正式drop同形态)
            var trapGap = new DoorSocket(SocketSide.Bottom, trap.Bounds.Width - 5,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            trap.Sockets.Add(trapGap);
            CorridorRouter.RouteToFloorBelow(trap, trapGap, spineFloor, L2Palette.PlatformFrameY, L2Palette.WallBase);
            var rowGap = new DoorSocket(SocketSide.Bottom, DungeonworldMetrics.RoomShellThick,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            row.Sockets.Add(rowGap);
            CorridorRouter.RouteToFloorBelow(row, rowGap, spineFloor, L2Palette.PlatformFrameY, L2Palette.WallBase);

            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L2Preview] 看样落成 排={plan.CellCount}室+藏物尾段 h={plan.RowHeight}"
                + $" 门={rowReport.DoorsPlaced}成/{rowReport.DoorsFailed}拒"
                + $" 排家具={rowReport.FurniturePlaced}成/{rowReport.FurnitureRejected}拒"
                + $" 机关廊家具={trapTally.Placed}成/{trapTally.Rejected}拒"
                + $" row={row.Bounds} trap={trap.Bounds}");
        }
    }
}
