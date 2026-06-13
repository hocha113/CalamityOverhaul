using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>任务日志界面样式契约</summary>
    public interface IQuestLogStyle
    {
        /// <summary>更新样式</summary>
        void UpdateStyle();
        /// <summary>绘制主背景</summary>
        void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        /// <summary>绘制节点</summary>
        void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha);
        /// <summary>绘制连接线</summary>
        void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha);
        /// <summary>获取面板内边距</summary>
        Vector4 GetPadding();
        /// <summary>绘制任务详情面板</summary>
        void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha);
        /// <summary>获取关闭按钮区域</summary>
        Rectangle GetCloseButtonRect(Rectangle panelRect);
        /// <summary>获取领取奖励按钮区域</summary>
        Rectangle GetRewardButtonRect(Rectangle panelRect);
        /// <summary>绘制进度条</summary>
        void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        /// <summary>获取一键领取按钮区域</summary>
        Rectangle GetClaimAllButtonRect(Rectangle panelRect);
        /// <summary>绘制一键领取按钮</summary>
        void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha);
        /// <summary>获取重置视图按钮区域</summary>
        Rectangle GetResetViewButtonRect(Rectangle panelRect);
        /// <summary>绘制重置视图按钮</summary>
        void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha);
        /// <summary>获取样式切换按钮区域</summary>
        Rectangle GetStyleSwitchButtonRect(Rectangle panelRect);
        /// <summary>绘制样式切换按钮</summary>
        void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha);
        /// <summary>获取夜间模式按钮区域</summary>
        Rectangle GetNightModeButtonRect(Rectangle panelRect);
        /// <summary>绘制夜间模式按钮</summary>
        void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode);
    }
}
