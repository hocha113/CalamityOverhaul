namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.Common
{
    /// <summary>机械Boss通用视觉模式(Destroyer/Prime/Twins 共用)</summary>
    internal enum MechBossVisualMode
    {
        /// <summary>常态：红橙描边+暗部红化，夜间可读</summary>
        Idle = 0,
        /// <summary>警告：蓄力/锁定/转阶段，红黄脉冲描边</summary>
        Warning = 1,
        /// <summary>冲刺：白热橙边+横向能量条纹</summary>
        Dashing = 2,
    }
}
