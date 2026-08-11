using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //骨架族纹样语汇：节段链 + 断口/关节销，纹样都围着「拆开的连接」长

    /// <summary>节段离网芯片。五节链条，中间一节被拔出，断口两侧留销孔</summary>
    internal class SegmentDelinkChip : BaseHackProtocolChip<SegmentDelink>
    {
        //纹样一律收在 ±0.80 内；孤立销孔写成单点子路径（M x y），零长 M..L 会被当线段丢掉
        protected override string DiePath =>
            "M -0.80 -0.10 L -0.50 -0.10 M -0.42 -0.10 L -0.12 -0.10 "
            + "M 0.12 -0.10 L 0.42 -0.10 M 0.50 -0.10 L 0.80 -0.10 "
            + "M -0.46 -0.10 M 0.46 -0.10 "
            + "M -0.15 0.45 L 0.15 0.45 "
            + "M -0.06 -0.10 M 0.06 -0.10 "
            + "M -0.15 0.45 L -0.15 0.58 M 0.15 0.45 L 0.15 0.58";
    }

    /// <summary>肢体征收芯片。一根关节臂从躯干伸出后向内折回，肘部一枚销钉，臂端箭簇指向躯干</summary>
    internal class LimbSeizureChip : BaseHackProtocolChip<LimbSeizure>
    {
        protected override string DiePath =>
            "M -0.70 -0.50 L -0.38 -0.50 L -0.38 0.50 L -0.70 0.50 Z "
            + "M -0.38 -0.20 L 0.45 -0.35 L 0.05 0.30 "
            + "M 0.45 -0.35 "
            + "M 0.05 0.30 L 0.24 0.24 M 0.05 0.30 L 0.20 0.44";
    }

    /// <summary>协同断链芯片。三个节点由链路相连，右段链断成两截，断口各留一枚销孔</summary>
    internal class CommandLinkCutChip : BaseHackProtocolChip<CommandLinkCut>
    {
        protected override string DiePath =>
            "M -0.74 -0.08 L -0.58 -0.08 L -0.58 0.08 L -0.74 0.08 Z "
            + "M -0.08 -0.08 L 0.08 -0.08 L 0.08 0.08 L -0.08 0.08 Z "
            + "M 0.58 -0.08 L 0.74 -0.08 L 0.74 0.08 L 0.58 0.08 Z "
            + "M -0.58 0 L -0.08 0 "
            + "M 0.08 -0.02 L 0.26 -0.06 M 0.40 0.06 L 0.58 0.02 "
            + "M 0.30 -0.14 M 0.36 0.14";
    }
}
