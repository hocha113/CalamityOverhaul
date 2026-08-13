using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //L6免接线看样入口(镜像DungeonworldPreview/L2Preview惯例):任意世界脚下就地盖
    //"A型机关廊(单镖+落石+活塞留位)+B型裂砖刺坑廊+工匠墓志车间",不注册GenPass、
    //不影响世界形态;仅单人调试用(联机不发tile同步);触发TestItem片段见交付报告
    internal static class L6Preview
    {
        /// <summary>
        /// 在(originX, spineFloor)处铺看样条:spineFloor=迷你脊地板行(玩家脚下),
        /// 上方悬挂三段房,楼梯井下探回脊。强制母题计划,把可读预告一次晒齐。
        /// </summary>
        internal static void BuildFoundrySample(int originX, int spineFloor, int seed = 1919) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L6Preview] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            L6MachineSlots.Reset();
            var area = new Rectangle(originX - 8, spineFloor - 44, 150, 52);

            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L6Palette.Brick);
                }
            }
            var grid = new OccupancyGrid(area);
            TileBrush.CarveRect(area.Left + 2, spineFloor - 6, area.Right - 2, spineFloor, L6Palette.WallTiled);
            grid.MarkUnchecked(new Rectangle(area.Left, spineFloor - 7, area.Width, area.Bottom - (spineFloor - 7)));

            int floorA = spineFloor - 11;

            //A型:单镖+落石+活塞留位(威胁由浅入深,预告件一次到齐)
            var planA = new L6Rooms.CorridorPlanA {
                Motifs = [L6Traps.Motif.Dart, L6Traps.Motif.Boulder, L6Traps.Motif.PistonSlot],
                Lens = [15, 14, 11],
                Tier = 2,
            };
            Point sizeA = L6Rooms.CorridorAInteriorSize(planA);
            RoomNode corrA = RoomPlacer.TryPlace(grid, rand, area.Left + 4,
                area.Left + 4 + sizeA.X + 6, floorA, sizeA, sizeA);
            if (corrA == null) {
                CWRMod.Instance.Logger.Error("[L6Preview] A型机关廊落位失败,区域被占用?");
                return;
            }
            L6Rooms.Tally tallyA = L6Rooms.BuildCorridorA(corrA, planA, rand);

            //B型:单跨裂砖+刺坑+追身镖(下厅接驳,镜像L2教学廊升级形态)
            var planB = new L6Rooms.CorridorPlanB {
                SpanWidths = [8],
                Spikes = true,
                DartOver = true,
            };
            Point sizeB = L6Rooms.CorridorBInteriorSize(planB);
            RoomNode corrB = RoomPlacer.TryPlace(grid, rand, corrA.Bounds.Right + 4,
                corrA.Bounds.Right + 4 + sizeB.X + 6, floorA, sizeB, sizeB);
            if (corrB == null) {
                CWRMod.Instance.Logger.Error("[L6Preview] B型裂砖廊落位失败(看样区宽度不足?)");
                return;
            }
            L6Rooms.Tally tallyB = L6Rooms.BuildCorridorB(corrB, planB, rand);

            Point sizeW = L6Rooms.WorkshopInteriorSize(rand);
            RoomNode shop = RoomPlacer.TryPlace(grid, rand, corrB.Bounds.Right + 4,
                corrB.Bounds.Right + 4 + sizeW.X + 6, floorA, sizeW, sizeW);
            if (shop == null) {
                CWRMod.Instance.Logger.Error("[L6Preview] 车间落位失败");
                return;
            }
            L6Rooms.Tally tallyW = L6Rooms.BuildWorkshop(shop, rand, L6Rooms.SignEpitaph);

            //链边:A右门↔B左门(B接下厅),B右门↔车间左门;门板控节奏
            Link(corrA, corrB, arch: false);
            Link(corrB, shop, arch: false);

            //脊接驳:A下厅右端+车间各一口楼梯井
            Drop(corrA, corrA.Bounds.Width - 5, spineFloor);
            Drop(shop, DungeonworldMetrics.RoomShellThick, spineFloor);

            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            L6MachineSlots.LogAll();
            CWRMod.Instance.Logger.Info(
                $"[L6Preview] 看样落成 A家具={tallyA.Placed}成/{tallyA.Rejected}拒"
                + $" B家具={tallyB.Placed}成/{tallyB.Rejected}拒"
                + $" 车间={tallyW.Placed}成/{tallyW.Rejected}拒"
                + $" 留位={L6MachineSlots.Slots.Count}"
                + $" A={corrA.Bounds} B={corrB.Bounds} 车间={shop.Bounds}");
        }

        private static void Link(RoomNode a, RoomNode b, bool arch) {
            DoorSocket sa = arch ? L6Rooms.FloorArch(a, SocketSide.Right)
                : L6Rooms.FloorDoor(a, SocketSide.Right);
            DoorSocket sb = arch ? L6Rooms.FloorArch(b, SocketSide.Left)
                : L6Rooms.FloorDoor(b, SocketSide.Left);
            a.Sockets.Add(sa);
            b.Sockets.Add(sb);
            CorridorRouter.RouteDoorToDoor(a, sa, b, sb, L6Palette.WallTiled);
            L6Palette.PlaceDoorPlate(a.Bounds.Right - 2, a.FloorTop - 1);
            L6Palette.PlaceDoorPlate(b.Bounds.Left + 1, b.FloorTop - 1);
        }

        private static void Drop(RoomNode room, int offset, int spineFloor) {
            var gap = new DoorSocket(SocketSide.Bottom, offset,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            room.Sockets.Add(gap);
            CorridorRouter.RouteToFloorBelow(room, gap, spineFloor,
                L6Palette.PlatformFrameY, L6Palette.WallTiled);
        }
    }
}
