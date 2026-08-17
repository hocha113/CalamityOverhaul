using System.Diagnostics;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Passes
{
    //P90 收尾：全图帧修 + 生成报告
    //子世界任务表没有原版收尾帧修，直写 tile 之后必须自调 RangeFrame，否则砖面全是错帧
    //永远排在 Tasks 最后一位
    internal class KiyumeFinalizePass : GenPass
    {
        public KiyumeFinalizePass() : base("Kiyume Finalize", 0.5f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "岸线定形...";
            Stopwatch watch = Stopwatch.StartNew();

            WorldGen.RangeFrame(0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1);
            progress.Set(0.9);

            Main.refreshMap = true;
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[Kiyume] GenReport frame={watch.ElapsedMilliseconds}ms"
                + $" brush[solid={KiyumeTileBrush.SolidWrites} clear={KiyumeTileBrush.ClearWrites}"
                + $" liquid={KiyumeTileBrush.LiquidWrites}]"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY})");
        }
    }
}
