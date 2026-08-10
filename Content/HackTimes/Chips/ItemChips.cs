using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //掉落物族纹样语汇：方框 + 抓取角，读作"框住某件东西"

    /// <summary>远程回收芯片。方框被四角括住，一道箭头把它拽向左下</summary>
    internal class ItemRecallChip : BaseHackProtocolChip<ItemRecall>
    {
        protected override string DiePath =>
            "M -0.16 -0.44 L 0.44 -0.44 L 0.44 0.16 L -0.16 0.16 Z "
            + "M -0.34 -0.62 L -0.34 -0.34 M -0.34 -0.62 L -0.06 -0.62 "
            + "M 0.62 0.34 L 0.62 0.06 M 0.62 0.34 L 0.34 0.34 "
            + "M -0.16 0.16 L -0.72 0.66 M -0.72 0.66 L -0.72 0.34 M -0.72 0.66 L -0.40 0.66";
    }

    /// <summary>品质重掷芯片。方框上方三颗品阶星，一道回环箭头绕回来</summary>
    internal class ReappraiseChip : BaseHackProtocolChip<Reappraise>
    {
        protected override string DiePath =>
            "M -0.40 0.02 L 0.40 0.02 L 0.40 0.62 L -0.40 0.62 Z "
            + "M -0.44 -0.36 M 0 -0.44 M 0.44 -0.36 "
            + "M -0.66 0.32 Q -0.86 -0.30 -0.24 -0.66 Q 0.44 -0.94 0.78 -0.42 "
            + "M 0.78 -0.42 L 0.48 -0.44 M 0.78 -0.42 L 0.80 -0.72";
    }
}
