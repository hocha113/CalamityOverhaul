using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z1;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z4;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P50 带内容：按带分派 PlanAndBuild（扩容=新带文件入列，此处只做调度）
    internal class OldNetZoneContentPass : GenPass
    {
        public OldNetZoneContentPass() : base("OldNet ZoneContent", 0.6f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "重建墙脚接入区...";
            Z1Content.PlanAndBuild(OldNetPlans.Z1);
            progress.Set(0.3);

            progress.Message = "发掘废墟带机房...";
            Z2Content.PlanAndBuild(OldNetPlans.Z2);
            progress.Set(0.7);

            progress.Message = "勘探信号衰减带...";
            Z3Content.PlanAndBuild(OldNetPlans.Z3);
            progress.Set(0.9);

            Z4Content.PlanAndBuild(OldNetPlans.Z4);
            progress.Set(1.0);
        }
    }
}
