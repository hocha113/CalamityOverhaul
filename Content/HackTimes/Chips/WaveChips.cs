using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //2026-08 扩展批（Water 2 + Projectile 3）。
    //液体族纹样语汇：波纹 + 液面，纹样一律横向铺开；
    //弹幕族纹样语汇：箭簇 + 轨迹线，一眼看出是冲着"飞行中的东西"去的

    /// <summary>增压水域芯片。波纹被压成扁平，三道箭簇从液面向上顶出</summary>
    internal class PressureSurgeChip : BaseHackProtocolChip<PressureSurge>
    {
        protected override string DiePath =>
            "M -0.78 0.60 Q -0.50 0.50 -0.22 0.60 Q 0.06 0.70 0.34 0.60 Q 0.62 0.50 0.78 0.60 "
            + "M -0.44 0.36 L -0.44 -0.24 M -0.44 -0.24 L -0.58 -0.04 M -0.44 -0.24 L -0.30 -0.04 "
            + "M 0 0.36 L 0 -0.50 M 0 -0.50 L -0.15 -0.28 M 0 -0.50 L 0.15 -0.28 "
            + "M 0.44 0.36 L 0.44 -0.24 M 0.44 -0.24 L 0.30 -0.04 M 0.44 -0.24 L 0.58 -0.04";
    }

    /// <summary>镜像水面芯片。笔直的液面线，上下两枚箭簇互为倒影，水下那枚虚线</summary>
    internal class MirrorSurfaceChip : BaseHackProtocolChip<MirrorSurface>
    {
        protected override string DiePath =>
            "M -0.78 0 L 0.78 0 "
            + "M -0.52 -0.60 L 0.08 -0.12 M 0.08 -0.12 L -0.18 -0.16 M 0.08 -0.12 L 0 -0.38 "
            + "M -0.52 0.60 L -0.36 0.47 M -0.28 0.41 L -0.12 0.28 "
            + "M 0.08 0.12 L -0.18 0.16 M 0.08 0.12 L 0 0.38";
    }

    /// <summary>延迟引信芯片。轨迹断成虚线后停住，箭簇尾部盘一圈引信螺线</summary>
    internal class DelayFuseChip : BaseHackProtocolChip<DelayFuse>
    {
        protected override string DiePath =>
            "M -0.78 0.06 L -0.52 0.06 M -0.40 0.06 L -0.18 0.06 "
            + "M -0.18 0.06 L 0.30 0.06 M 0.30 0.06 L 0.08 -0.12 M 0.30 0.06 L 0.08 0.24 "
            + "M -0.14 0.30 Q -0.44 0.30 -0.44 0.50 Q -0.44 0.70 -0.22 0.70 "
            + "Q -0.04 0.70 -0.04 0.54 Q -0.04 0.42 -0.18 0.42";
    }

    /// <summary>弹幕征收芯片。一枚粗箭簇（双线杆身），三道细轨迹由外向内汇进尾部</summary>
    internal class ProjectileTitheChip : BaseHackProtocolChip<ProjectileTithe>
    {
        protected override string DiePath =>
            "M -0.06 -0.04 L 0.54 -0.04 M -0.06 0.08 L 0.54 0.08 "
            + "M 0.54 -0.04 L 0.70 0.02 M 0.54 0.08 L 0.70 0.02 "
            + "M 0.70 0.02 L 0.40 -0.24 M 0.70 0.02 L 0.40 0.28 "
            + "M -0.72 -0.42 L -0.14 -0.02 M -0.78 0.02 L -0.16 0.02 M -0.72 0.46 L -0.14 0.06";
    }

    /// <summary>弹幕采样芯片。箭簇被两个对角取样括号夹住，右侧一枚虚线复制品</summary>
    internal class ProjectileSampleChip : BaseHackProtocolChip<ProjectileSample>
    {
        protected override string DiePath =>
            "M -0.64 -0.26 L -0.64 -0.44 L -0.46 -0.44 M -0.04 0.26 L -0.04 0.44 L -0.22 0.44 "
            + "M -0.56 0 L -0.16 0 M -0.16 0 L -0.32 -0.14 M -0.16 0 L -0.32 0.14 "
            + "M 0.18 0 L 0.32 0 M 0.42 0 L 0.56 0 "
            + "M 0.68 0 L 0.54 -0.12 M 0.68 0 L 0.54 0.12";
    }
}
