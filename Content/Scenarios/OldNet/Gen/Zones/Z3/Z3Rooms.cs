using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //Z3 衰减区地表目录（本轮扩容）：焦黑尖塔群 + 坍塌掩体——
    //信号尽头终于有了"东西"，但都是烧毁的
    internal static class Z3Rooms
    {
        /// <summary>
        /// 焦黑尖塔群：3~5 根烧毁塔骨高低错落，塔间倒伏残梁；
        /// 最高一根顶上给普通节点（够到它要爬要跳）
        /// </summary>
        internal static int BuildScorchedSpireGroups(OldNetBuildContext ctx, int groups) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int g = 0; g < groups; g++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 30, ctx.Area.Right - 70);
                    int surface = floorTop[left + 15];
                    var footprint = new Rectangle(left, surface - 26, 30, 26);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildSpires(left, floorTop);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildSpires(int left, int[] floorTop) {
            int count = WorldGen.genRand.Next(3, 6);
            int tallestX = -1;
            int tallestH = 0;
            int x = left + 2;
            for (int s = 0; s < count && x < left + 27; s++) {
                int h = WorldGen.genRand.Next(6, 23);
                int w = WorldGen.genRand.NextBool(3) ? 1 : 2;
                int baseRow = floorTop[x];
                OldNetTileBrush.FillRect(x, baseRow - h, x + w, baseRow, Z3Style.RoomBrick);
                //焦断顶：斜切收角
                OldNetTileBrush.SetSloped(x + WorldGen.genRand.Next(w), baseRow - h - 1,
                    Z3Style.RoomBrick, WorldGen.genRand.NextBool()
                        ? SlopeType.SlopeDownLeft : SlopeType.SlopeDownRight);
                if (h > tallestH) {
                    tallestH = h;
                    tallestX = x;
                }
                //塔间倒伏残梁：烧塌的横杆躺在地上
                int gap = WorldGen.genRand.Next(3, 7);
                if (WorldGen.genRand.NextBool(3) && gap >= 4) {
                    int beamX = x + w + 1;
                    OldNetTileBrush.FillRect(beamX, floorTop[beamX] - 1,
                        beamX + gap - 2, floorTop[beamX], Z3Style.RoomBrick);
                }
                x += w + gap;
            }
            if (tallestX >= 0) {
                OldNetPlans.Budget.TryPlaceUnderPlain(tallestX, floorTop[tallestX] - tallestH - 2);
            }
        }

        /// <summary>
        /// 坍塌掩体：半埋破壳——屋顶塌开一角、碎块落进室内的小室，
        /// 五成藏加密节点（衰减区高险高值），五成普通节点
        /// </summary>
        internal static int BuildCollapsedBunkers(OldNetBuildContext ctx, int count) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int b = 0; b < count; b++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 30, ctx.Area.Right - 50);
                    int surface = floorTop[left + 6];
                    var footprint = new Rectangle(left, surface - 6, 13, 9);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildBunker(left, surface);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildBunker(int left, int surface) {
            int top = surface - 5;
            //半埋：壳体下沉 2 行进地里
            int bottom = surface + 2;
            OldNetTileBrush.FillRect(left, top, left + 13, bottom, Z3Style.RoomBrick);
            OldNetTileBrush.CarveRect(left + 2, top + 2, left + 11, bottom - 2, Z3Style.RoomWall);
            //塌角：屋顶右侧撕开 4 宽 + 两缘斜切 + 落进室内的碎堆
            int gapL = left + 7;
            OldNetTileBrush.CarveRect(gapL, top, gapL + 4, top + 2, Z3Style.RoomWall);
            OldNetTileBrush.SetSloped(gapL - 1, top, Z3Style.RoomBrick, SlopeType.SlopeDownLeft);
            OldNetTileBrush.SetSloped(gapL + 4, top, Z3Style.RoomBrick, SlopeType.SlopeDownRight);
            OldNetTileBrush.FillRect(gapL, bottom - 3, gapL + 2, bottom - 2, Z3Style.RoomBrick);
            //西侧门洞（3 高，贴外侧地面）
            OldNetTileBrush.CarveRect(left, bottom - 5, left + 2, bottom - 2, Z3Style.RoomWall);
            //节点：五成加密五成普通，落在室内地板
            int nx = left + 4;
            int ny = bottom - 3;
            if (WorldGen.genRand.NextBool()) {
                OldNetNodeBudget.WriteNodeTile(nx, ny,
                    ModContent.TileType<OldNetEncryptedNodeTile>());
            }
            else {
                OldNetPlans.Budget.TryPlaceUnderPlain(nx, ny);
            }
        }
    }
}
