using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //物块族纹样语汇：格栅方块 + 网格线，一格与众不同

    /// <summary>物块征用芯片。中心一粒人形点，八枚小方块绕圈，其中一枚已飞离轨道</summary>
    internal class TileConscriptChip : BaseHackProtocolChip<TileConscript>
    {
        protected override string DiePath =>
            //人形点：头是单点子路径，零长线段会被按线段丢掉
            "M 0 -0.16 M 0 -0.08 L 0 0.14 "
            //环上七枚方块，东北位空缺
            + "M 0.49 -0.07 L 0.63 -0.07 L 0.63 0.07 L 0.49 0.07 Z "
            + "M 0.33 0.33 L 0.47 0.33 L 0.47 0.47 L 0.33 0.47 Z "
            + "M -0.07 0.49 L 0.07 0.49 L 0.07 0.63 L -0.07 0.63 Z "
            + "M -0.47 0.33 L -0.33 0.33 L -0.33 0.47 L -0.47 0.47 Z "
            + "M -0.63 -0.07 L -0.49 -0.07 L -0.49 0.07 L -0.63 0.07 Z "
            + "M -0.47 -0.47 L -0.33 -0.47 L -0.33 -0.33 L -0.47 -0.33 Z "
            + "M -0.07 -0.63 L 0.07 -0.63 L 0.07 -0.49 L -0.07 -0.49 Z "
            //离轨的那一枚与它的脱离轨迹
            + "M 0.57 -0.71 L 0.71 -0.71 L 0.71 -0.57 L 0.57 -0.57 Z "
            + "M 0.44 -0.44 L 0.52 -0.52";
    }

    /// <summary>矿脉共振芯片。网格三格嵌菱形矿点，一圈波纹自中心格外扩</summary>
    internal class VeinResonanceChip : BaseHackProtocolChip<VeinResonance>
    {
        protected override string DiePath =>
            //三乘三网格线
            "M -0.72 -0.24 L 0.72 -0.24 M -0.72 0.24 L 0.72 0.24 "
            + "M -0.24 -0.72 L -0.24 0.72 M 0.24 -0.72 L 0.24 0.72 "
            //三格菱形矿点
            + "M -0.48 -0.58 L -0.38 -0.48 L -0.48 -0.38 L -0.58 -0.48 Z "
            + "M 0.48 -0.10 L 0.58 0 L 0.48 0.10 L 0.38 0 Z "
            + "M 0 0.38 L 0.10 0.48 L 0 0.58 L -0.10 0.48 Z "
            //自中心格外扩的波纹环
            + "M 0 -0.34 Q 0.34 -0.34 0.34 0 Q 0.34 0.34 0 0.34 "
            + "Q -0.34 0.34 -0.34 0 Q -0.34 -0.34 0 -0.34";
    }

    /// <summary>应力反转芯片。网格封边双线加粗，边中应力线内收，中心格双箭头向内</summary>
    internal class StressInvertChip : BaseHackProtocolChip<StressInvert>
    {
        protected override string DiePath =>
            //双层封边
            "M -0.72 -0.72 L 0.72 -0.72 L 0.72 0.72 L -0.72 0.72 Z "
            + "M -0.62 -0.62 L 0.62 -0.62 L 0.62 0.62 L -0.62 0.62 Z "
            //四条边中的应力线向内收
            + "M 0 -0.62 L 0 -0.44 M 0 0.62 L 0 0.44 "
            + "M -0.62 0 L -0.44 0 M 0.62 0 L 0.44 0 "
            //中心格向内的双箭头
            + "M -0.16 -0.28 L 0 -0.12 L 0.16 -0.28 "
            + "M -0.16 0.28 L 0 0.12 L 0.16 0.28";
    }
}
