using System.Text;
using Terraria;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones
{
    //====================================================================
    //P54.5 子地带:在 L4/L5/L6 的剩余死岩与既有区表面落三个"层级尺度地带"
    //(暗渠/落灰场/渣汽疏泄,施工图 WAVE2-ENVIRONMENTS §3~§6)。
    //
    //位置约束与 P52/P54 完全同构:必须在 AnnexPass 之后——填充体系吃剩的空档
    //才是本 pass 的定义域;必须在 ScatterPass 之前——新凿区要吃到带级撒布。
    //genRand 消耗整段追加在 P54 之后,只有 P55 及之后的随机流位移(R4,裁决 §1-12)。
    //
    //带序恒 L4→L5→L6(层间禁换序);逐地带 fail loud:选不出址/一段没凿成打 Error,
    //已建半成品保留(全部挂在锚点上,连通性不受损,镜像 GrowCluster 纪律)。
    //不改 ValidatePass:本 pass 自报一行结构化日志,供 GenReport 的 nodes= 涨幅归因。
    //====================================================================
    internal class ZonePass : GenPass
    {
        public ZonePass() : base("Dungeonworld Zones", 1.5f) { }

        /// <summary>本 pass 新增凿空格数(与主内容/填充分开记,单次运行可读增量)</summary>
        internal static long CarveWrites;
        internal static string LastSummary = "-";

        internal static void Reset() {
            CarveWrites = 0;
            LastSummary = "-";
            //回放制:注册表与喷口表随每次生成重建(镜像 L4WaterWorks.Reset 纪律)
            ZoneRegistry.Reset();
            Machines.DungeonworldZoneVents.Reset();
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "沉积子地带...";
            Reset();
            long carveBefore = TileBrush.ClearWrites;
            UnifiedRandom rand = WorldGen.genRand;

            //带序恒定 L4→L5→L6;缺上下文=P30 责任,跳过不硬造
            if (LayerPlans.L4 != null) {
                DrownedCulverts.PlanAndBuild(LayerPlans.L4, rand);
            }
            else {
                CWRMod.Instance.Logger.Warn("[ZonePass] L4缺层上下文,暗渠带跳过(责任=P30)");
            }
            progress.Set(0.45);

            if (LayerPlans.L5 != null) {
                AshfallStratum.PlanAndBuild(LayerPlans.L5, rand);
            }
            else {
                CWRMod.Instance.Logger.Warn("[ZonePass] L5缺层上下文,落灰场跳过(责任=P30)");
            }
            progress.Set(0.75);

            if (LayerPlans.L6 != null) {
                SlagVentBelt.PlanAndBuild(LayerPlans.L6, rand);
            }
            else {
                CWRMod.Instance.Logger.Warn("[ZonePass] L6缺层上下文,渣汽疏泄带跳过(责任=P30)");
            }
            progress.Set(0.95);

            CarveWrites = TileBrush.ClearWrites - carveBefore;
            var zones = new StringBuilder();
            foreach ((ZoneKind kind, Rectangle area) in ZoneRegistry.All) {
                zones.Append($" {kind}@({area.X},{area.Y},{area.Width}x{area.Height})");
            }
            LastSummary = zones.Length > 0 ? zones.ToString() : "none";
            CWRMod.Instance.Logger.Info(
                $"[ZonePass] 子地带收束 新增凿空{CarveWrites}格"
                + $" 喷口{Machines.DungeonworldZoneVents.Vents.Count}"
                + $" 登记{ZoneRegistry.All.Count}矩形:{LastSummary}");
            progress.Set(1.0);
        }
    }
}
