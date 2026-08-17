using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P30 分带规划：纯数据零tile写入——建立四带上下文、宏观足印入格、
    //锚位裁决（封锁区/中继在栅格上互斥落位，撤销一切硬编码列位）
    internal class OldNetZonePlanPass : GenPass
    {
        public OldNetZonePlanPass() : base("OldNet ZonePlan", 0.2f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "规划旧网分带...";

            int top = OldNetMetrics.BorderThick;
            int height = OldNetMetrics.UnderDeepBottom - top;
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;

            OldNetPlans.Z1 = new OldNetBuildContext("墙脚带",
                new Rectangle(OldNetMetrics.WallCols, top, OldNetMetrics.FootCols, height));
            OldNetPlans.Z2 = new OldNetBuildContext("废墟带",
                new Rectangle(ruinLeft, top, OldNetMetrics.RuinCols, height));
            OldNetPlans.Z3 = new OldNetBuildContext("衰减区",
                new Rectangle(OldNetMetrics.FadeLeft, top,
                    OldNetMetrics.PlayRight - OldNetMetrics.FadeLeft, height));
            OldNetPlans.Z4 = new OldNetBuildContext("高空带",
                new Rectangle(OldNetMetrics.BorderThick, top,
                    OldNetMetrics.Width - OldNetMetrics.BorderThick * 2,
                    OldNetMetrics.SkyBandBottom - top));

            //P20 宏观足印入格：竖井/平台厅先占位，带内容构造性避让
            foreach (Rectangle fp in OldNetPlans.MacroFootprints) {
                OldNetPlans.Z1.Grid.MarkUnchecked(fp);
                OldNetPlans.Z2.Grid.MarkUnchecked(fp);
                OldNetPlans.Z3.Grid.MarkUnchecked(fp);
                OldNetPlans.Z4.Grid.MarkUnchecked(fp);
            }

            //出生区与登出终端足印：预留 + 零撒布
            var spawnStrip = new Rectangle(OldNetMetrics.WallCols,
                OldNetMetrics.FloorRow - 40, OldNetMetrics.SpawnFlatCols + 10, 50);
            OldNetPlans.Z1.Grid.MarkUnchecked(spawnStrip);
            OldNetPlans.ScatterExclusions.Add(spawnStrip);
            progress.Set(0.4);

            //锚位裁决：先封锁区（大足印）后中继，栅格保证互斥且避开竖井
            PlanSealBoxes(ruinLeft);
            progress.Set(0.7);
            PlanRelays(ruinLeft);
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[OldNet] ZonePlan sealBoxes={OldNetPlans.SealBoxes.Count}"
                + $" relays={OldNetPlans.RelaySpots.Count}");
        }

        //封锁区：废墟带等分段各一盒，段内随机试位，栅格拒绝即重掷
        private static void PlanSealBoxes(int ruinLeft) {
            int segW = OldNetMetrics.RuinCols / OldNetMetrics.SealBoxCount;
            for (int b = 0; b < OldNetMetrics.SealBoxCount; b++) {
                int segLeft = ruinLeft + b * segW + 40;
                int segRight = ruinLeft + (b + 1) * segW - 40;
                for (int attempt = 0; attempt < 24; attempt++) {
                    int cx = WorldGen.genRand.Next(segLeft, segRight);
                    int x0 = cx - OldNetMetrics.SealBoxW / 2;
                    int surface = OldNetPlans.FloorTop[cx];
                    var box = new Rectangle(x0, surface - OldNetMetrics.SealBoxH,
                        OldNetMetrics.SealBoxW, OldNetMetrics.SealBoxH);
                    if (!OldNetPlans.Z2.Grid.TryReserve(box, OldNetMetrics.AnchorPadding)) {
                        continue;
                    }
                    OldNetPlans.SealBoxes.Add(box);
                    OldNetPlans.ScatterExclusions.Add(new Rectangle(
                        box.X - 3, box.Y - 4, box.Width + 6, box.Height + 8));
                    break;
                }
            }
        }

        //中继站：段内偏中随机，避让封锁区与竖井由栅格兜底
        private static void PlanRelays(int ruinLeft) {
            int segW = OldNetMetrics.RuinCols / OldNetMetrics.RelayCount;
            for (int i = 0; i < OldNetMetrics.RelayCount; i++) {
                int segLeft = ruinLeft + i * segW + segW / 4;
                int segRight = ruinLeft + i * segW + segW * 3 / 4;
                for (int attempt = 0; attempt < 24; attempt++) {
                    int x = WorldGen.genRand.Next(segLeft, segRight);
                    int floorY = OldNetPlans.FloorTop[x];
                    var footprint = new Rectangle(x - 4, floorY - 8, 10, 10);
                    if (!OldNetPlans.Z2.Grid.TryReserve(footprint, OldNetMetrics.AnchorPadding)) {
                        continue;
                    }
                    OldNetPlans.RelaySpots.Add(new Point(x, floorY));
                    OldNetPlans.ScatterExclusions.Add(new Rectangle(
                        footprint.X - 2, footprint.Y - 2, footprint.Width + 4, footprint.Height + 4));
                    break;
                }
            }
        }
    }
}
