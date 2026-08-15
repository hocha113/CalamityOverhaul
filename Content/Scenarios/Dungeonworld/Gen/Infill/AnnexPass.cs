using System.Text;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //P54 封存副翼:填 L1/L2/L6 活跃区两侧的横向留白。
    //
    //与夹层带(P52)分成两个pass而不是合一:两者的定义域正交(一个吃纵向节距、
    //一个吃横向留白)、锚点不同(主房 vs 层脊)、层集不同,合一只会让日志与
    //耗时都读不出是哪边的账。位置同样卡在P50与P55之间。
    //
    //层集:L1/L2/L6。L3/L4/L5已近全幅布房没有留白可填;
    //L7的四周空隙是"吊在深渊上方"的构图要求(STRUCTURES §2.4-⑦),动不得。
    //====================================================================
    internal class AnnexPass : GenPass
    {
        public AnnexPass() : base("Dungeonworld Annex", 2f) { }

        private static readonly int[] Bands = [0, 1, 5];

        internal static string LastSummary = "-";
        internal static long CarveWrites;

        internal static void Reset() {
            LastSummary = "-";
            CarveWrites = 0;
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "开启封存副翼...";
            Reset();
            long carveBefore = TileBrush.ClearWrites;
            var summary = new StringBuilder();

            for (int i = 0; i < Bands.Length; i++) {
                int bandIndex = Bands[i];
                LayerBuildContext ctx = LayerPlans.ByIndex(bandIndex);
                InfillSkin skin = InfillSkin.For(bandIndex);
                if (ctx == null || skin == null) {
                    CWRMod.Instance.Logger.Warn(
                        $"[Annex] 带{bandIndex}缺上下文或皮肤,跳过(责任=P30/皮肤表)");
                    continue;
                }

                AnnexPlanner.Report report = AnnexPlanner.Build(ctx, skin, WorldGen.genRand);
                //L6两个填充体系都吃,补档只声明一次,交给P52那趟(重复声明=撒布跑两遍)
                if (bandIndex != 5) {
                    ctx.Scatter.AddRange(InfillScatter.For(bandIndex));
                }
                LayerBand band = DungeonworldMetrics.Bands[bandIndex];
                CWRMod.Instance.Logger.Info($"[Annex] {band.Name} {report}");
                if (summary.Length > 0) {
                    summary.Append(' ');
                }
                summary.Append($"{band.Name}:{report.Wings}翼/{report.Rooms}房");

                //两翼都没成=活跃区边界探测或翼宽门槛出了问题,响出来不静默留墙
                if (report.Wings == 0) {
                    CWRMod.Instance.Logger.Error(
                        $"[Annex] {band.Name}零副翼,预计两侧留白仍是实心,责任=活跃区边界探测或翼宽门槛");
                }
                progress.Set((i + 1.0) / Bands.Length);
            }

            CarveWrites = TileBrush.ClearWrites - carveBefore;
            LastSummary = summary.Length > 0 ? summary.ToString() : "none";
            CWRMod.Instance.Logger.Info($"[Annex] 合计新增凿空{CarveWrites}格 [{LastSummary}]");
        }
    }
}
