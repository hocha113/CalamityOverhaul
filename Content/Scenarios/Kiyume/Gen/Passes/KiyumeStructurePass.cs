using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Passes
{
    //P40 结构：预制件建筑与环境微区的统一入口，排在 Terrain 与 Scatter 之间（裁决5）
    //本包只立通道骨架；结构模块由 W2/W3 各包在下方锚行接入，锚行顺序即调用顺序：
    //信仰轴线（依赖村壳回写的 FloorTop）→ 水缘 → 微区（井/灯道要在前两者足印定形后落点）
    internal class KiyumeStructurePass : GenPass
    {
        public KiyumeStructurePass() : base("Kiyume Structures", 0.8f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            KiyumePlans.Report(progress, "该立的还立在原处...");
            CWRMod.Instance.Logger.Info("[Kiyume] Structures start");

            //──结构挂点：信仰轴线──
            KiyumeShrine.Build(progress);
            //──结构挂点：水缘──
            KiyumeShoalWrecks.Build();
            //──结构挂点：微区──
            KiyumeMicroSites.Build(progress);

            progress.Set(1.0);
            CWRMod.Instance.Logger.Info(
                $"[Kiyume] Structures 禁区={KiyumeStructures.ScatterExclusions.Count}"
                + $" 预留={KiyumeStructures.ReservedSpans.Count}"
                + $" 藏身={KiyumeStructures.HideVolumes.Count}"
                + $" 灯位={KiyumeStructures.LanternPosts.Count}"
                + $" 井={KiyumeStructures.WellMouths.Count}"
                + $" 鸟居={KiyumeStructures.ToriiGates.Count}"
                + $" 社祠={KiyumeStructures.Shrines.Count}"
                + $" 门洞={KiyumeStructures.DoorwayPoints.Count}");
        }
    }
}
