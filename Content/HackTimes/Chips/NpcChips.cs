using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //生体族纹样语汇：躯体轮廓 + 神经节，纹样都绕着一个"活物"长

    /// <summary>装甲解析芯片。躯体轮廓被一道横扫线切开，甲片向两侧崩开</summary>
    internal class ArmorParseChip : BaseHackProtocolChip<ArmorParse>
    {
        protected override string DiePath =>
            "M -0.34 -0.66 Q -0.52 0 -0.34 0.66 M 0.34 -0.66 Q 0.52 0 0.34 0.66 "
            + "M -0.34 -0.66 L 0.34 -0.66 M -0.34 0.66 L 0.34 0.66 "
            + "M -0.88 -0.06 L 0.88 -0.06 "
            + "M -0.56 -0.34 L -0.80 -0.52 M 0.56 -0.34 L 0.80 -0.52";
    }

    /// <summary>强制注销芯片。名册上的一个记号被划掉，四角散着注销点</summary>
    internal class ExorciseChip : BaseHackProtocolChip<Exorcise>
    {
        protected override string DiePath =>
            "M -0.50 -0.62 L -0.50 0.62 L 0.50 0.62 L 0.50 -0.62 Z "
            + "M -0.30 -0.30 L 0.30 -0.30 M -0.30 0 L 0.30 0 M -0.30 0.30 L 0.10 0.30 "
            + "M -0.72 -0.80 L 0.74 0.82 "
            + "M -0.80 0.62 M 0.80 -0.62";
    }

    /// <summary>数据榨取芯片。神经节被一根导管接走，末端汇成回流口</summary>
    internal class DataLeechChip : BaseHackProtocolChip<DataLeech>
    {
        protected override string DiePath =>
            "M -0.46 -0.30 L -0.10 -0.30 L -0.10 0.30 L -0.46 0.30 Z "
            + "M -0.46 -0.30 L -0.78 -0.58 M -0.46 0.30 L -0.78 0.58 M -0.46 0 L -0.82 0 "
            + "M -0.10 0 Q 0.28 0 0.34 -0.34 Q 0.40 -0.68 0.76 -0.68 "
            + "M 0.76 -0.68 L 0.52 -0.84 M 0.76 -0.68 L 0.52 -0.50";
    }

    /// <summary>蜂群链接芯片。一个主节点向三个从节点拉出总线</summary>
    internal class SwarmLinkChip : BaseHackProtocolChip<SwarmLink>
    {
        protected override string DiePath =>
            "M -0.62 0 L -0.24 -0.30 L -0.24 0.30 Z "
            + "M -0.24 0 L 0.30 0 "
            + "M 0.30 -0.62 L 0.30 0.62 "
            + "M 0.30 -0.62 L 0.74 -0.62 M 0.30 0 L 0.74 0 M 0.30 0.62 L 0.74 0.62 "
            + "M 0.80 -0.62 M 0.80 0 M 0.80 0.62";
    }
}
