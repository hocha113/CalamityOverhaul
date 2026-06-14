namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>SHPC HUD 配色与几何常量</summary>
    internal static class SHPCTheme
    {
        //主色调，弧带与按钮的基础色
        public static readonly Color Cyan = new(86, 220, 240);
        //扫光/悬停描边高光
        public static readonly Color CyanHi = new(170, 245, 255);
        //深色背景槽位
        public static readonly Color SlotBg = new(8, 22, 30);
        //更深的投影色
        public static readonly Color ShadowDark = new(0, 6, 10);
        //柔和的边框色
        public static readonly Color Border = new(40, 110, 130);
        //高亮边框色
        public static readonly Color BorderHi = new(120, 220, 240);
        //选中时的暖色，避免整屏冷色单调
        public static readonly Color Accent = new(255, 170, 60);
        //禁用按钮的灰青色
        public static readonly Color Disabled = new(60, 80, 90);
        //文字主色
        public static readonly Color Text = new(220, 240, 245);
        //文字次色
        public static readonly Color TextDim = new(120, 160, 175);

        //核心几何参数，集中在此便于整体调整
        public const float CoreRadius = 16f;
        //核心外环半径
        public const float CoreRingR = 22f;
        //按钮内弧半径
        public const float ButtonInnerR = 60f;
        //按钮外弧半径
        public const float ButtonOuterR = 96f;
        //按钮中心半径，摆图标
        public const float ButtonMidR = (ButtonInnerR + ButtonOuterR) * 0.5f;
        //信息面板距按钮外缘的偏移
        public const float InfoPanelGap = 18f;

        //扇形整体起止角度，使用屏幕坐标系，-PiOver2为正上方，0为正右方
        //从接近正上方略微偏右开始，到接近正右方结束，整个扇形朝右上方展开
        public const float FanStart = -1.484f;
        public const float FanEnd = -0.087f;
        //单个按钮之间的角度间隔
        public const float ButtonGap = 0.045f;
    }
}
