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

        /// <summary>
        /// 行右缘的提供者徽记。纹样与主色来自 <see cref="EntrustEntryData.Provider"/>，
        /// 框的画法归界面样式（Chronicle=邮戳，旧样式=素框）
        /// </summary>
        void DrawProviderBadge(SpriteBatch sb, Vector2 center, float radius,
            EntrustEntryData entry, float alpha);

        /// <summary>展开区提供者落款的占用高度；无提供者返回 0，测量与绘制必须同口径</summary>
        int GetProviderSignatureHeight(EntrustEntryData entry);

        /// <summary>展开区尾部的提供者落款（头像 + 名字）</summary>
        void DrawProviderSignature(SpriteBatch sb, EntrustEntryData entry,
            float x, float y, float width, float alpha);

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
