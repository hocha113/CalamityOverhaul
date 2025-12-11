using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public interface IQuestLogStyle
    {
        /// <summary>
        /// 更新样式
        /// </summary>
        void UpdateStyle();
        /// <summary>
        /// 绘制主背景
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="log"></param>
        /// <param name="panelRect"></param>
        void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        /// <summary>
        /// 绘制节点
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="node"></param>
        /// <param name="drawPos"></param>
        /// <param name="scale"></param>
        /// <param name="isHovered"></param>
        /// <param name="alpha"></param>
        void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha);
        /// <summary>
        /// 绘制连接线
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="isUnlocked"></param>
        /// <param name="alpha"></param>
        void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha);
        /// <summary>
        /// 获取面板内边距
        /// </summary>
        /// <returns></returns>
        Vector4 GetPadding();
        /// <summary>
        /// 绘制任务详情面板
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="node"></param>
        /// <param name="panelRect"></param>
        /// <param name="alpha"></param>
        void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha);
        /// <summary>
        /// 获取关闭按钮区域
        /// </summary>
        /// <param name="panelRect"></param>
        /// <returns></returns>
        Rectangle GetCloseButtonRect(Rectangle panelRect);
        /// <summary>
        /// 获取领取奖励按钮区域
        /// </summary>
        /// <param name="panelRect"></param>
        /// <returns></returns>
        Rectangle GetRewardButtonRect(Rectangle panelRect);
        /// <summary>
        /// 绘制进度条
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="log"></param>
        /// <param name="panelRect"></param>
        void DrawProgressBar(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        /// <summary>
        /// 获取一键领取按钮区域
        /// </summary>
        /// <param name="panelRect"></param>
        /// <returns></returns>
        Rectangle GetClaimAllButtonRect(Rectangle panelRect);
        /// <summary>
        /// 绘制一键领取按钮
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="panelRect"></param>
        /// <param name="isHovered"></param>
        /// <param name="alpha"></param>
        void DrawClaimAllButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha);
        /// <summary>
        /// 获取重置视图按钮区域
        /// </summary>
        /// <param name="panelRect"></param>
        /// <returns></returns>
        Rectangle GetResetViewButtonRect(Rectangle panelRect);
        /// <summary>
        /// 绘制重置视图按钮
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="panelRect"></param>
        /// <param name="directionToCenter"></param>
        /// <param name="isHovered"></param>
        /// <param name="alpha"></param>
        void DrawResetViewButton(SpriteBatch spriteBatch, Rectangle panelRect, Vector2 directionToCenter, bool isHovered, float alpha);
        /// <summary>
        /// 获取样式切换按钮区域
        /// </summary>
        /// <param name="panelRect"></param>
        /// <returns></returns>
        Rectangle GetStyleSwitchButtonRect(Rectangle panelRect);
        /// <summary>
        /// 绘制样式切换按钮
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="panelRect"></param>
        /// <param name="isHovered"></param>
        /// <param name="alpha"></param>
        void DrawStyleSwitchButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha);
        /// <summary>
        /// 获取夜间模式按钮区域
        /// </summary>
        /// <param name="panelRect"></param>
        /// <returns></returns>
        Rectangle GetNightModeButtonRect(Rectangle panelRect);
        /// <summary>
        /// 绘制夜间模式按钮
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="panelRect"></param>
        /// <param name="isHovered"></param>
        /// <param name="alpha"></param>
        /// <param name="isNightMode"></param>
        void DrawNightModeButton(SpriteBatch spriteBatch, Rectangle panelRect, bool isHovered, float alpha, bool isNightMode);
    }
}
