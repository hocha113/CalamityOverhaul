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
    }
}
