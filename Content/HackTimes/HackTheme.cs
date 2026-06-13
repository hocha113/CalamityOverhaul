namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间主题配色</summary>
    internal static class HackTheme
    {
        //深色背景层级
        public static readonly Color BgDarkest = new(6, 8, 12);
        public static readonly Color BgPanel = new(10, 14, 20);
        public static readonly Color BgSection = new(14, 18, 26);
        public static readonly Color BgSlot = new(16, 22, 30);
        public static readonly Color BgSlotHover = new(20, 30, 40);

        //边框
        public static readonly Color Border = new(35, 50, 60);
        public static readonly Color BorderBright = new(50, 70, 80);
        public static readonly Color InnerShadow = new(3, 5, 8);

        //主强调色
        public static readonly Color Accent = new(0, 200, 210);
        //副强调色
        public static readonly Color AccentAlt = new(40, 180, 160);
        //警告色
        public static readonly Color Danger = new(220, 45, 45);
        //上传中色
        public static readonly Color Uploading = new(200, 170, 40);
        //蔓延色
        public static readonly Color Contagion = new(160, 40, 200);

        //文字层级
        public static readonly Color TextDim = new(70, 85, 95);
        public static readonly Color TextNormal = new(140, 160, 170);
        public static readonly Color TextBright = new(210, 225, 230);

        //网格和装饰
        public static readonly Color GridLine = new(18, 28, 35);
        public static readonly Color EdgeGlow = new(30, 200, 210);

        //进度条
        public static readonly Color ProgressBg = new(12, 16, 22);
        public static readonly Color ProgressFill = new(0, 190, 200);
        public static readonly Color ProgressGlow = new(40, 220, 230);

        #region 类别辅助

        public static Color CategoryColor(QuickHackCategory cat) => cat switch {
            QuickHackCategory.Lethal => Danger,
            QuickHackCategory.Control => Uploading,
            QuickHackCategory.Covert => AccentAlt,
            QuickHackCategory.Contagion => Contagion,
            QuickHackCategory.TileManip => new Color(80, 200, 255),
            QuickHackCategory.Paranormal => new Color(180, 60, 220),
            _ => Accent,
        };

        public static string CategorySymbol(QuickHackCategory cat) => cat switch {
            QuickHackCategory.Lethal => "◆",
            QuickHackCategory.Control => "◇",
            QuickHackCategory.Covert => "○",
            QuickHackCategory.Contagion => "◎",
            QuickHackCategory.TileManip => "▣",
            QuickHackCategory.Paranormal => "☠",
            _ => "●",
        };

        public static string CategoryLabel(QuickHackCategory cat) => cat switch {
            QuickHackCategory.Lethal => HackTime.CatLethal.Value,
            QuickHackCategory.Control => HackTime.CatControl.Value,
            QuickHackCategory.Covert => HackTime.CatCovert.Value,
            QuickHackCategory.Contagion => HackTime.CatContagion.Value,
            QuickHackCategory.TileManip => HackTime.CatTileManip.Value,
            QuickHackCategory.Paranormal => HackTime.CatParanormal.Value,
            _ => HackTime.CatUnknown.Value,
        };

        #endregion
    }
}
