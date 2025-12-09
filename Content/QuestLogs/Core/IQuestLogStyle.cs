using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public interface IQuestLogStyle
    {
        void UpdateStyle();
        //绘制主背景
        void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        //绘制节点
        void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered, float alpha);
        //绘制连接线
        void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked, float alpha);
        //获取面板内边距
        Vector4 GetPadding();
        //绘制任务详情面板
        void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha);
        //获取关闭按钮区域
        Rectangle GetCloseButtonRect(Rectangle panelRect);
        //获取领取奖励按钮区域
        Rectangle GetRewardButtonRect(Rectangle panelRect);
    }
}
