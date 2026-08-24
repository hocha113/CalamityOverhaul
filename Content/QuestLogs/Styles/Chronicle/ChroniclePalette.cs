namespace CalamityOverhaul.Content.QuestLogs.Styles.Chronicle
{
    /// <summary>
    /// 「远征纪要」色板：皮革桌板 + 羊皮纸内页 + 褐墨 + 烫金 + 蜡封绯<br/>
    /// 与 QuestChronicleBg.fx 同源，改这里必须同步改 shader 的 uCol* 上载
    /// </summary>
    internal static class ChroniclePalette
    {
        //皮革桌板，暗酒褐
        public static readonly Color Leather = new(46, 27, 23);
        public static readonly Color LeatherDeep = new(24, 13, 11);

        //羊皮纸内页
        public static readonly Color Paper = new(212, 193, 158);
        public static readonly Color PaperDeep = new(146, 122, 88);

        //褐墨三档：正文 / 次级 / 未点亮。次级与未点亮压深过一次，浅墨在纸上读作没墨水
        public static readonly Color Ink = new(46, 33, 26);
        public static readonly Color InkMute = new(76, 59, 43);
        public static readonly Color InkFaint = new(122, 103, 79);

        //烫金：压印线、已通路线、强调
        public static readonly Color Gold = new(186, 146, 76);
        public static readonly Color GoldHi = new(242, 214, 148);
        public static readonly Color GoldDeep = new(112, 82, 38);

        //蜡封绯：完结的印记
        public static readonly Color Seal = new(146, 42, 36);
        public static readonly Color SealHi = new(198, 78, 62);
        public static readonly Color SealDeep = new(84, 20, 18);

        //黄铜活儿：书上的金属配件
        public static readonly Color Brass = new(150, 118, 62);
        public static readonly Color BrassHi = new(214, 184, 118);
        public static readonly Color BrassDeep = new(72, 54, 26);

        //烛光暖白
        public static readonly Color Candle = new(255, 226, 176);

        /// <summary>颜色转 shader 的 float3（0~1）</summary>
        public static Vector3 Vec3(Color color) => color.ToVector3();
    }
}
