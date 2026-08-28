using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen.Passes
{
    //P40 结构：废村组团 + 村井
    //Terrain 之后（贴面与洼水已就位）、Scatter 之前（撒布按禁区让路）
    internal class KiameStructurePass : GenPass
    {
        public KiameStructurePass() : base("Kiame Structure", 0.8f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiamePlans.Report(progress, "空屋还立在雨里...");
            CWRMod.Instance.Logger.Info("[Kiame] Structure start");

            KiameRuins.Build();
            //地基平整改写过逐列地板，出生行按回写后的规划重取
            Main.spawnTileY = KiamePlans.FloorTopAt(KiameMetrics.SpawnX);
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[Kiame] Structure 残屋={KiameRuins.Huts} 沉水户={KiameRuins.Sunken} 井={KiameRuins.Wells}");
        }
    }
}
