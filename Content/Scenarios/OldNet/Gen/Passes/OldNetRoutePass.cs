using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones;
using System;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P20 路网：竖井（地表→浅层→深层）+ 平台厅。宏观足印记录进 OldNetPlans，
    //P30 MarkUnchecked 后带内容构造性避让
    internal class OldNetRoutePass : GenPass
    {
        public OldNetRoutePass() : base("OldNet Route", 0.3f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "凿通旧网竖井...";

            //浅层井位：Z1 一口 / Z2 两口 / Z3 一口，段内随机
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;
            CarveShaft(PickCol(OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols + 80,
                ruinLeft - 60), OldNetMetrics.UnderShallowFloorRow, deep: false);
            progress.Set(0.25);
            CarveShaft(PickCol(ruinLeft + 40, ruinLeft + 350),
                OldNetMetrics.UnderShallowFloorRow, deep: false);
            progress.Set(0.45);
            CarveShaft(PickCol(ruinLeft + 470, OldNetMetrics.FadeLeft - 80),
                OldNetMetrics.UnderShallowFloorRow, deep: false);
            progress.Set(0.65);
            CarveShaft(PickCol(OldNetMetrics.FadeLeft + 80, OldNetMetrics.PlayRight - 80),
                OldNetMetrics.UnderShallowFloorRow, deep: false);
            progress.Set(0.8);

            //深层井：自废墟带首口浅井的平台厅地板继续向下
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                if (shaft.Deep || OldNetMetrics.BandIndexForColumn(shaft.Col) != 2) {
                    continue;
                }
                int deepCol = shaft.Landing.Left + 2;
                CarveShaft(deepCol, OldNetMetrics.UnderDeepFloorRow,
                    deep: true, mouthOverride: shaft.Landing.Bottom);
                break;
            }
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info($"[OldNet] Route shafts={OldNetPlans.Shafts.Count}");
        }

        private static int PickCol(int min, int max) => WorldGen.genRand.Next(min, max);

        /// <summary>
        /// 凿一口竖井到目标层：厅壳→厅内膛→井衬里→井内腔→井口盖板与歇脚平台。
        /// mouthOverride 供深井使用（井口=浅层厅地板行）
        /// </summary>
        private static void CarveShaft(int col, int landingFloorRow, bool deep, int mouthOverride = -1) {
            int bandIndex = Math.Max(OldNetMetrics.BandIndexForColumn(col), 1);
            ushort brick = OldNetZoneStyleMap.RoomBrick(bandIndex);
            ushort wall = OldNetZoneStyleMap.RoomWall(bandIndex);
            short frameY = OldNetZoneStyleMap.PlatformFrameY(bandIndex);
            int w = OldNetMetrics.ShaftWidth;

            //井口行：地表井取井宽内最高地板；深井取传入的厅地板行
            int mouthRow = mouthOverride;
            if (mouthRow < 0) {
                mouthRow = int.MaxValue;
                for (int i = 0; i < w; i++) {
                    mouthRow = Math.Min(mouthRow, OldNetPlans.FloorTop[col + i]);
                }
            }

            //平台厅（内膛）横向居中于井
            int landingLeft = col - (OldNetMetrics.LandingW - w) / 2;
            var landing = new Rectangle(landingLeft, landingFloorRow - OldNetMetrics.LandingH,
                OldNetMetrics.LandingW, OldNetMetrics.LandingH);

            //厅壳与内膛
            OldNetTileBrush.FillRect(landing.Left - 2, landing.Top - 2,
                landing.Right + 2, landing.Bottom + 2, brick);
            OldNetTileBrush.CarveRect(landing.Left, landing.Top, landing.Right, landing.Bottom, wall);

            //井体：两侧衬里 + 内腔（内腔穿透厅顶壳，井厅贯通）
            OldNetTileBrush.FillRect(col - 1, mouthRow, col, landing.Top, brick);
            OldNetTileBrush.FillRect(col + w, mouthRow, col + w + 1, landing.Top, brick);
            OldNetTileBrush.CarveRect(col, mouthRow, col + w, landing.Top, wall);

            //井口盖板（地表行走连续，S 键可下）+ 歇脚平台（借平台可跳跃上行）
            OldNetTileBrush.PlatformRow(col, col + w, mouthRow, frameY);
            for (int y = mouthRow + OldNetMetrics.ShaftLedgeStep; y < landing.Top - 2;
                y += OldNetMetrics.ShaftLedgeStep) {
                OldNetTileBrush.PlatformRow(col, col + w, y, frameY);
            }

            OldNetPlans.Shafts.Add(new OldNetShaft(col, mouthRow, landing, deep));
            OldNetPlans.MacroFootprints.Add(new Rectangle(
                col - 2, mouthRow - 2, w + 4, landing.Top - mouthRow + 4));
            OldNetPlans.MacroFootprints.Add(new Rectangle(
                landing.Left - 4, landing.Top - 4, landing.Width + 8, landing.Height + 8));
        }
    }
}
