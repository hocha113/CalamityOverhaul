using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //电路族纹样第二批：语汇仍是母线 + 继电器，横平竖直；
    //坐标一律收在 ±0.80 内（首批 GridBlackout 蹭到 0.86 是旧账，本批不沿用）

    /// <summary>弹药置换芯片。炮管接一条竖直供弹带，弹带三道横档读作弹链，底座一枚继电器骑在母线上</summary>
    internal class MunitionSwapChip : BaseHackProtocolChip<MunitionSwap>
    {
        protected override string DiePath =>
            "M -0.32 -0.28 L 0.12 -0.28 L 0.12 0.04 L -0.32 0.04 Z "
            + "M 0.12 -0.12 L 0.76 -0.12 "
            + "M -0.10 -0.80 L -0.10 -0.28 "
            + "M -0.18 -0.74 L -0.02 -0.74 M -0.18 -0.60 L -0.02 -0.60 M -0.18 -0.46 L -0.02 -0.46 "
            + "M -0.10 0.04 L -0.10 0.26 "
            + "M -0.28 0.26 L 0.08 0.26 L 0.08 0.50 L -0.28 0.50 Z "
            + "M -0.80 0.38 L -0.28 0.38 M 0.08 0.38 L 0.80 0.38";
    }

    /// <summary>炮台联网芯片。四座小炮管沿母线排开，母线中段一枚菱形节点向上引出一道中空的瞄准十字</summary>
    internal class TurretMeshChip : BaseHackProtocolChip<TurretMesh>
    {
        protected override string DiePath =>
            "M -0.80 0.42 L 0.80 0.42 "
            + "M -0.62 0.42 L -0.62 0.24 M -0.62 0.24 L -0.48 0.10 "
            + "M -0.30 0.42 L -0.30 0.24 M -0.30 0.24 L -0.16 0.10 "
            + "M 0.30 0.42 L 0.30 0.24 M 0.30 0.24 L 0.44 0.10 "
            + "M 0.62 0.42 L 0.62 0.24 M 0.62 0.24 L 0.76 0.10 "
            + "M 0 0.30 L 0.10 0.42 L 0 0.54 L -0.10 0.42 Z "
            + "M 0 0.30 L 0 -0.38 "
            + "M -0.24 -0.46 L -0.08 -0.46 M 0.08 -0.46 L 0.24 -0.46 "
            + "M 0 -0.70 L 0 -0.54";
    }

    /// <summary>信标伪造芯片。塔身向外三层同心弧，两侧各一对箭头朝内——招来，不是发散，与电网瘫痪的向外弧刻意相反</summary>
    internal class BeaconForgeChip : BaseHackProtocolChip<BeaconForge>
    {
        protected override string DiePath =>
            "M 0 -0.30 L 0 0.60 M -0.24 0.60 L 0.24 0.60 "
            + "M -0.18 -0.30 L 0.18 -0.30 "
            + "M -0.24 -0.42 Q 0 -0.56 0.24 -0.42 "
            + "M -0.40 -0.48 Q 0 -0.68 0.40 -0.48 "
            + "M -0.56 -0.54 Q 0 -0.80 0.56 -0.54 "
            + "M -0.74 -0.26 L -0.60 -0.18 M -0.74 -0.10 L -0.60 -0.18 "
            + "M 0.74 -0.26 L 0.60 -0.18 M 0.74 -0.10 L 0.60 -0.18 "
            + "M -0.72 0.22 L -0.58 0.30 M -0.72 0.38 L -0.58 0.30 "
            + "M 0.72 0.22 L 0.58 0.30 M 0.72 0.38 L 0.58 0.30";
    }

    /// <summary>提权芯片。塔顶一把锁：锁梁左半合拢右半悬开读作已断，锁体内一道向上的阶梯折线（提权阶梯）</summary>
    internal class PrivilegeEscalateChip : BaseHackProtocolChip<PrivilegeEscalate>
    {
        protected override string DiePath =>
            "M 0 0.02 L 0 0.66 M -0.22 0.66 L 0.22 0.66 "
            + "M -0.14 0.32 L 0.14 0.32 "
            + "M -0.30 -0.50 L 0.30 -0.50 L 0.30 0.02 L -0.30 0.02 Z "
            + "M -0.18 -0.50 Q -0.18 -0.76 0 -0.76 "
            + "M 0.10 -0.72 Q 0.26 -0.68 0.22 -0.54 "
            + "M -0.20 -0.08 L -0.06 -0.08 L -0.06 -0.20 L 0.06 -0.20 L 0.06 -0.32 L 0.18 -0.32 "
            + "M 0.18 -0.32 L 0.18 -0.44 M 0.12 -0.38 L 0.18 -0.44 L 0.24 -0.38";
    }
}
