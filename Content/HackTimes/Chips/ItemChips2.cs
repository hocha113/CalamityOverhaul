using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //掉落物族第二批，沿用族语汇：方框 + 抓取角，读作"框住某件东西"

    /// <summary>拆解芯片。方框沿对角裂成两半，四个抓取角向外张开，裂缝里散出三枚小方块</summary>
    internal class ItemSalvageChip : BaseHackProtocolChip<ItemSalvage>
    {
        protected override string DiePath =>
            "M -0.46 0.18 L -0.46 -0.46 L 0.18 -0.46 Z "
            + "M 0.46 -0.18 L 0.46 0.46 L -0.18 0.46 Z "
            + "M -0.70 -0.52 L -0.70 -0.70 L -0.52 -0.70 "
            + "M 0.52 -0.70 L 0.70 -0.70 L 0.70 -0.52 "
            + "M -0.70 0.52 L -0.70 0.70 L -0.52 0.70 "
            + "M 0.52 0.70 L 0.70 0.70 L 0.70 0.52 "
            + "M 0.26 -0.31 L 0.31 -0.26 L 0.26 -0.21 L 0.21 -0.26 Z "
            + "M 0.42 -0.48 L 0.48 -0.42 L 0.42 -0.36 L 0.36 -0.42 Z "
            + "M 0.58 -0.63 L 0.63 -0.58 L 0.58 -0.53 L 0.53 -0.58 Z";
    }

    /// <summary>数据烙印芯片。两个方框并列，左框内一枚星记，一道短箭头把星记推向右框</summary>
    internal class DataBrandChip : BaseHackProtocolChip<DataBrand>
    {
        protected override string DiePath =>
            "M -0.72 -0.26 L -0.20 -0.26 L -0.20 0.26 L -0.72 0.26 Z "
            + "M 0.20 -0.26 L 0.72 -0.26 L 0.72 0.26 L 0.20 0.26 Z "
            + "M -0.46 -0.13 L -0.46 0.13 M -0.59 0 L -0.33 0 "
            + "M -0.55 -0.09 L -0.37 0.09 M -0.55 0.09 L -0.37 -0.09 "
            + "M -0.12 0 L 0.12 0 M 0.12 0 L 0.04 -0.07 M 0.12 0 L 0.04 0.07 "
            + "M -0.72 -0.44 L -0.72 -0.58 L -0.58 -0.58 "
            + "M 0.72 0.44 L 0.72 0.58 L 0.58 0.58";
    }

    /// <summary>身份伪装芯片。方框内一具人形剪影，四个抓取角改成向内收拢的雷达括弧</summary>
    internal class EntityMasqueradeChip : BaseHackProtocolChip<EntityMasquerade>
    {
        protected override string DiePath =>
            "M -0.46 -0.46 L 0.46 -0.46 L 0.46 0.46 L -0.46 0.46 Z "
            + "M -0.07 -0.32 L 0.07 -0.32 L 0.07 -0.18 L -0.07 -0.18 Z "
            + "M 0 -0.18 L 0 0.10 "
            + "M -0.20 -0.04 L 0.20 -0.04 "
            + "M 0 0.10 L -0.15 0.34 M 0 0.10 L 0.15 0.34 "
            + "M -0.70 -0.54 L -0.54 -0.54 L -0.54 -0.70 "
            + "M 0.70 -0.54 L 0.54 -0.54 L 0.54 -0.70 "
            + "M -0.70 0.54 L -0.54 0.54 L -0.54 0.70 "
            + "M 0.70 0.54 L 0.54 0.54 L 0.54 0.70";
    }
}
