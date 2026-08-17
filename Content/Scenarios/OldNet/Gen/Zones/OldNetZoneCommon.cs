using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones
{
    //带内容共用建造块：浮空平台/挂房链。随机全走WorldGen.genRand
    internal static class OldNetZoneCommon
    {
        /// <summary>浮空几何平台：双层厚度读作悬浮板（赛博空间理应是直角的）</summary>
        internal static void PlaceFloatingSlabs(int xFrom, int xTo, int gapMin, int gapMax, ushort brick) {
            int[] floorTop = OldNetPlans.FloorTop;
            int x = xFrom;
            while (x < xTo) {
                int slabWidth = WorldGen.genRand.Next(4, 10);
                int lift = WorldGen.genRand.Next(12, 43);
                int y = floorTop[x] - lift;
                for (int i = 0; i < slabWidth && x + i < xTo; i++) {
                    OldNetTileBrush.SetSolid(x + i, y, brick);
                    OldNetTileBrush.SetSolid(x + i, y + 1, brick);
                }
                x += slabWidth + WorldGen.genRand.Next(gapMin, gapMax + 1);
            }
        }

        /// <summary>
        /// 把带内浅层平台厅逐个挂房；nodeChance=每房机会性放地下普通节点的概率。
        /// 返回建成房数
        /// </summary>
        internal static int HangRoomsForBand(OldNetBuildContext ctx, int bandIndex,
            ushort brick, ushort wall, int roomsPerLanding, float nodeChance) {
            int built = 0;
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                if (shaft.Deep || OldNetMetrics.BandIndexForColumn(shaft.Col) != bandIndex) {
                    continue;
                }
                built += HangRoomsAt(ctx, shaft.Landing, roomsPerLanding, brick, wall,
                    new Point(8, 5), new Point(16, 7), nodeChance);
            }
            return built;
        }

        /// <summary>
        /// 带界立牌：告示牌 + 文本（PlaceSign 自带锚定校验，拒绝即跳过记日志）。
        /// 站位行取地板上一格，附近轻微扫位提高成功率
        /// </summary>
        internal static void PlaceBoundarySign(int x, string text) {
            int[] floorTop = OldNetPlans.FloorTop;
            for (int dx = 0; dx < 8; dx++) {
                int px = x + dx;
                int standRow = floorTop[px] - 1;
                if (!WorldGen.PlaceSign(px, standRow, Terraria.ID.TileID.Signs)) {
                    continue;
                }
                int sign = Sign.ReadSign(px, standRow);
                if (sign >= 0) {
                    Sign.TextSign(sign, text);
                }
                return;
            }
            CWRMod.Instance.Logger.Warn($"[OldNet] 带界立牌落位失败@x≈{x}");
        }

        /// <summary>指定平台厅挂房 + 机会性节点，深层房间入口共用</summary>
        internal static int HangRoomsAt(OldNetBuildContext ctx, Rectangle landing, int count,
            ushort brick, ushort wall, Point interiorMin, Point interiorMax, float nodeChance) {
            int built = 0;
            foreach (OldNetRoomNode room in OldNetRoomBuilder.HangRoomsOffLanding(
                ctx, landing, count, brick, wall, interiorMin, interiorMax)) {
                built++;
                if (WorldGen.genRand.NextFloat() < nodeChance
                    && room.InteriorRight - room.InteriorLeft > 4) {
                    int nx = WorldGen.genRand.Next(room.InteriorLeft + 1, room.InteriorRight - 1);
                    OldNetPlans.Budget.TryPlaceUnderPlain(nx, room.FloorTop - 1);
                }
            }
            return built;
        }
    }
}
