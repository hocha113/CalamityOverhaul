using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    /// <summary>委托管理器界面样式契约</summary>
    internal interface IEntrustManagerStyle
    {
        #region 生命周期

        /// <summary>每帧更新动画计时器和粒子</summary>
        void Update(Rectangle panelRect, float openProgress);

        /// <summary>重置样式状态</summary>
        void Reset();

        #endregion

        #region 面板级绘制

        /// <summary>绘制面板背景</summary>
        void DrawPanelBackground(SpriteBatch sb, Rectangle panelRect, float alpha);

        /// <summary>绘制面板边框</summary>
        void DrawPanelFrame(SpriteBatch sb, Rectangle panelRect, float alpha);

        /// <summary>绘制顶部标题栏</summary>
        void DrawHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha);

        /// <summary>绘制分类选项卡栏</summary>
        void DrawCategoryTabs(SpriteBatch sb, Rectangle tabRect, string[] categories,
            int selectedIndex, float alpha);

        /// <summary>绘制滚动条</summary>
        void DrawScrollbar(SpriteBatch sb, Rectangle trackRect, float scrollRatio,
            float viewRatio, float alpha);

        /// <summary>绘制底部状态栏</summary>
        void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha);

        #endregion

        #region 任务条目绘制

        /// <summary>绘制单个任务条目</summary>
        void DrawQuestEntry(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, int entryIndex);

        /// <summary>绘制条目分隔线</summary>
        void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha);

        #endregion

        #region 颜色与度量

        /// <summary>获取面板背景阴影颜色</summary>
        Color GetShadowColor(float alpha);

        /// <summary>获取标题文字颜色</summary>
        Color GetHeaderTextColor(float alpha);

        /// <summary>根据任务状态获取对应的状态颜色</summary>
        Color GetStatusColor(QuestEntryStatus status, float alpha);

        /// <summary>获取单个任务条目的高度</summary>
        int GetEntryHeight();

        /// <summary>获取条目内边距</summary>
        int GetEntryPadding();

        #endregion

        #region 特效

        /// <summary>绘制背景粒子特效层</summary>
        void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha);

        /// <summary>绘制前景特效覆盖层</summary>
        void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha);

        #endregion

        #region 样式切换按钮

        /// <summary>获取样式切换按钮的矩形区域</summary>
        Rectangle GetStyleSwitchButtonRect(Rectangle panelRect);

        /// <summary>绘制样式切换按钮</summary>
        void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha);

        #endregion
    }
}
