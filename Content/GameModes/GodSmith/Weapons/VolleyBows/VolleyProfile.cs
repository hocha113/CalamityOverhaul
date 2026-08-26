namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 齐射编队形状。Line/Wedge/Cross/Echelon/Cone 是出膛位形（生成后直飞），
    /// Rain/Butterfly/Ring 类特殊编队由各武器方案自行编排（队列天降/相位编舞）
    /// </summary>
    internal enum GsVolleyFormation
    {
        /// <summary>横列：垂直弹道等距排开</summary>
        Line,
        /// <summary>楔形：中锋突前，两翼后错</summary>
        Wedge,
        /// <summary>十字：中锋 + 上下前后四向</summary>
        Cross,
        /// <summary>雁行：斜线错列</summary>
        Echelon,
        /// <summary>锥形：按总扇角均分角度（SpreadPx 解释为总扇角度数）</summary>
        Cone,
    }

    /// <summary>
    /// 打标弹幕的角色编码（写进 router.MarkData 随生成包过线）。
    /// 0~9 为家族保留段，10 起各武器方案自定义
    /// </summary>
    internal static class GsVolleyRole
    {
        /// <summary>普通射击的原版弹幕（打标但无角色）</summary>
        public const int None = 0;
        /// <summary>齐射主箭（全伤）</summary>
        public const int VolleyMain = 1;
        /// <summary>齐射副箭（SideArrowMul 折伤）</summary>
        public const int VolleySide = 2;
        /// <summary>连弩三连点射的补射箭</summary>
        public const int PointBlast = 3;
        /// <summary>武器自定义角色起始值</summary>
        public const int CustomBase = 10;
    }
}
