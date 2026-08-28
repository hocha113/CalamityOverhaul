using System.Diagnostics;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen.Passes
{
    //P90 收尾：全图帧修 + 生成报告
    //子世界任务表没有原版收尾帧修，直写 tile 之后必须自调帧修，否则砖面全是错帧
    //永远排在 Tasks 最后一位
    internal class KiameFinalizePass : GenPass
    {
        public KiameFinalizePass() : base("Kiame Finalize", 0.5f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiamePlans.Report(progress, "雨把一切钉在原地...");
            CWRMod.Instance.Logger.Info("[Kiame] Finalize start");
            Stopwatch watch = Stopwatch.StartNew();

            //RangeFrame 会对每一格（含洼水）走 TileFrame→Liquid.AddWater，坠落砖还会在 gen 线程 NewProjectile。
            //EveryTileFrame 是原版收尾用的安全路径：跳过空气/纯液体、关掉液体入队和坠落动作。
            WorldGen.EveryTileFrame();
            progress.Set(0.9);

            Main.refreshMap = true;
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[Kiame] GenReport frame={watch.ElapsedMilliseconds}ms"
                + $" brush[solid={KiameTileBrush.SolidWrites} clear={KiameTileBrush.ClearWrites}"
                + $" liquid={KiameTileBrush.LiquidWrites}]"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY})");
        }
    }
}
