using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //容器族纹样语汇：箱体剖面 + 锁栓/槽格，读作"看进一只箱子"
    //世界族纹样语汇：地平线 + 天体/垂线，唯一一族底部固定一条地平线
    //（重力反转是全族唯一的例外——它的地平线画在顶部，本身就是纹义）

    /// <summary>索引预读芯片。箱体剖开露出三格槽位，一道视线从箱外斜穿进最亮那格</summary>
    internal class IndexPrereadChip : BaseHackProtocolChip<IndexPreread>
    {
        protected override string DiePath =>
            "M -0.62 -0.36 L 0.62 -0.36 L 0.62 0.56 L -0.62 0.56 Z "
            + "M -0.62 -0.12 L 0.62 -0.12 "
            + "M -0.21 -0.12 L -0.21 0.56 M 0.21 -0.12 L 0.21 0.56 "
            + "M -0.78 -0.72 L -0.34 -0.48 L 0.34 0.10 "
            + "M 0.42 0.26";
    }

    /// <summary>锁芯烧穿芯片。箱面一枚锁栓，锁芯烧出一个孔，孔外三道焦裂纹</summary>
    internal class LockBurnChip : BaseHackProtocolChip<LockBurn>
    {
        protected override string DiePath =>
            "M -0.62 -0.42 L 0.62 -0.42 L 0.62 0.58 L -0.62 0.58 Z "
            + "M -0.62 -0.16 L 0.62 -0.16 "
            + "M -0.16 -0.02 L 0.16 -0.02 L 0.16 0.34 L -0.16 0.34 Z "
            + "M 0 0.16 "
            + "M 0.10 0.10 L 0.34 -0.06 M -0.10 0.10 L -0.34 -0.04 M 0.02 0.26 L 0.10 0.50";
    }

    /// <summary>昼夜跳转芯片。地平线上一枚半沉日轮，一道弧箭跨到对侧月牙</summary>
    internal class DielSkipChip : BaseHackProtocolChip<DielSkip>
    {
        protected override string DiePath =>
            "M -0.78 0.52 L 0.78 0.52 "
            + "M -0.62 0.52 Q -0.62 0.20 -0.36 0.20 Q -0.10 0.20 -0.10 0.52 "
            + "M -0.36 0.04 Q 0.06 -0.60 0.48 -0.10 "
            + "M 0.48 -0.10 L 0.30 -0.16 M 0.48 -0.10 L 0.52 -0.30 "
            + "M 0.36 0.20 Q 0.62 0.28 0.56 0.52 "
            + "M 0.36 0.20 Q 0.50 0.34 0.56 0.52";
    }

    /// <summary>雷暴注入芯片。地平线上方一片云弧，三道雷折线各落在一枚靶点上</summary>
    internal class StormInjectChip : BaseHackProtocolChip<StormInject>
    {
        protected override string DiePath =>
            "M -0.78 0.62 L 0.78 0.62 "
            + "M -0.54 -0.30 Q -0.34 -0.56 -0.08 -0.40 Q 0.14 -0.62 0.40 -0.38 "
            + "Q 0.58 -0.28 0.48 -0.14 L -0.44 -0.14 Q -0.64 -0.18 -0.54 -0.30 "
            + "M -0.34 -0.14 L -0.26 0.10 L -0.40 0.14 L -0.30 0.40 "
            + "M 0.02 -0.14 L 0.10 0.08 L -0.04 0.12 L 0.06 0.38 "
            + "M 0.38 -0.14 L 0.46 0.06 L 0.32 0.10 L 0.42 0.36 "
            + "M -0.30 0.50 M 0.06 0.48 M 0.42 0.46";
    }

    /// <summary>重力反转芯片。地平线倒画在顶部，三枚小方块朝上飘，垂线箭头向上</summary>
    internal class GravityInvertChip : BaseHackProtocolChip<GravityInvert>
    {
        protected override string DiePath =>
            "M -0.78 -0.54 L 0.78 -0.54 "
            + "M -0.52 0.06 L -0.34 0.06 L -0.34 0.24 L -0.52 0.24 Z "
            + "M -0.10 0.30 L 0.08 0.30 L 0.08 0.48 L -0.10 0.48 Z "
            + "M -0.16 -0.18 L 0.02 -0.18 L 0.02 0.00 L -0.16 0.00 Z "
            + "M 0.50 0.50 L 0.50 -0.26 "
            + "M 0.50 -0.26 L 0.38 -0.10 M 0.50 -0.26 L 0.62 -0.10";
    }
}
