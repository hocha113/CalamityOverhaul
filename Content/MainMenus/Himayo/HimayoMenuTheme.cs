using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>夜樱主菜单调色板与布局常量；菜单绘制路径内 screenWidth/mouse 已是 UI 空间，直接取用</summary>
    internal static class HimayoMenuTheme
    {
        //樱瓣粉，与鬼切领域樱瓣同源
        public static readonly Color PetalPink = new(255, 205, 216);
        public static readonly Color PetalPinkDeep = new(250, 178, 194);
        //远景紫雾，向夜空底色靠拢
        public static readonly Color HazePurple = new(196, 158, 214);
        //稀有暗红瓣，呼应背景灯笼
        public static readonly Color PetalCrimson = new(158, 32, 46);
        //正文象牙白与暗态
        public static readonly Color TextIvory = new(240, 232, 238);
        public static readonly Color TextDim = new(172, 152, 176);
        //悬停高亮与下划线辉光
        public static readonly Color AccentBloom = new(255, 176, 196);

        //左侧按钮列锚点与间距（UI 空间像素，避开画面正中的立绘）
        public const float ButtonAnchorX = 92f;
        public const float ButtonSpacing = 54f;
        public const float ButtonTextScale = 0.62f;
        public const float ButtonHoverScaleBonus = 0.07f;
        public const float ButtonHoverSlide = 14f;

        //标题簇
        public const float TitleX = 92f;
        public const float TitleY = 84f;
    }
}
