namespace CalamityOverhaul.Content.Narrative.Guides
{
    /// <summary>
    /// 传奇武器教学卡的统一字号（比目鱼 / 鬼切 / 鬼伞三家共用，新卡也从这里取）。
    /// 口径承 Kikasa UI 字体规范（UI-CATALOG「字体规范」）：任何文字不低于 0.85；
    /// 三家此前各写一套 0.72~0.82 的小字，用户两次点名偏小后在此并轨
    /// </summary>
    internal static class GuideCardTypography
    {
        /// <summary>卡题</summary>
        public const float Title = 1.0f;

        /// <summary>讲解正文</summary>
        public const float Body = 0.9f;

        /// <summary>操作提示行（正文之后、带键位的那一句）</summary>
        public const float Prompt = 0.95f;

        /// <summary>读数 / 死路 / 键位补充等次要提示</summary>
        public const float Hint = 0.85f;

        /// <summary>卡底按钮文字</summary>
        public const float Button = 0.85f;

        /// <summary>行距附加像素（在字高之上）</summary>
        public const float LineGap = 3f;

        /// <summary>讲解卡默认宽度；字号上调后同步放宽，正文换行数与旧卡相当</summary>
        public const int CardWidth = 380;
    }
}
