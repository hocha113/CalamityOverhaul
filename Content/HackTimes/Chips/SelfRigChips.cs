using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //自体族纹样语汇：人形轮廓 + 内环仪表，永远居中对称；电路走线绕人形布设，
    //读作"电路叠在人身上"。孤立点写单点子路径（M x y），零长 M..L 会被按线段丢掉

    /// <summary>电能折算芯片。人形胸腔一枚电池，两条引线对称绕成下半内环刻度</summary>
    internal class PowerTransmuteChip : BaseHackProtocolChip<PowerTransmute>
    {
        protected override string DiePath =>
            "M -0.10 -0.66 L 0.10 -0.66 L 0.10 -0.48 L -0.10 -0.48 Z "
            + "M -0.30 -0.38 L 0.30 -0.38 L 0.22 0.26 L -0.22 0.26 Z "
            + "M -0.10 -0.20 L 0.10 -0.20 L 0.10 0.08 L -0.10 0.08 Z "
            + "M -0.04 -0.26 L 0.04 -0.26 "
            + "M -0.10 -0.06 L -0.42 -0.06 Q -0.64 -0.06 -0.64 0.18 Q -0.64 0.44 -0.36 0.54 "
            + "M 0.10 -0.06 L 0.42 -0.06 Q 0.64 -0.06 0.64 0.18 Q 0.64 0.44 0.36 0.54 "
            + "M -0.52 0.36 M 0 0.58 M 0.52 0.36";
    }

    /// <summary>神经超频芯片。人形头部向外辐射七道短线，下半内环刻度被指针推过最后一格</summary>
    internal class NeuralOverclockChip : BaseHackProtocolChip<NeuralOverclock>
    {
        protected override string DiePath =>
            "M -0.10 -0.50 L 0.10 -0.50 L 0.10 -0.30 L -0.10 -0.30 Z "
            + "M -0.26 -0.20 L 0.26 -0.20 L 0.20 0.30 L -0.20 0.30 Z "
            + "M 0 -0.78 V -0.62 "
            + "M -0.24 -0.74 L -0.15 -0.60 M 0.24 -0.74 L 0.15 -0.60 "
            + "M -0.44 -0.62 L -0.30 -0.50 M 0.44 -0.62 L 0.30 -0.50 "
            + "M -0.58 -0.42 L -0.40 -0.36 M 0.58 -0.42 L 0.40 -0.36 "
            + "M -0.56 0.50 Q 0 0.78 0.56 0.50 "
            + "M -0.42 0.58 L -0.37 0.48 M 0 0.68 V 0.58 M 0.42 0.58 L 0.37 0.48 "
            + "M 0 0.26 L 0.60 0.64";
    }

    /// <summary>役鬼强驱芯片。人形背后一道虚线鬼影，两者之间一根绷紧带结的绳</summary>
    internal class WraithForceDriveChip : BaseHackProtocolChip<WraithForceDrive>
    {
        protected override string DiePath =>
            "M 0.18 -0.52 L 0.36 -0.52 L 0.36 -0.34 L 0.18 -0.34 Z "
            + "M 0.10 -0.24 L 0.44 -0.24 L 0.38 0.34 L 0.16 0.34 Z "
            + "M -0.50 -0.50 Q -0.34 -0.64 -0.20 -0.50 "
            + "M -0.54 -0.38 L -0.52 -0.26 M -0.58 -0.14 L -0.54 -0.02 "
            + "M -0.58 0.12 L -0.52 0.24 M -0.54 0.36 L -0.44 0.46 "
            + "M -0.20 -0.10 L -0.08 -0.07 M -0.08 -0.07 Q -0.02 -0.14 0.02 -0.05 "
            + "M 0.02 -0.05 L 0.10 -0.02 L 0.16 -0.01";
    }
}
