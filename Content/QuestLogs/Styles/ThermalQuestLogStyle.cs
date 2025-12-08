using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs.Styles
{
    public class ThermalQuestLogStyle : IQuestLogStyle
    {
        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            //绘制半透明黑色背景
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, panelRect, Color.Black * 0.8f);

            //绘制边框
            int border = 2;
            //上
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, border), Color.OrangeRed);
            //下
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - border, panelRect.Width, border), Color.OrangeRed);
            //左
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, border, panelRect.Height), Color.OrangeRed);
            //右
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - border, panelRect.Y, border, panelRect.Height), Color.OrangeRed);

            //绘制扫描线效果(简单的模拟)
            float scanY = (float)(Main.timeForVisualEffects / 60.0 % 1.0) * panelRect.Height;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y + (int)scanY, panelRect.Width, 2), Color.Orange * 0.3f);
        }

        public void DrawNode(SpriteBatch spriteBatch, QuestNode node, Vector2 drawPos, float scale, bool isHovered) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int size = (int)(40 * scale);
            Rectangle nodeRect = new Rectangle((int)drawPos.X - size / 2, (int)drawPos.Y - size / 2, size, size);

            Color color = node.IsCompleted ? Color.Green : (node.IsUnlocked ? Color.Orange : Color.Gray);
            if (isHovered) {
                color = Color.White;
            }

            //绘制节点背景
            spriteBatch.Draw(pixel, nodeRect, color * 0.8f);
            
            //绘制边框
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, node.Name, drawPos.X, drawPos.Y + size / 2 + 4, Color.White, Color.Black, Vector2.Zero, 0.8f);
        }

        public void DrawConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end, bool isUnlocked) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 diff = end - start;
            float rotation = diff.ToRotation();
            float length = diff.Length();
            Color color = isUnlocked ? Color.Orange : Color.Gray;

            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, (int)length, 2), color, rotation, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        public void DrawLauncher(SpriteBatch spriteBatch, Vector2 position, bool isHovered) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle((int)position.X, (int)position.Y, 32, 32);
            Color color = isHovered ? Color.OrangeRed : Color.DarkRed;
            spriteBatch.Draw(pixel, rect, color);
            
            //绘制简单的图标或文字
            Utils.DrawBorderString(spriteBatch, "Quest", position + new Vector2(4, 8), Color.White, 0.7f);
        }

        public Vector4 GetPadding() {
            return new Vector4(10, 30, 10, 10); // Left, Top, Right, Bottom
        }
    }
}
