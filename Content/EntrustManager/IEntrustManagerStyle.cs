using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>管理器界面样式契约</summary>
    internal interface IEntrustManagerStyle
    {
        #region 生命周期

        void Update(Rectangle panelRect, float openProgress);

        void Reset();

        #endregion

        #region 面板级绘制

        void DrawPanelBackground(SpriteBatch sb, Rectangle panelRect, float alpha);

        void DrawPanelFrame(SpriteBatch sb, Rectangle panelRect, float alpha);

        void DrawHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha);

        void DrawCategoryTabs(SpriteBatch sb, Rectangle tabRect, string[] categories,
            int selectedIndex, float alpha);

        void DrawScrollbar(SpriteBatch sb, Rectangle trackRect, float scrollRatio,
            float viewRatio, float alpha);

        void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha);

        /// <summary>一条委托都没有时的空态提示</summary>
        void DrawEmptyHint(SpriteBatch sb, Rectangle contentRect, string text, float alpha);

        /// <summary>悬停条目时页脚上方的操作提示（展开/关注/挂起）</summary>
        void DrawInteractionHints(SpriteBatch sb, Rectangle footerRect, EntrustEntryData entry, float alpha);

        #endregion

        #region 任务条目绘制

        void DrawQuestEntry(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, int entryIndex);

        void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha);

        #endregion

        #region 颜色与度量

        Color GetShadowColor(float alpha);

        Color GetHeaderTextColor(float alpha);

        Color GetStatusColor(QuestEntryStatus status, float alpha);

        int GetEntryHeight();

        int GetEntryPadding();

        #endregion

        #region 特效

        void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha);

        void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha);

        #endregion

        #region 样式切换按钮

        Rectangle GetStyleSwitchButtonRect(Rectangle panelRect);

        void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha);

        #endregion
    }
}
