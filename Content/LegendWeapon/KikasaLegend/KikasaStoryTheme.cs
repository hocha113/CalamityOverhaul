namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 鬼雨叙事色板。与 KikasaSky.fx 鬼雨异化态（RAIN_* 常量）同源：
    /// 湿墨冷青为骨，溺月惨白是唯一亮色，禁红禁暖。
    /// 面板 shader（KikasaNarrativePanel.fx）与 CPU 笔触共用这组颜色，
    /// 皮肤侧见 Content/Narrative/Presentation/Skins/Kikasa
    /// </summary>
    internal static class KikasaStoryTheme
    {
        //基底由深到浅——带青壳的近黑沉云，不是纯黑
        public static readonly Color Void = new(7, 9, 11);
        public static readonly Color Deep = new(14, 18, 21);
        public static readonly Color Mid = new(28, 35, 38);
        //雨青主色：雨幡、波光、框线（同 RAIN_SHAFT 一族提亮）
        public static readonly Color Rain = new(96, 120, 126);
        //溺月惨白：高光、名字辉环（同 RAIN_SUN_RIM 一族提亮）
        public static readonly Color Moon = new(196, 214, 218);
        //湿墨水光：打字机尾字、hover 文字的浸水冷青——near-white 在白字上不可见，必须偏色
        public static readonly Color WetInk = new(136, 202, 216);
        //文字主次色，冷白与灰青（次级兼提示行）
        public static readonly Color Text = new(226, 234, 236);
        public static readonly Color TextDim = new(150, 178, 186);
        //面板 CPU 回退底色
        public static readonly Color PanelBg = new(10, 13, 15);
    }
}
