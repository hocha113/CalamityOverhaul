using System.Text;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes
{
    //P55:表驱动撒布装饰(引擎=原版三段模式F30,密度档引用D表ROOMS-INDEX §7矩阵)
    //条目来源=A路内置跨层通用首批+层代理经ctx.Scatter声明;Wave-2循环全七带
    //在P50层内容之后执行:撒布只装修已凿空区,几何已冻结(§3.1-3装修单向性)
    internal class ScatterPass : GenPass
    {
        public ScatterPass() : base("Dungeonworld Scatter", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "撒布装饰...";
            var report = new StringBuilder();

            //L1按矩阵:蛛网=零,杂物=低(蜡烬)——蜡烬无原版fallback家具,
            //按Wave-1资产纪律执行保守解:A路不内置,留给L1路经ctx.Scatter声明【待签字】
            RunLayer(LayerPlans.L1, [], report);
            progress.Set(0.3);

            //L2按矩阵:蛛网=低,杂物=标(骨堆/罐)→骨堆标+罐低;
            //认领表"L2地面骨堆≤2件/囚室"由去重距8(与囚室宽6~9同量级)近似保证
            RunLayer(LayerPlans.L2, [
                CommonScatter.Cobweb(ScatterDensity.Low),
                CommonScatter.BonePiles(ScatterDensity.Standard),
                CommonScatter.DungeonPots(ScatterDensity.Low),
            ], report);
            progress.Set(0.5);

            //L3~L7(Wave-2):A路不内置通用条目——INDEX §7矩阵中蛛网/骨堆/罐的
            //档位(如L5蛛网峰+骨堆峰)属层母题认领,由各层代理经ctx.Scatter自行
            //声明(CommonScatter工厂对层代理可用),防止内置与层声明双重覆盖;
            //本循环只兑现各层已声明条目,单条目MaxPlaced上限内建(R5耗时保险)
            for (int i = 2; i < DungeonworldMetrics.Bands.Length; i++) {
                RunLayer(LayerPlans.ByIndex(i), [], report);
            }
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info($"[Dungeonworld] P55 Scatter{report}"
                + $" 总计placed={ScatterEngine.TotalPlaced} attempts={ScatterEngine.TotalAttempts}");
        }

        private static void RunLayer(LayerBuildContext ctx, ScatterEntry[] builtin, StringBuilder report) {
            if (ctx == null) {
                return;
            }
            foreach (ScatterEntry entry in builtin) {
                Execute(ctx, entry, report);
            }
            foreach (ScatterEntry entry in ctx.Scatter) {
                Execute(ctx, entry, report);
            }
        }

        private static void Execute(LayerBuildContext ctx, ScatterEntry entry, StringBuilder report) {
            (int placed, int attempts) = ScatterEngine.Run(ctx.Band, entry);
            report.Append($" {ctx.Band.Name}.{entry.Name}={placed}/{attempts}");
        }
    }
}
