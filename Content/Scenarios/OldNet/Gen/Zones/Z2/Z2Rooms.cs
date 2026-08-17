using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2
{
    //Z2 废墟带地表目录（M3 + 本轮扩容）：服务器墓地 + 断裂数据桥 +
    //坠毁数据方舟 + 冷却塔——主产区的信服力结构
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

        /// <summary>
        /// 坠毁数据方舟：断成两截的运载舰残骸——两段中空舱体隔着撕裂口对望，
        /// 舱内普通节点，够大的主舱五成藏加密节点。"从天上掉下来的东西"
        /// </summary>
        internal static int BuildDataArks(OldNetBuildContext ctx, int count) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int a = 0; a < count; a++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int span = WorldGen.genRand.Next(28, 37);
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 40, ctx.Area.Right - span - 40);
                    int surface = floorTop[left + span / 2];
                    int hullH = WorldGen.genRand.Next(7, 10);
                    var footprint = new Rectangle(left, surface - hullH - 1, span, hullH + 3);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildArk(left, surface, span, hullH);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildArk(int left, int surface, int span, int hullH) {
            //撕裂口：偏离中点的断口把船撕成艏艉两段
            int tear = WorldGen.genRand.Next(5, 8);
            int tearStart = left + span * WorldGen.genRand.Next(40, 61) / 100 - tear / 2;
            int top = surface - hullH;
            //地基找平：坠落冲击把地面压实
            OldNetTileBrush.FillRect(left, surface, left + span, surface + 2, Z2Style.RoomBrick);
            BuildArkSegment(left, tearStart, top, surface, doorOnRight: true);
            BuildArkSegment(tearStart + tear, left + span, top, surface, doorOnRight: false);
            //撕裂口毛边：两缘斜切 + 口中崩落碎块
            OldNetTileBrush.SetSloped(tearStart - 1, top - 1, Z2Style.RoomBrick, SlopeType.SlopeDownLeft);
            OldNetTileBrush.SetSloped(tearStart + tear, top - 1, Z2Style.RoomBrick, SlopeType.SlopeDownRight);
            OldNetTileBrush.FillRect(tearStart + tear / 2, surface - 1,
                tearStart + tear / 2 + 2, surface, Z2Style.RoomBrick);
        }

        //单段舱体：壳+内膛+朝撕裂口的门洞+外角斜切；节点在舱内
        private static void BuildArkSegment(int segLeft, int segRight, int top, int surface, bool doorOnRight) {
            if (segRight - segLeft < 8) {
                return;
            }
            OldNetTileBrush.FillRect(segLeft, top, segRight, surface, Z2Style.RoomBrick);
            OldNetTileBrush.CarveRect(segLeft + 2, top + 2, segRight - 2, surface - 1, Z2Style.RoomWall);
            //门洞开向撕裂口一侧（3 高）
            int doorX = doorOnRight ? segRight - 2 : segLeft;
            OldNetTileBrush.CarveRect(doorX, surface - 4, doorX + 2, surface - 1, Z2Style.RoomWall);
            //外侧船首/船尾收角
            OldNetTileBrush.SetSloped(segLeft, top, Z2Style.RoomBrick, SlopeType.SlopeDownRight);
            OldNetTileBrush.SetSloped(segRight - 1, top, Z2Style.RoomBrick, SlopeType.SlopeDownLeft);
            //舱内节点：地面一枚普通；大舱五成在高处藏加密（要跳一下才够到）
            int innerL = segLeft + 3;
            int innerR = segRight - 3;
            OldNetPlans.Budget.TryPlaceUnderPlain(WorldGen.genRand.Next(innerL, innerR), surface - 2);
            if (segRight - segLeft >= 14 && WorldGen.genRand.NextBool()) {
                OldNetNodeBudget.WriteNodeTile(WorldGen.genRand.Next(innerL, innerR), top + 3,
                    ModContent.TileType<OldNetEncryptedNodeTile>());
            }
        }

        /// <summary>
        /// 冷却塔：中空烟囱——内井通顶敞口，井内横档交替贴壁可攀爬，
        /// 塔基双侧门洞，中段一枚节点。废墟带的纵向地标
        /// </summary>
        internal static int BuildCoolantStacks(OldNetBuildContext ctx, int count) {
            int built = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            for (int s = 0; s < count; s++) {
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(ctx.Area.Left + 40, ctx.Area.Right - 60);
                    int surface = floorTop[left + 5];
                    int h = WorldGen.genRand.Next(24, 33);
                    var footprint = new Rectangle(left, surface - h, 10, h + 2);
                    if (!ctx.Grid.TryReserve(footprint, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildStack(left, surface, h);
                    OldNetPlans.ScatterExclusions.Add(footprint);
                    built++;
                    break;
                }
            }
            return built;
        }

        private static void BuildStack(int left, int surface, int h) {
            int top = surface - h;
            //基座找平 + 筒身（外壳 10 宽、内井 6 宽）
            OldNetTileBrush.FillRect(left, surface, left + 10, surface + 2, Z2Style.RoomBrick);
            OldNetTileBrush.FillRect(left, top, left + 10, surface, Z2Style.RoomBrick);
            OldNetTileBrush.CarveRect(left + 2, top + 1, left + 8, surface - 1, Z2Style.RoomWall);
            //顶口敞开通天（无墙）+ 口缘收角
            OldNetTileBrush.CarveRect(left + 2, top, left + 8, top + 1, WallID.None);
            OldNetTileBrush.SetSloped(left, top, Z2Style.RoomBrick, SlopeType.SlopeDownRight);
            OldNetTileBrush.SetSloped(left + 9, top, Z2Style.RoomBrick, SlopeType.SlopeDownLeft);
            //井内横档：每 6 行一道 3 宽平台，左右交替贴壁
            bool side = false;
            for (int y = surface - 5; y > top + 3; y -= 6) {
                int px = side ? left + 2 : left + 5;
                OldNetTileBrush.PlatformRow(px, px + 3, y, Z2Style.PlatformFrameY);
                side = !side;
            }
            //塔基双侧门洞（3 高）
            OldNetTileBrush.CarveRect(left, surface - 4, left + 2, surface - 1, Z2Style.RoomWall);
            OldNetTileBrush.CarveRect(left + 8, surface - 4, left + 10, surface - 1, Z2Style.RoomWall);
            //中段节点：悬在井心，攀爬中途的糖
            OldNetPlans.Budget.TryPlaceUnderPlain(left + WorldGen.genRand.Next(3, 7), surface - h / 2);
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
