using System.Text;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Infill
{
    //====================================================================
    //P52 夹层带:填 L4/L5/L6 主结构节距之间的纵向死岩。
    //
    //为什么是独立pass而不是改七个层入口:P50跑完之后,ctx.Grid里正好躺着
    //"全部主内容足印"这一份精确底稿,"还空着的地方"因此有了严格定义;
    //而层文件一行不改,既不冒回归风险,也不与在建的层内容抢文件。
    //
    //位置约束:必须在P50之后(要拿到主内容足印)、P55之前(新区要吃到撒布)。
    //随机消耗整段排在七层之后,既有种子的L1~L7布局逐格不变(R4)。
    //====================================================================
    internal class IntersticePass : GenPass
    {
        public IntersticePass() : base("Dungeonworld Interstice", 2f) { }

        //本器接管的层带索引(与DungeonworldMetrics.Bands同索引)。
        //L3甲板制已吃满纵深、L7悬空构图要留空,两带永不入列;
        //L1/L2只有150行,主内容之间挤不出一层夹层,由封存副翼(P54)横向补。
        private static readonly int[] Bands = [3, 4, 5];

        /// <summary>本次生成的填充计数,GenReport取用</summary>
        internal static string LastSummary = "-";
        /// <summary>本pass新增的凿空格数,与主内容的凿空量分开记,便于单次运行就读出增量</summary>
        internal static long CarveWrites;

        internal static void Reset() {
            LastSummary = "-";
            CarveWrites = 0;
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "凿通夹层...";
            Reset();
            long carveBefore = TileBrush.ClearWrites;
            var summary = new StringBuilder();

            for (int i = 0; i < Bands.Length; i++) {
                int bandIndex = Bands[i];
                LayerBuildContext ctx = LayerPlans.ByIndex(bandIndex);
                InfillSkin skin = InfillSkin.For(bandIndex);
                if (ctx == null || skin == null) {
                    CWRMod.Instance.Logger.Warn(
                        $"[Interstice] 带{bandIndex}缺上下文或皮肤,跳过(责任=P30/皮肤表)");
                    continue;
                }

                IntersticePlanner.Report report = IntersticePlanner.Build(ctx, skin, WorldGen.genRand);
                //补档撒布声明进ctx,P55统一执行(本pass排在P55之前正是为了赶上这趟)
                ctx.Scatter.AddRange(InfillScatter.For(bandIndex));
                LayerBand band = DungeonworldMetrics.Bands[bandIndex];
                CWRMod.Instance.Logger.Info($"[Interstice] {band.Name} {report}");
                if (summary.Length > 0) {
                    summary.Append(' ');
                }
                summary.Append($"{band.Name}:{report.Clusters}簇/{report.Tiers}层/"
                    + $"{report.Utilities + report.Rubbles}房");

                //一簇都没成=死带探测或宿主门槛出了问题,响出来而不是静默留一片实心
                if (report.Clusters == 0) {
                    CWRMod.Instance.Logger.Error(
                        $"[Interstice] {band.Name}零夹层簇(宿主拒{report.HostsRejected}),"
                        + "预计该带节距之间仍是实心,责任=宿主门槛或死带探测");
                }
                progress.Set((i + 1.0) / Bands.Length);
            }

            CarveWrites = TileBrush.ClearWrites - carveBefore;
            LastSummary = summary.Length > 0 ? summary.ToString() : "none";
            CWRMod.Instance.Logger.Info($"[Interstice] 合计新增凿空{CarveWrites}格 [{LastSummary}]");
        }
    }
}
