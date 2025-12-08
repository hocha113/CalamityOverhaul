using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public interface IQuestLogStyle
    {
        //绘制主背景
        void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect);
        //绘制节点
        void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered);
        //绘制连接线
        void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked);
        //获取面板内边距
        Vector4 GetPadding();
        //绘制任务详情面板
        void DrawQuestDetail(SpriteBatch spriteBatch, QuestNode node, Rectangle panelRect, float alpha);
    }
}
