using Microsoft.Xna.Framework;
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
        //绘制启动按钮
        void DrawLauncher(SpriteBatch spriteBatch, Vector2 position, bool isHovered);
        //获取面板内边距
        Vector4 GetPadding();
    }
}
