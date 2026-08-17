using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z1
{
    //Z1 墙脚带地表目录（M3）：废弃中继阵列 + 接入亭——教学密度的低威胁地标
    internal static class Z1Rooms
    {
        /// <summary>
        /// 废弃中继阵列：一排断头死塔（锡镀立柱+横梁+顶端断口），
        /// 纯剪影装饰——"这里曾经有人维护"的第一眼证据
        /// </summary>
        internal static int BuildDeadPylonArrays(OldNetBuildContext ctx, int groups) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int g = 0; g < groups; g++) {
                //组足印：26 宽 × 塔高带，栅格预留避锚位/竖井
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 60, ctx.Area.Right - 90);
                    int surface = floorTop[left + 13];
                    var footprint = new Rectangle(left, surface - 34, 26, 34);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildPylonGroup(left, floorTop);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }

        //一组三根塔：等距立柱，高度渐变，顶端一侧斜切读作断口
        private static void BuildPylonGroup(int left, int[] floorTop) {
            for (int p = 0; p < 3; p++) {
                int x = left + 3 + p * 9;
                int h = 16 + WorldGen.genRand.Next(10);
                int baseRow = floorTop[x];
                //双柱身
                OldNetTileBrush.FillRect(x, baseRow - h, x + 2, baseRow, Z1Style.RoomBrick);
                //横梁：每 5 行一道 4 宽平台
                for (int y = baseRow - 5; y > baseRow - h + 2; y -= 5) {
                    OldNetTileBrush.PlatformRow(x - 1, x + 3, y, Z1Style.PlatformFrameY);
                }
                //顶端断口：一侧高一格 + 斜切收角
                OldNetTileBrush.SetSolid(x, baseRow - h - 1, Z1Style.RoomBrick);
                OldNetTileBrush.SetSloped(x + 1, baseRow - h - 1, Z1Style.RoomBrick, SlopeType.SlopeDownLeft);
            }
        }

        /// <summary>接入亭：出生带的小掩体（壳+门洞+内墙），教学动线上的第一个"屋"</summary>
        internal static int BuildShelterPods(OldNetBuildContext ctx, int count) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int i = 0; i < count; i++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(
                        OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols + 20,
                        ctx.Area.Left + 400);
                    int surface = floorTop[left + 5];
                    var footprint = new Rectangle(left, surface - 7, 11, 7);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    //壳体 + 内膛 + 双侧门洞（3 高）
                    OldNetTileBrush.FillRect(left, surface - 7, left + 11, surface, Z1Style.RoomBrick);
                    OldNetTileBrush.CarveRect(left + 1, surface - 6, left + 10, surface - 1, Z1Style.RoomWall);
                    OldNetTileBrush.CarveRect(left, surface - 4, left + 1, surface - 1, Z1Style.RoomWall);
                    OldNetTileBrush.CarveRect(left + 10, surface - 4, left + 11, surface - 1, Z1Style.RoomWall);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }
    }
}
