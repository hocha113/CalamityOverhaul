using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //L4免接线看样入口(镜像DungeonworldPreview/L2Preview惯例):任意世界脚下就地盖
    //"阀室+堰闸+半淹管廊+沉没囚室+共享水线+满水settle+双水线痕",
    //不注册GenPass、不影响世界形态;仅单人调试用(联机不发tile同步)。
    //触发TestItem片段见交付报告。两态切换走 PreviewApplyState。
    internal static class L4Preview
    {
        //最近一次看样的假层带+包络,供 PreviewApplyState 限带settle/帧修
        internal static LayerBand LastBand;
        internal static Rectangle LastArea;
        internal static bool HasSample;

        /// <summary>
        /// 在(originX, dryFloor)处铺看样条:dryFloor=玩家脚下干层地板。
        /// 上方阀室/堰闸齐平,下方共享水线挂管廊+沉没囚室,楼梯井回干层。
        /// 占地约[originX-8, originX+150]×[dryFloor-22, dryFloor+46]。
        /// </summary>
        internal static void BuildWaterSample(int originX, int dryFloor, int seed = 4040) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L4Preview] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            var area = new Rectangle(originX - 8, dryFloor - 22, 158, 70);
            LastBand = new LayerBand("L4Preview", area.Top, area.Height, L4Palette.Brick, L4Palette.WallBase);
            LastArea = area;
            L4WaterWorks.Reset();
            HasSample = false;

            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
            }
            var grid = new OccupancyGrid(area);
            //迷你干走廊(净高4)+占用,玩家出生即站在阀室门外
            TileBrush.CarveRect(area.Left + 2, dryFloor - 4, area.Right - 2, dryFloor, L4Palette.WallBase);
            grid.MarkUnchecked(new Rectangle(area.Left, dryFloor - 5, area.Width, 8));

            int waterline = dryFloor + 16;
            int galFloor = waterline + 4;
            int sunkenFloor = waterline + 10;

            Point valveSize = L4Rooms.ValveRoomInteriorSize(rand);
            RoomNode valve = RoomPlacer.TryPlace(grid, rand, area.Left + 6,
                area.Left + 6 + valveSize.X + 8, dryFloor, valveSize, valveSize);
            if (valve == null) {
                CWRMod.Instance.Logger.Error("[L4Preview] 阀室落位失败");
                return;
            }
            L4Rooms.Tally valveTally = L4Rooms.BuildValveRoom(valve, rand);

            Point gateSize = L4Rooms.GateCorridorInteriorSize(rand);
            RoomNode gate = RoomPlacer.TryPlace(grid, rand, valve.Bounds.Right + 4,
                valve.Bounds.Right + 4 + gateSize.X + 8, dryFloor, gateSize, gateSize);
            if (gate == null) {
                CWRMod.Instance.Logger.Error("[L4Preview] 堰闸走廊落位失败");
                return;
            }
            L4Rooms.Tally gateTally = L4Rooms.BuildGateCorridor(gate, rand);

            var linkTally = new L4Rooms.Tally();
            L4Rooms.LinkDryRooms(valve, gate, dryFloor, L4Palette.WallBase, ref linkTally);

            Point galSize = L4Rooms.GalleryInteriorSize(rand);
            galSize.X = System.Math.Min(galSize.X, 28);
            //管廊放在阀室右侧下方,避免楼梯井柱与管廊左壳撞列
            int galMin = valve.Bounds.Right + 2;
            RoomNode gallery = RoomPlacer.TryPlace(grid, rand, galMin,
                galMin + galSize.X + 8, galFloor, galSize, galSize);
            if (gallery == null) {
                CWRMod.Instance.Logger.Error("[L4Preview] 管廊落位失败");
                return;
            }
            L4Rooms.Tally galTally = L4Rooms.BuildGallery(gallery, waterline, rand, drained: false, sunkenChest: true);

            Point cellSize = L4Rooms.SunkenCellInteriorSize(rand);
            cellSize.X = System.Math.Min(cellSize.X, 30);
            RoomNode cells = RoomPlacer.TryPlace(grid, rand, gallery.Bounds.Right + 4,
                gallery.Bounds.Right + 4 + cellSize.X + 8, sunkenFloor, cellSize, cellSize);
            if (cells == null) {
                CWRMod.Instance.Logger.Error("[L4Preview] 沉没囚室落位失败(看样区宽度不足?)");
                return;
            }
            L4Rooms.Tally cellTally = L4Rooms.BuildSunkenCells(cells, waterline, rand);
            L4Rooms.CarveWetPort(gallery.InteriorRight, cells.InteriorLeft, waterline, L4Palette.WallSlab);
            //注水坑放在湿房X范围之外,避免7深坑体凿穿下方管廊顶
            int pitX = System.Math.Max(gate.Bounds.Right + 4, cells.Bounds.Right + 3);
            if (pitX + 6 < area.Right) {
                L4Rooms.CarveWaterPit(pitX, dryFloor, L4Palette.WallSlab);
            }

            //阀室地板下探到管廊走道:爬升=galFloor-dryFloor=20>坡道上限,楼梯井
            var drop = new DoorSocket(SocketSide.Bottom, DungeonworldMetrics.RoomShellThick,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            valve.Sockets.Add(drop);
            CorridorRouter.RouteToFloorBelow(valve, drop, galFloor,
                L4Palette.PlatformFrameY, L4Palette.WallBase);
            //井底接到管廊内膛(湿port不穿井柱)
            int wellX = valve.Bounds.Left + DungeonworldMetrics.RoomShellThick;
            L4Rooms.CarveWetPort(wellX + DungeonworldMetrics.StairWellWidth, gallery.InteriorLeft,
                waterline, L4Palette.WallSlab);

            int wet = L4WaterWorks.FillState(high: true);
            L4WaterWorks.SettleBand(LastBand);
            L4WaterWorks.PaintAging();
            HasSample = true;

            WorldGen.RangeFrame(area.Left - 1, area.Top - 1, area.Right + 1, area.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L4Preview] 看样落成 水线={waterline} 舱段={L4WaterWorks.Compartments.Count} 水格={wet}"
                + $" 阀家具={valveTally.Placed}成/{valveTally.Rejected}拒"
                + $" 闸={gateTally.Placed}/{gateTally.Rejected}"
                + $" 廊={galTally.Placed}/{galTally.Rejected}"
                + $" 囚={cellTally.Placed}/{cellTally.Rejected}"
                + $" valve={valve.Bounds} gallery={gallery.Bounds} cells={cells.Bounds}"
                + " 两态切换:L4Preview.PreviewApplyState(false/true)");
        }

        /// <summary>
        /// 看样条两态切换(R1雏形的可玩入口)。须先 BuildWaterSample。
        /// high=true满水,false排空;一次性重写+限带settle,不物理模拟排水。
        /// </summary>
        internal static void PreviewApplyState(bool high) {
            if (!HasSample || L4WaterWorks.Compartments.Count == 0) {
                CWRMod.Instance.Logger.Warn("[L4Preview] 无看样舱段,先调用 BuildWaterSample");
                return;
            }
            L4WaterWorks.ApplyState(high, LastBand);
            WorldGen.RangeFrame(LastArea.Left - 1, LastArea.Top - 1, LastArea.Right + 1, LastArea.Bottom + 1);
        }
    }
}
