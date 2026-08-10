using CalamityOverhaul.Content.HackTimes.Protocols;

namespace CalamityOverhaul.Content.HackTimes.Chips
{
    //弹幕族纹样语汇：箭簇 + 轨迹线，一眼看出是冲着"飞行中的东西"去的

    /// <summary>弹道接管芯片。箭簇掉头，轨迹在中途折回</summary>
    internal class ProjectileHijackChip : BaseHackProtocolChip<ProjectileHijack>
    {
        protected override string DiePath =>
            "M -0.78 0.30 L -0.10 0.30 Q 0.42 0.30 0.42 -0.18 L 0.42 -0.52 "
            + "M 0.42 -0.52 L 0.18 -0.24 M 0.42 -0.52 L 0.66 -0.24 "
            + "M -0.78 0.30 L -0.54 0.06 M -0.78 0.30 L -0.54 0.54";
    }

    /// <summary>弹道冻结芯片。箭簇撞上一道竖闸，两侧霜齿</summary>
    internal class ProjectileFreezeChip : BaseHackProtocolChip<ProjectileFreeze>
    {
        protected override string DiePath =>
            "M -0.82 0 L 0.10 0 M 0.10 0 L -0.14 -0.22 M 0.10 0 L -0.14 0.22 "
            + "M 0.40 -0.72 L 0.40 0.72 "
            + "M 0.40 -0.44 L 0.72 -0.60 M 0.40 0 L 0.76 0 M 0.40 0.44 L 0.72 0.60";
    }

    /// <summary>
    /// 数据清除芯片。轨迹线走到一半被叉断，断口散着碎点。<br/>
    /// 碎点写成孤立的 M（单点子路径＝点凿）；写成零长的 M..L 会被按线段丢掉
    /// </summary>
    internal class DataPurgeChip : BaseHackProtocolChip<DataPurge>
    {
        protected override string DiePath =>
            "M -0.84 -0.34 L -0.16 -0.34 M -0.84 0 L -0.30 0 M -0.84 0.34 L -0.16 0.34 "
            + "M 0.16 -0.52 L 0.62 0.52 M 0.62 -0.52 L 0.16 0.52 "
            + "M 0.04 -0.16 M 0.02 0.20";
    }

    /// <summary>弹道超频芯片。箭簇后拖三道递增的尾焰</summary>
    internal class BallisticOverclockChip : BaseHackProtocolChip<BallisticOverclock>
    {
        protected override string DiePath =>
            "M -0.20 0 L 0.72 0 M 0.72 0 L 0.44 -0.26 M 0.72 0 L 0.44 0.26 "
            + "M -0.34 -0.38 L -0.74 -0.54 M -0.30 0 L -0.86 0 M -0.34 0.38 L -0.74 0.54";
    }
}
