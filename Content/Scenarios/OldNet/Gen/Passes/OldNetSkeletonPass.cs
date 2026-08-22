using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P10 骨架：规划态复位 + 边界 + 黑墙体 + 逐列地板线 + 地下体 + 衰减区开凿
    //只管地形体块；竖井/锚位/结构/撒布归后续 pass（M2a 流水线拆分）
    internal class OldNetSkeletonPass : GenPass
    {
        public OldNetSkeletonPass() : base("OldNet Skeleton", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "重建旧网数据平原...";

            //每次深潜重生成：规划态/计数器/闸门登记全部重置
            //（闸门表不能在 OnWorldLoad 清，生成 pass 先于它运行）
            OldNetGenClock.Reset();
            OldNetTileBrush.ResetForNewGen();
            OldNetPlans.Reset();
            OldNetICEDirector.SealGates.Clear();

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            //逐列地板顶行：出生区全平，其余随机游走起伏；衰减区起伏更凶（信号尽头的破碎地形）
            int[] floorTop = new int[width];
            int wobble = 0;
            for (int x = 0; x < width; x++) {
                if (x < OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols) {
                    wobble = 0;
                }
                else {
                    int amp = x >= OldNetMetrics.FadeLeft
                        ? OldNetMetrics.FadeWobble : OldNetMetrics.FloorWobble;
                    wobble = System.Math.Clamp(wobble + WorldGen.genRand.Next(-1, 2), -amp, amp);
                }
                floorTop[x] = OldNetMetrics.FloorRow + wobble;
            }
            OldNetPlans.FloorTop = floorTop;

            for (int x = 0; x < width; x++) {
                progress.Set(x / (double)(width - 1));
                bool sideBorder = x < OldNetMetrics.BorderThick || x >= width - OldNetMetrics.BorderThick;
                bool wallBody = x < OldNetMetrics.WallCols;
                ushort brick = OldNetMetrics.BandForColumn(x)?.FloorBrick ?? TileID.ObsidianBrick;

                for (int y = 0; y < height; y++) {
                    bool topBottomBorder = y < OldNetMetrics.BorderThick || y >= height - OldNetMetrics.BorderThick;
                    //衰减区不再整块封死（M2a 开凿）：地板线贯通全图
                    bool solid = sideBorder || topBottomBorder || wallBody || y >= floorTop[x];
                    if (solid) {
                        //边界与墙体统一黑曜石砖，地板与地下体用带表砖色
                        ushort type = wallBody || sideBorder || topBottomBorder
                            ? TileID.ObsidianBrick : brick;
                        OldNetTileBrush.SetSolid(x, y, type);
                    }
                    else {
                        OldNetTileBrush.ClearCell(x, y);
                    }
                }
            }

            CWRMod.Instance.Logger.Info(
                $"[OldNet] Skeleton solid={OldNetTileBrush.SolidWrites} air={OldNetTileBrush.ClearWrites}"
                + $" macroSeed={OldNetMetrics.MacroSeed}");
        }
    }
}
