using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //液体族纹样语汇：波纹 + 液面，纹样一律横向铺开

    /// <summary>电解水域芯片。两道波纹之间劈下一道折线电弧</summary>
    internal class ElectrolysisChip : BaseHackProtocolChip<Electrolysis>
    {
        protected override string DiePath =>
            "M -0.82 0.40 Q -0.54 0.20 -0.26 0.40 Q 0.02 0.60 0.30 0.40 Q 0.58 0.20 0.84 0.40 "
            + "M -0.82 0.68 Q -0.54 0.48 -0.26 0.68 Q 0.02 0.88 0.30 0.68 Q 0.58 0.48 0.84 0.68 "
            + "M -0.10 -0.82 L 0.22 -0.28 L -0.14 -0.20 L 0.16 0.30";
    }

    /// <summary>冷凝固化芯片。波纹上方结出六角冰晶</summary>
    internal class CryostasisChip : BaseHackProtocolChip<Cryostasis>
    {
        protected override string DiePath =>
            "M -0.82 0.62 Q -0.50 0.42 -0.18 0.62 Q 0.14 0.82 0.46 0.62 Q 0.66 0.50 0.84 0.62 "
            + "M -0.02 -0.72 L -0.02 0.18 M -0.42 -0.48 L 0.38 0.00 M 0.38 -0.48 L -0.42 0.00 "
            + "M -0.02 -0.44 L -0.24 -0.62 M -0.02 -0.44 L 0.20 -0.62";
    }

    /// <summary>液体抽排芯片。液面下沉，底部一道排口与三滴下落</summary>
    internal class LiquidPurgeChip : BaseHackProtocolChip<LiquidPurge>
    {
        protected override string DiePath =>
            "M -0.80 -0.52 Q -0.48 -0.72 -0.16 -0.52 Q 0.16 -0.32 0.48 -0.52 Q 0.68 -0.64 0.82 -0.52 "
            + "M -0.46 -0.10 L -0.46 0.30 M 0 -0.10 L 0 0.42 M 0.46 -0.10 L 0.46 0.30 "
            + "M -0.62 0.66 L 0.62 0.66 M -0.62 0.66 L -0.42 0.88 M 0.62 0.66 L 0.42 0.88";
    }
}
