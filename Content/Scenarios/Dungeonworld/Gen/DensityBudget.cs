namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //密度预算(用户拍板的"防实心大陆"机制,指标定义见STRUCTURES §3.5):
    //两档制——硬下限fail loud(回归护栏语义:防未来改动劣化,不追求理想值),
    //目标值只进report-only字段,Wave-2提档
    internal readonly struct DensityBudget(bool hardEnabled, int minNodes, int nodeTarget,
        int maxBlankRun, double minCarvePercent, double carveIdealPercent)
    {
        //本带是否启用硬闸(2026-08-15起七带全开;关掉即退回report-only)
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
    //
    //2026-08-15:L3~L7全部转硬闸。取线沿用L1/L2那套"代码路径推演+现状必过"的
    //保守法(本地无游戏环境),三条线一律只拦灾难级退化——层内容整个没生成、
    //活跃区塌成一条缝、房间全被占用栅格拒了这一类,不追求理想值。
    //节点下限的依据是各层自己已经在打的花名册下限告警(层内容入口里那段Warn),
    //取其和的六到八成:
    //  L3 厅3+塔4+迷8+禁1=16 → 14   L4 廊12+阀3+主泵1+落水1=17 → 14
    //  L5 无自带下限,序列排27~29 → 12   L6 廊10+厅2+库3+主控1=16 → 12
    //  L7 前庭/渡桥/东舱/终库/钟龛=5个固定图节点(倒吊中殿不入图) → 4
    //空白段按各层活跃宽度推:L3~L5全幅布房(廊近乎横贯)→400;
    //L6活跃区SpawnX±780,两侧各余约178列本就空白→500;
    //L7只有400~600宽的悬空舱,其余全实心是设计如此→1400。
    //挖空率下限压到L1/L2同档以下(深层大房多,实际值应远高于此)。
    //
    //【仍待回填】三条线的准值要用首次QA的GenReport density[]实测重标:
    //MinCarvePercent=实测x0.85、MaxBlankRun=实测x1.2、MinNodes对D表下沿复核。
    //在那之前这五行只是护栏,不是标尺;理想挖空率一栏留0表示"尚无实测基线"。
    internal static class DensityBudgets
    {
        internal static readonly DensityBudget[] ByBand = [
            //L1教堂区:硬闸启用
            new(true, 8, 12, 800, 3.0, 25.0),
            //L2牢狱层:硬闸启用(NodeTarget=16含Boss预留,D表§5)
            new(true, 10, 16, 800, 3.0, 35.0),
            //L3大档案馆:21层甲板廊近乎横贯全幅,空白段大了就是廊没成段
            new(true, 14, 36, 400, 2.0, 0.0),
            //L4水牢:五组干湿带全幅布房
            new(true, 14, 30, 400, 2.0, 0.0),
            //L5万骨窖:六地层全幅布房,坑道游走连接
            new(true, 12, 30, 400, 2.0, 0.0),
            //L6铸造机关层:Z字折限在SpawnX±780,两侧天然空白
            new(true, 12, 26, 500, 1.5, 0.0),
            //L7倒吊教堂:悬空紧凑舱,四周留空是设计(§2.4-⑦),空白线必须放宽
            new(true, 4, 6, 1400, 0.5, 0.0),
        ];
    }
}
