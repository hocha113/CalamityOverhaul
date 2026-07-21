using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>追踪窗口样式契约</summary>
    internal interface IEntrustTrackerWidgetStyle
    {
        #region 生命周期

        void Update(Rectangle widgetRect, float slideProgress);

        void Reset();

        #endregion

        #region 面板绘制

        void DrawWidgetBackground(SpriteBatch sb, Rectangle rect, float alpha);

        void DrawWidgetFrame(SpriteBatch sb, Rectangle rect, float alpha);

        void DrawWidgetHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha);

        void DrawWidgetProgress(SpriteBatch sb, Rectangle barRect, float progress,
            string progressText, float alpha);

        void DrawWidgetDivider(SpriteBatch sb, Vector2 start, Vector2 end, float alpha);

        void DrawWidgetOverlay(SpriteBatch sb, Rectangle rect, float alpha);

        #endregion

        #region 颜色

        Color GetWidgetTitleColor(float alpha);

        Color GetWidgetTextColor(float alpha);

        Color GetWidgetAccentColor(float alpha);

        #endregion

        #region 度量

        /// <summary>null 默认 220px</summary>
        int? GetPreferredWidth();

        /// <summary>null 默认 90px</summary>
        int? GetMinHeight();

        /// <summary>null 不启用紧凑</summary>
        int? GetIdleCompactHeight(EntrustEntryData entry) => null;

        /// <summary>紧凑可见度 0~1</summary>
        float GetCompactVisibility(EntrustEntryData entry) => 1f;

        #endregion
    }
}
