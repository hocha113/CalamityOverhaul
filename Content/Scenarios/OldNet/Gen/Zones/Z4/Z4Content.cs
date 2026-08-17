using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z4
{
    //Z4 高空带（M3）：剪影巨构——天线桅杆（地表通高空的唯一爬升动线）+
    //浮空残骸方舱（远景敬畏，翼装/绳索可达）
    internal static class Z4Content
    {
        internal static void PlanAndBuild(OldNetBuildContext ctx) {
            int masts = BuildAntennaMasts();
            int hulks = BuildFloatingHulks(ctx);
            CWRMod.Instance.Logger.Info($"[OldNet] Z4 masts={masts} hulks={hulks}");
        }

        //──── 天线桅杆：自地板拔起穿入高空带，横臂即爬梯 ────

        private static int BuildAntennaMasts() {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;
            for (int m = 0; m < OldNetMetrics.AntennaCount; m++) {
                //均匀分段：废墟带与衰减区各摊
                int segW = (OldNetMetrics.PlayRight - 60 - ruinLeft) / OldNetMetrics.AntennaCount;
                bool placed = false;
                for (int attempt = 0; attempt < 16 && !placed; attempt++) {
                    int x = ruinLeft + m * segW + WorldGen.genRand.Next(20, segW - 20);
                    int topRow = WorldGen.genRand.Next(50, 96);
                    int baseRow = floorTop[x];
                    //双段预留：高空段走 Z4 栅格；近地段还要过所在带的栅格
                    //（塔基不许打穿封锁区/中继/竖井的既有足印）
                    var skyprint = new Rectangle(x - 5, OldNetMetrics.BorderThick, 12,
                        OldNetMetrics.SkyBandBottom - OldNetMetrics.BorderThick);
                    OldNetBuildContext ground = OldNetMetrics.BandIndexForColumn(x) switch {
                        1 => OldNetPlans.Z1,
                        3 => OldNetPlans.Z3,
                        _ => OldNetPlans.Z2,
                    };
                    var groundprint = new Rectangle(x - 5, baseRow - 44, 12, 46);
                    if (ground == null || !ground.Grid.CanReserve(groundprint, 0)) {
                        continue;
                    }
                    if (!OldNetPlans.Z4.Grid.TryReserve(skyprint, 2)) {
                        continue;
                    }
                    ground.Grid.MarkUnchecked(groundprint);
                    BuildMast(x, baseRow, topRow);
                    placed = true;
                    built++;
                }
            }
            return built;
        }

        private static void BuildMast(int x, int baseRow, int topRow) {
            //双柱身：一根到顶
            OldNetTileBrush.FillRect(x, topRow, x + 2, baseRow, Z4Style.FrameBrick);
            //横臂即爬梯：每 7 行一道，左右交替出挑（借平台跳跃可一路到顶）
            bool side = false;
            for (int y = baseRow - 7; y > topRow + 4; y -= 7) {
                int left = side ? x - 4 : x;
                OldNetTileBrush.PlatformRow(left, left + 6, y, Z4Style.PlatformFrameY);
                side = !side;
            }
            //顶冠：三层收窄 + 顶端信标节点（给爬到顶的人一枚糖）
            OldNetTileBrush.PlatformRow(x - 3, x + 5, topRow + 2, Z4Style.PlatformFrameY);
            OldNetTileBrush.FillRect(x - 1, topRow - 1, x + 3, topRow, Z4Style.FrameBrick);
            OldNetPlans.Budget.TryPlaceUnderPlain(x + 1, topRow - 2);
        }

        //──── 浮空残骸方舱：高空带的中空巨构，剪影敬畏 + 高值内舱 ────

        private static int BuildFloatingHulks(OldNetBuildContext ctx) {
            int built = 0;
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;
            for (int h = 0; h < OldNetMetrics.HulkCount; h++) {
                for (int attempt = 0; attempt < 20; attempt++) {
                    int left = WorldGen.genRand.Next(ruinLeft, OldNetMetrics.PlayRight - 60);
                    int top = WorldGen.genRand.Next(OldNetMetrics.HulkRowMin, OldNetMetrics.HulkRowMax);
                    var bounds = new Rectangle(left, top, 30, 14);
                    if (!ctx.Grid.TryReserve(bounds, 6)) {
                        continue;
                    }
                    BuildHulk(bounds);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildHulk(Rectangle b) {
            //中空壳体（2 厚）+ 内墙
            OldNetTileBrush.FillRect(b.Left, b.Top, b.Right, b.Bottom, Z4Style.FrameBrick);
            OldNetTileBrush.CarveRect(b.Left + 2, b.Top + 2, b.Right - 2, b.Bottom - 2,
                Terraria.ID.WallID.MartianConduit);
            //两侧舷窗开口（4 高）：翼装进入的口
            OldNetTileBrush.CarveRect(b.Left, b.Bottom - 6, b.Left + 2, b.Bottom - 2,
                Terraria.ID.WallID.MartianConduit);
            OldNetTileBrush.CarveRect(b.Right - 2, b.Bottom - 6, b.Right, b.Bottom - 2,
                Terraria.ID.WallID.MartianConduit);
            //内部层架
            OldNetTileBrush.PlatformRow(b.Left + 4, b.Right - 4, b.Top + 6, Z4Style.PlatformFrameY);
            //内舱高值：普通节点 1-2 枚
            OldNetPlans.Budget.TryPlaceUnderPlain(b.Left + 6, b.Bottom - 3);
            if (WorldGen.genRand.NextBool()) {
                OldNetPlans.Budget.TryPlaceUnderPlain(b.Right - 7, b.Top + 5);
            }
            //底部垂绳：可达性的无言提示（SHPCCradle 同语汇）
            foreach (int rx in new[] { b.Left + 8, b.Right - 9 }) {
                for (int step = 0; step < 18; step++) {
                    int y = b.Bottom + step;
                    Tile tile = Main.tile[rx, y];
                    if (tile.HasTile) {
                        break;
                    }
                    tile.HasTile = true;
                    tile.TileType = Terraria.ID.TileID.Rope;
                }
            }
        }
    }
}
