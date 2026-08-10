using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //电路族纹样语汇：母线 + 继电器，横平竖直，与生体族的曲线拉开距离

    /// <summary>炮台劫持芯片。炮管从右转向左，底座上一枚翻转的识别标</summary>
    internal class TurretHijackChip : BaseHackProtocolChip<TurretHijack>
    {
        protected override string DiePath =>
            "M -0.62 0.56 L 0.62 0.56 M -0.40 0.56 L -0.40 0.16 L 0.40 0.16 L 0.40 0.56 "
            + "M 0 0.16 L -0.72 -0.36 "
            + "M -0.72 -0.36 L -0.44 -0.34 M -0.72 -0.36 L -0.62 -0.62 "
            + "M 0.24 -0.28 L 0.72 -0.28 M 0.48 -0.52 L 0.48 -0.04";
    }

    /// <summary>机械超频芯片。母线穿过继电器，上方一道过压折线</summary>
    internal class MachineOverclockChip : BaseHackProtocolChip<MachineOverclock>
    {
        protected override string DiePath =>
            "M -0.86 0.34 L -0.34 0.34 M 0.34 0.34 L 0.86 0.34 "
            + "M -0.34 0.06 L 0.34 0.06 L 0.34 0.62 L -0.34 0.62 Z "
            + "M -0.52 -0.24 L -0.16 -0.24 L -0.34 -0.56 L 0.10 -0.56 "
            + "M 0.10 -0.56 L -0.08 -0.86 L 0.44 -0.86";
    }

    /// <summary>电网瘫痪芯片。塔顶发出的两道弧下面，一排节点熄了三盏</summary>
    internal class GridBlackoutChip : BaseHackProtocolChip<GridBlackout>
    {
        protected override string DiePath =>
            "M 0 -0.86 L 0 -0.16 M -0.28 -0.16 L 0.28 -0.16 "
            + "M -0.34 -0.62 Q -0.16 -0.44 -0.34 -0.26 "
            + "M 0.34 -0.62 Q 0.16 -0.44 0.34 -0.26 "
            + "M -0.86 0.30 L 0.86 0.30 "
            + "M -0.56 0.30 L -0.56 0.66 M 0 0.30 L 0 0.66 M 0.56 0.30 L 0.56 0.66 "
            + "M -0.70 0.80 L -0.42 0.80 M -0.14 0.80 L 0.14 0.80 M 0.42 0.80 L 0.70 0.80";
    }
}
