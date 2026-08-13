using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P50:层内容入口调度(brief §2.5接缝契约,入口契约全文见LayerBuildContext头注释)
    //入口由并行层代理落盘到Gen\Layers\L{n}\,本pass只做调度;
    //入口缺席=该层保持M0空脊形态,记日志不失败(null安全跳过)
    internal class LayerContentPass : GenPass
    {
        public LayerContentPass() : base("Dungeonworld Layer Content", 2f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "铺设层内容...";

            Layers.L1.L1Content.PlanAndBuild(LayerPlans.L1);
            progress.Set(0.5);

            Layers.L2.L2Content.PlanAndBuild(LayerPlans.L2);
            progress.Set(1.0);
        }
    }
}
