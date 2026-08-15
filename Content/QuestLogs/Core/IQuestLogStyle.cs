using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>任务日志界面样式契约</summary>
    public interface IQuestLogStyle
    {
        void UpdateStyle();
        void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha);
        void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha);
        Vector4 GetPadding();
        void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha);
        Rectangle GetCloseButtonRect(Rectangle panelRect);
        Rectangle GetRewardButtonRect(Rectangle panelRect);
        void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        Rectangle GetClaimAllButtonRect(Rectangle panelRect);
        void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha);
        Rectangle GetResetViewButtonRect(Rectangle panelRect);
        void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha);
        Rectangle GetStyleSwitchButtonRect(Rectangle panelRect);
        void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha);
        Rectangle GetNightModeButtonRect(Rectangle panelRect);
        void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode);

        #region 全屏扩展，默认实现让既有样式零改动沿用旧行为

        /// <summary>样式名，用于切换提示</summary>
        string DisplayName => GetType().Name;

        /// <summary>是否提供日夜双模式，为否时容器不画夜间模式按钮</summary>
        bool SupportsNightMode => true;

        /// <summary>
        /// 是否自绘全屏外框（页眉、左栏、页脚、合卷键与详情栏的收起键）。<br/>
        /// 旧样式只会拿到一张铺满屏幕的背景矩形，其余交给容器的通用绘制
        /// </summary>
        bool DrawsOwnChrome => false;

        /// <summary>全屏外框，仅 <see cref="DrawsOwnChrome"/> 为真时调用</summary>
        void DrawChrome(SpriteBatch spriteBatch, QuestLog log, in QuestLogLayout layout) { }

        /// <summary>
        /// 容器每帧绘制前交付分区快照。<br/>
        /// 供那些签名里拿不到分区的旧接口成员（如 <see cref="DrawProgressBar"/>）取用
        /// </summary>
        void SyncLayout(in QuestLogLayout layout) { }

        /// <summary>
        /// 右侧停靠详情栏。默认转发到旧的 <see cref="DrawQuestDetail"/>，
        /// 新样式重写它以接管滚动排版
        /// </summary>
        void DrawDetail(SpriteBatch spriteBatch, QuestNode node, in QuestLogLayout layout, float alpha, float scroll)
            => DrawQuestDetail(spriteBatch, node, layout.Detail, alpha);

        /// <summary>详情正文总高，供容器夹紧滚动量；返回 0 视为不可滚动</summary>
        float MeasureDetailHeight(QuestNode node, in QuestLogLayout layout) => 0f;

        #endregion
    }
}
