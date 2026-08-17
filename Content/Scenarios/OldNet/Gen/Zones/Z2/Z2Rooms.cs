using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2
{
    //Z2 废墟带地表目录（M3）：服务器墓地 + 断裂数据桥——主产区的信服力结构
    internal static class Z2Rooms
    {
        /// <summary>
        /// 服务器墓地：方碑场——竖立/倾颓混排的导管镀层碑。
        /// "死机的机柜排成墓园"是废墟带的第一母题
        /// </summary>
        internal static int BuildServerGraveyards(OldNetBuildContext ctx, int fields) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int f = 0; f < fields; f++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 30, ctx.Area.Right - 80);
                    int surface = floorTop[left + 20];
                    var footprint = new Rectangle(left, surface - 10, 40, 10);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildGraveField(left, floorTop);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildGraveField(int left, int[] floorTop) {
            int count = WorldGen.genRand.Next(6, 10);
            int x = left + 2;
            for (int m = 0; m < count && x < left + 38; m++) {
                int baseRow = floorTop[x];
                int roll = WorldGen.genRand.Next(10);
                if (roll < 2) {
                    //倾颓碑：横倒的 4x2 板
                    OldNetTileBrush.FillRect(x, baseRow - 2, x + 4, baseRow, Z2Style.RoomBrick);
                    x += 6;
                }
                else {
                    //立碑：2 宽 3-6 高，30% 顶部斜切（被削的头）
                    int h = WorldGen.genRand.Next(3, 7);
                    OldNetTileBrush.FillRect(x, baseRow - h, x + 2, baseRow, Z2Style.RoomBrick);
                    if (roll < 5) {
                        OldNetTileBrush.SetSloped(x, baseRow - h - 1, Z2Style.RoomBrick,
                            WorldGen.genRand.NextBool()
                                ? SlopeType.SlopeDownRight : SlopeType.SlopeDownLeft);
                    }
                    x += WorldGen.genRand.Next(4, 7);
                }
            }
        }

        /// <summary>
        /// 断裂数据桥：中空双墩桥——两端桥面自浮空断开，中央缺口读作"链路断了"。
        /// 桥面可走（悬空双层板 + 缺口），是废墟带的立体动线
        /// </summary>
        internal static int BuildBrokenBridges(OldNetBuildContext ctx, int count) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int b = 0; b < count; b++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 40, ctx.Area.Right - 100);
                    int lift = WorldGen.genRand.Next(18, 31);
                    int span = WorldGen.genRand.Next(30, 43);
                    int deckRow = floorTop[left + span / 2] - lift;
                    var footprint = new Rectangle(left, deckRow - 4, span, 8);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildBridge(left, deckRow, span);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildBridge(int left, int deckRow, int span) {
            //中央缺口：桥断在中间，两端桥面残存
            int gap = WorldGen.genRand.Next(6, 11);
            int gapStart = left + (span - gap) / 2;
            for (int x = left; x < left + span; x++) {
                if (x >= gapStart && x < gapStart + gap) {
                    continue;
                }
                OldNetTileBrush.SetSolid(x, deckRow, Z2Style.RoomBrick);
                OldNetTileBrush.SetSolid(x, deckRow + 1, Z2Style.RoomBrick);
            }
            //断口毛边：缺口两缘斜切 + 悬垂残杆
            OldNetTileBrush.SetSloped(gapStart - 1, deckRow - 1, Z2Style.RoomBrick, SlopeType.SlopeDownLeft);
            OldNetTileBrush.SetSloped(gapStart + gap, deckRow - 1, Z2Style.RoomBrick, SlopeType.SlopeDownRight);
            OldNetTileBrush.FillRect(gapStart - 1, deckRow + 2, gapStart, deckRow + 4, Z2Style.RoomBrick);
            OldNetTileBrush.FillRect(gapStart + gap, deckRow + 2, gapStart + gap + 1, deckRow + 4, Z2Style.RoomBrick);
            //两端栏杆桩
            OldNetTileBrush.FillRect(left, deckRow - 2, left + 1, deckRow, Z2Style.RoomBrick);
            OldNetTileBrush.FillRect(left + span - 1, deckRow - 2, left + span, deckRow, Z2Style.RoomBrick);
        }
    }
}
