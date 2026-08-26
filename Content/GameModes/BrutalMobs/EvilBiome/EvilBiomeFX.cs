using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome
{
    /// <summary>
    /// 邪地小怪机制的共用风味表:四种邪液流派的配色/原版减益/粉尘统一从这里取,
    /// 保证云、溅射、汲取三个家族在同一流派下视觉与减益一致
    /// </summary>
    internal static class EvilBiomeFX
    {
        /// <summary>腐化之蚀(紫底绿芯)</summary>
        public const int FlavorCorrupt = 0;
        /// <summary>猩红之血(暗红底亮红芯)</summary>
        public const int FlavorCrimson = 1;
        /// <summary>灵液(暗金底亮黄芯)</summary>
        public const int FlavorIchor = 2;
        /// <summary>诅咒之焰(暗绿底荧绿芯)</summary>
        public const int FlavorCursed = 3;

        /// <summary>暗色外层(以 A&gt;0 绘制,承担轮廓与实体感)</summary>
        public static Color Deep(int flavor) => flavor switch {
            FlavorCrimson => new Color(61, 8, 12),
            FlavorIchor => new Color(92, 70, 14),
            FlavorCursed => new Color(26, 58, 20),
            _ => new Color(54, 36, 82),
        };

        /// <summary>亮色内芯(A=0 加色敷料)</summary>
        public static Color Bright(int flavor) => flavor switch {
            FlavorCrimson => new Color(228, 52, 44),
            FlavorIchor => new Color(248, 214, 88),
            FlavorCursed => new Color(148, 246, 74),
            _ => new Color(152, 224, 108),
        };

        /// <summary>命中玩家时挂的原版减益(禁止新建 ModBuff)</summary>
        public static int BuffFor(int flavor) => flavor switch {
            FlavorCrimson => BuffID.Bleeding,
            FlavorIchor => BuffID.Ichor,
            FlavorCursed => BuffID.CursedInferno,
            _ => BuffID.Weak,
        };

        /// <summary>风味粉尘(统一用火把系,无重力发光,行为一致)</summary>
        public static int DustFor(int flavor) => flavor switch {
            FlavorCrimson => DustID.CrimsonTorch,
            FlavorIchor => DustID.IchorTorch,
            FlavorCursed => DustID.CursedTorch,
            _ => DustID.CorruptTorch,
        };
    }
}
