namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //密度预算(用户拍板的"防实心大陆"机制,指标定义见STRUCTURES §3.5):
    //两档制——硬下限fail loud(回归护栏语义:防未来改动劣化,不追求理想值),
    //目标值只进report-only字段,Wave-2提档
    internal readonly struct DensityBudget(bool hardEnabled, int minNodes, int nodeTarget,
        int maxBlankRun, double minCarvePercent, double carveIdealPercent)
    {
        //本带是否启用硬闸(Wave-1仅L1/L2;L3~L7未穿衣,report-only)
        internal readonly bool HardEnabled = hardEnabled;
        //硬线:节点数下限【待回填】
        internal readonly int MinNodes = minNodes;
        //report-only:D表ROOMS-INDEX §5节点档下沿,Wave-2提档为硬线
        internal readonly int NodeTarget = nodeTarget;
        //硬线:沿脊最大空白段(列)【待回填=实测+20%取整】
        internal readonly int MaxBlankRun = maxBlankRun;
        //硬线:挖空率%下限【待回填=实测x0.85】
        internal readonly double MinCarvePercent = minCarvePercent;
        //report-only:理想挖空率%
        internal readonly double CarveIdealPercent = carveIdealPercent;
    }

    //与DungeonworldMetrics.Bands同索引
    //
    //【待回填】三条硬线为"先测量后定线"的保守临时值(本地无游戏环境,只能代码路径推演;
    //取线原则=现状必过,只拦灾难级退化),待用户首次QA的GenReport density[]字段回填:
    //  1.MinNodes  ← 与D表下沿(L1=12,L2=16)对照后由用户定;实况:L1图节点上限=11+教堂主体1=12
    //    (卫星房允许落位缺席),恰贴D下沿,直接取12会在缺席种子上刷错,故临时线放低
    //  2.MaxBlankRun ← 实测值上浮20%取整(L2活跃区SpawnX±600之外全实心,西缘空白段推演≈400级)
    //  3.MinCarvePercent ← 实测值x0.85(推演:脊6行+房间群,L1/L2约5~10%量级)
    internal static class DensityBudgets
    {
        internal static readonly DensityBudget[] ByBand = [
            //L1教堂区:硬闸启用
            new(true, 8, 12, 800, 3.0, 25.0),
            //L2牢狱层:硬闸启用(NodeTarget=16含Boss预留,D表§5)
            new(true, 10, 16, 800, 3.0, 35.0),
            //L3~L7:report-only,目标值取D表§5节点档下沿,Wave-2逐层启用
            new(false, 0, 36, 0, 0.0, 0.0),
            new(false, 0, 30, 0, 0.0, 0.0),
            new(false, 0, 30, 0, 0.0, 0.0),
            new(false, 0, 26, 0, 0.0, 0.0),
            new(false, 0, 6, 0, 0.0, 0.0),
        ];
    }
}
