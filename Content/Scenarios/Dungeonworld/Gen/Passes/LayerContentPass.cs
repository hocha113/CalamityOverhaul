using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P50:层内容入口调度(brief §2.5/§2.7接缝契约,入口契约全文见LayerBuildContext头注释)
    //入口由并行层代理落盘到Gen\Layers\L{n}\,本pass只做调度;
    //入口缺席=该层保持M0空脊形态,Warn日志不失败;
    //调度顺序自上而下L1→L7=P50随机消耗顺序(R4:层间禁止换序,新层只能按带序插入)
    internal class LayerContentPass : GenPass
    {
        public LayerContentPass() : base("Dungeonworld Layer Content", 2f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "铺设层内容...";
            var log = CWRMod.Instance.Logger;

            //L1教堂区(Wave-1已接线)
            Layers.L1.L1Content.PlanAndBuild(LayerPlans.L1);
            progress.Set(1 / 7.0);

            //L2牢狱层(Wave-1已接线)
            Layers.L2.L2Content.PlanAndBuild(LayerPlans.L2);
            progress.Set(2 / 7.0);

            //L3大档案馆(Wave-2槽位)——入口落盘后用下行替换本段Warn:
            //Layers.L3.L3Content.PlanAndBuild(LayerPlans.L3);
            //TODO(父级缝合):L3路交付后按上行接线
            log.Warn("[Dungeonworld] P50 L3内容入口未接线,保持M0空脊");
            progress.Set(3 / 7.0);

            //L4水牢(Wave-2槽位)——入口落盘后用下行替换本段Warn:
            //Layers.L4.L4Content.PlanAndBuild(LayerPlans.L4);
            //TODO(父级缝合):L4路交付后按上行接线
            log.Warn("[Dungeonworld] P50 L4内容入口未接线,保持M0空脊");
            progress.Set(4 / 7.0);

            //L5万骨窖(Wave-2槽位)——入口落盘后用下行替换本段Warn:
            //Layers.L5.L5Content.PlanAndBuild(LayerPlans.L5);
            //TODO(父级缝合):L5路交付后按上行接线
            log.Warn("[Dungeonworld] P50 L5内容入口未接线,保持M0空脊");
            progress.Set(5 / 7.0);

            //L6铸造机关层(Wave-2槽位)——入口落盘后用下行替换本段Warn:
            //Layers.L6.L6Content.PlanAndBuild(LayerPlans.L6);
            //TODO(父级缝合):L6路交付后按上行接线
            log.Warn("[Dungeonworld] P50 L6内容入口未接线,保持M0空脊");
            progress.Set(6 / 7.0);

            //L7倒吊教堂(Wave-2槽位)——入口落盘后用下行替换本段Warn:
            //Layers.L7.L7Content.PlanAndBuild(LayerPlans.L7);
            //TODO(父级缝合):L7路交付后按上行接线
            log.Warn("[Dungeonworld] P50 L7内容入口未接线,保持M0空脊");
            progress.Set(1.0);
        }
    }
}
