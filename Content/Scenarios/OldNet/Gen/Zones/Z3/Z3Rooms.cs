using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //Z3 衰减区地表目录（本轮扩容）：焦黑尖塔群 + 坍塌掩体
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
        /// 检疫关卡（点子6）：疯域规则线 FadeLeft 上的失守关卡。双塔门架跨在动线上，
        /// 西面板材完好、东面焦黑剥蚀（材质拼缝讲"从东面被冲垮"），断闸横杆砸在路面。
        /// 固定锚跨带落位：Z2/Z3 双栅格先全查后全标（无回滚机制，顺序即纪律），
        /// 任一侧被占整体东移 24 列重试 3 次。返回关卡东缘列（带界立牌让位用），全败给 -1
        /// </summary>
        internal static int BuildCheckpoint() {
            int[] floorTop = OldNetPlans.FloorTop;
            for (int shift = 0; shift < 4; shift++) {
                int cx = OldNetMetrics.CheckpointCol + shift * 24;
                int baseRow = floorTop[cx];
                //足印：门洞两侧塔+标线+防冲桩，含探照灯头与找平层
                var foot = new Rectangle(cx - 16, baseRow - 24, OldNetMetrics.CheckpointFootW, 28);
                //按带界拆两半，各归各带栅格（Z4 天线桅杆双段预留同款写法）
                int splitX = System.Math.Clamp(OldNetMetrics.FadeLeft, foot.Left, foot.Right);
                var west = new Rectangle(foot.X, foot.Y, splitX - foot.X, foot.Height);
                var east = new Rectangle(splitX, foot.Y, foot.Right - splitX, foot.Height);
                bool westOk = west.Width <= 0 || OldNetPlans.Z2.Grid.CanReserve(west, 0);
                bool eastOk = east.Width <= 0 || OldNetPlans.Z3.Grid.CanReserve(east, 0);
                if (!westOk || !eastOk) {
                    continue;
                }
                if (west.Width > 0) {
                    OldNetPlans.Z2.Grid.MarkUnchecked(west);
                }
                if (east.Width > 0) {
                    OldNetPlans.Z3.Grid.MarkUnchecked(east);
                }
                BuildCheckpointAt(cx, baseRow, floorTop);
                OldNetPlans.ScatterExclusions.Add(foot);
                CWRMod.Instance.Logger.Info($"[OldNet] 检疫关卡@列{cx} 找平行{baseRow}");
                return foot.Right - 1;
            }
            CWRMod.Instance.Logger.Warn("[OldNet] 检疫关卡落位失败（边界带被占）");
            return -1;
        }

        private static void BuildCheckpointAt(int cx, int baseRow, int[] floorTop) {
            int split = OldNetMetrics.FadeLeft;
            //① 全足印地面找平：填谷削峰到 baseRow，回写 FloorTop 维持全流水线口径一致
            //（P70 补墙与 P80 审计均以回写后的地表线为准；削出的露天格不刷墙，与户外口径一致）
            for (int x = cx - 16; x < cx + 18; x++) {
                ushort ground = x < split ? Z2.Z2Style.FloorBrick : Z3Style.FloorBrick;
                if (floorTop[x] > baseRow) {
                    for (int y = baseRow; y < floorTop[x]; y++) {
                        OldNetTileBrush.SetSolid(x, y, ground);
                    }
                }
                else if (floorTop[x] < baseRow) {
                    for (int y = floorTop[x]; y < baseRow; y++) {
                        OldNetTileBrush.ClearCell(x, y);
                    }
                }
                floorTop[x] = baseRow;
            }

            //② 双塔（各 5 宽 18 高）：塔身各自中线拼缝，西半完好镀层、东半焦黑黑曜石
            BuildCheckpointTower(cx - 9, baseRow, Z2.Z2Style.RoomWall, headDir: 1);
            BuildCheckpointTower(cx + 4, baseRow, Z3Style.RoomWall, headDir: -1);

            //③ 顶梁门架连双塔 + 梁下熄灭的扫描头短悬条
            for (int x = cx - 4; x < cx + 4; x++) {
                ushort brick = x < cx ? Z2.Z2Style.RoomBrick : Z3Style.RoomBrick;
                OldNetTileBrush.SetSolid(x, baseRow - 18, brick);
                OldNetTileBrush.SetSolid(x, baseRow - 17, brick);
            }
            for (int x = cx - 3; x < cx + 4; x += 3) {
                OldNetTileBrush.SetSolid(x, baseRow - 16, Z3Style.RoomBrick);
            }

            //④ 断闸横杆：自西塔东壁伸出的斜杆残段，杆尖折落成路面 1 高矮堆
            //（矮堆上方留空，P80 洪泛按四邻可绕，通行性不受影响）
            for (int k = 0; k < 3; k++) {
                OldNetTileBrush.SetSloped(cx - 4 + k, baseRow - 8 + k,
                    Z3Style.RoomBrick, SlopeType.SlopeDownRight);
            }
            OldNetTileBrush.SetSolid(cx, baseRow - 1, Z3Style.RoomBrick);
            OldNetTileBrush.SetSloped(cx + 1, baseRow - 1, Z3Style.RoomBrick, SlopeType.SlopeDownRight);

            //⑤ 检疫标线：门洞两侧地面各 6 格红白警戒纹（锡镀板/黑曜石 1 格交替镶嵌）
            for (int k = 0; k < 6; k++) {
                ushort stripe = k % 2 == 0 ? TileID.TinPlating : TileID.ObsidianBrick;
                OldNetTileBrush.SetSolid(cx - 15 + k, baseRow, stripe);
                OldNetTileBrush.SetSolid(cx + 9 + k, baseRow, stripe);
            }

            //⑥ 防冲桩：东面一排 5 根 2 高斜切桩，第 2/4 根倾倒
            //（确定化半数：全立会丢"防的方向说明失守的方向"叙事，不交给独立掷骰）
            for (int p = 0; p < 5; p++) {
                int px = cx + 9 + p * 2;
                bool fallen = p % 2 == 1;
                if (fallen) {
                    OldNetTileBrush.SetSolid(px, baseRow - 1, Z3Style.RoomBrick);
                    OldNetTileBrush.SetSloped(px + 1, baseRow - 1,
                        Z3Style.RoomBrick, SlopeType.SlopeDownRight);
                }
                else {
                    OldNetTileBrush.SetSolid(px, baseRow - 1, Z3Style.RoomBrick);
                    OldNetTileBrush.SetSloped(px, baseRow - 2, Z3Style.RoomBrick,
                        WorldGen.genRand.NextBool()
                            ? SlopeType.SlopeDownLeft : SlopeType.SlopeDownRight);
                }
            }

            //⑦ 西塔值房凹龛：底部 3 高 3 深，东向开口对门洞
            //02槽位:关卡值房终端（凹龛内地面 (cx-7, baseRow-1)）
            OldNetTileBrush.CarveRect(cx - 7, baseRow - 3, cx - 4, baseRow, Z2.Z2Style.RoomWall);

            //06槽位:过线触发（门洞中心列 cx，站立行 baseRow-1，坐标由 CheckpointCol 反查）

            //⑧ 告示牌挂西塔脚（扫位落在西侧标线带上）
            OldNetZoneCommon.PlaceBoundarySign(cx - 14, OldNetTexts.OldNetSignCheckpoint.Value);
        }

        //关卡塔：5 宽 18 高，塔身开三个 1×2 窗龛（带墙防透天幕），
        //塔顶平台 + 朝门洞悬挑的探照灯头（熄灭不发光，斜切罩收角）
        //04槽位:塔顶哨戒（平台行 baseRow-19，全图最叙事自洽的布防岗位）
        private static void BuildCheckpointTower(int x0, int baseRow, ushort nicheWall, int headDir) {
            for (int x = x0; x < x0 + 5; x++) {
                ushort brick = x < x0 + 2 ? Z2.Z2Style.RoomBrick : Z3Style.RoomBrick;
                for (int y = baseRow - 18; y < baseRow; y++) {
                    OldNetTileBrush.SetSolid(x, y, brick);
                }
            }
            //窗龛
            int wx = x0 + 2;
            foreach (int wy in new[] { baseRow - 15, baseRow - 11, baseRow - 7 }) {
                OldNetTileBrush.ClearCell(wx, wy, nicheWall);
                OldNetTileBrush.ClearCell(wx, wy + 1, nicheWall);
            }
            //塔顶平台
            OldNetTileBrush.PlatformRow(x0, x0 + 5, baseRow - 19, Z3Style.PlatformFrameY);
            //探照灯头：2×2 悬挑向门洞 + 斜切罩；灯头用门洞侧半塔的材质（西塔=焦黑，东塔=完好）
            ushort headBrick = headDir > 0 ? Z3Style.RoomBrick : Z2.Z2Style.RoomBrick;
            int headL = headDir > 0 ? x0 + 4 : x0 - 1;
            OldNetTileBrush.FillRect(headL, baseRow - 21, headL + 2, baseRow - 19, headBrick);
            OldNetTileBrush.SetSloped(headDir > 0 ? headL + 1 : headL, baseRow - 22, headBrick,
                headDir > 0 ? SlopeType.SlopeDownRight : SlopeType.SlopeDownLeft);
        }

        /// <summary>
        /// 坍塌掩体：半埋破壳，屋顶塌开一角、碎块落进室内的小室，
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
