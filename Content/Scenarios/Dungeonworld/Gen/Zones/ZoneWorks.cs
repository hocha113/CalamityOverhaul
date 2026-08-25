using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones
{
    //三个地带模块共用的小件:宿主过滤/落口规划与开凿/等价材质交换。
    //只服务 Gen\Zones,不是新的公共基建,别的体系不许来挂。
    internal static class ZoneWorks
    {
        //落口列避让序:原位优先,左右各挪最多3列
        private static readonly int[] GapDodges = [0, -1, 1, -2, 2, -3, 3];

        /// <summary>
        /// 表面材质等价交换:只改 TileType,保留 slope/半砖/液体/漆
        /// (原版尖刺 pass 同款语法 F31;TileBrush.SetSolid 会重置 slope 与液体,
        /// 对既有区地表做交换必须走本函数,直写先例=L4Palette.LaySunkenChain/LayerTint)。
        /// </summary>
        internal static void SwapSolidType(int x, int y, ushort type) {
            if (WorldGen.InWorld(x, y, 5)) {
                Main.tile[x, y].TileType = type;
            }
        }

        /// <summary>房内是否存了液体:贴地板行采样(镜像 IntersticePlanner.HoldsLiquid)</summary>
        internal static bool HoldsLiquid(RoomNode room) {
            int y = room.FloorTop - 1;
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                if (WorldGen.InWorld(x, y, 5) && Main.tile[x, y].LiquidAmount > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 规划宿主地板 3 宽落口列(零写入):落口列上站着家具则左右挪列避让
        /// (免得凿掉家具脚下地板,FurnitureAudit 会响),挪不开返回 -1 交调用方换宿主。
        /// </summary>
        internal static int PlanHostFloorGap(RoomNode host, int wantShaftX) {
            int lo = host.InteriorLeft;
            int hi = host.InteriorRight - 3;
            if (hi < lo) {
                return -1;
            }
            int baseX = System.Math.Clamp(wantShaftX, lo, hi);
            foreach (int dx in GapDodges) {
                int x = baseX + dx;
                if (x >= lo && x <= hi && !GapBlocked(host, x)) {
                    return x;
                }
            }
            return -1;
        }

        /// <summary>开凿已规划的落口:登记 socket+井口盖平台(井体由调用方接着刻)</summary>
        internal static void OpenHostFloorGap(RoomNode host, int shaftX,
            short platformFrameY, ushort wall) {
            var gap = new DoorSocket(SocketSide.Bottom, shaftX - host.Bounds.Left,
                SocketKind.PlatformGap, 3);
            host.Sockets.Add(gap);
            CorridorRouter.OpenPlatformGap(host, gap, platformFrameY, wall);
        }

        //落口 3 列的站立行上有家具(HasTile)即视为被占
        private static bool GapBlocked(RoomNode host, int x) {
            int standRow = host.FloorTop - 1;
            for (int i = 0; i < 3; i++) {
                if (!WorldGen.InWorld(x + i, standRow, 5) || Main.tile[x + i, standRow].HasTile) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>图内节点索引(镜像 IntersticePlanner.IndexOf:引用相等倒序扫)</summary>
        internal static int IndexOf(RoomGraph graph, RoomNode node) {
            for (int i = graph.Rooms.Count - 1; i >= 0; i--) {
                if (ReferenceEquals(graph.Rooms[i], node)) {
                    return i;
                }
            }
            return 0;
        }
    }
}
