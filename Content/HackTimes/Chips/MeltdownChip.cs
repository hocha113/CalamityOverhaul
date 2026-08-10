using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    /// <summary>熔毁协议芯片。晶粒纹＝临界的反应核，四向裂纹从核心炸开</summary>
    internal class MeltdownChip : BaseHackProtocolChip<MeltdownProtocol>
    {
        protected override string DiePath =>
            "M 0 -0.44 L 0.38 0 L 0 0.44 L -0.38 0 Z "
            + "M 0 -0.44 L 0 -0.80 "
            + "M 0.38 0 L 0.60 0 L 0.72 -0.15 "
            + "M 0 0.44 L 0 0.80 "
            + "M -0.38 0 L -0.60 0 L -0.72 0.15 "
            + "M -0.19 -0.22 L -0.47 -0.52 "
            + "M 0.19 0.22 L 0.47 0.52";
    }
}
