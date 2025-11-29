using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Sulfseas.OldDukes.OldDukeShops
{
    /// <summary>
    /// 硫磺海风格的滚动条
    /// </summary>
    internal class OldDukeScrollBar
    {
        public bool IsDragging { get; private set; }
        private Rectangle indicatorRect;

        /// <summary>
        /// 更新滚动条状态
        /// </summary>
        public void Update(Vector2 panelPosition, int barHeight, int scrollOffset, int maxScroll,
            int totalItems, int visibleItems, Point mousePosition, bool mouseLeftDown,
            bool mouseLeftRelease, out int newScrollOffset) {
            newScrollOffset = scrollOffset;

            if (maxScroll <= 0) {
                IsDragging = false;
                return;
            }

            //滚动条位置（放在右侧）
            int barX = (int)(panelPosition.X + 520);
            int barY = (int)(panelPosition.Y + 140);

            Rectangle barBg = new Rectangle(barX, barY, 12, barHeight);

            //滑块高度
            float indicatorHeightRatio = (float)visibleItems / totalItems;
            int indicatorHeight = Math.Max(30, (int)(barHeight * indicatorHeightRatio));

            //滑块位置
            float scrollRatio = maxScroll > 0 ? (float)scrollOffset / maxScroll : 0f;
            int indicatorY = barY + (int)((barHeight - indicatorHeight) * scrollRatio);

            indicatorRect = new Rectangle(barX, indicatorY, 12, indicatorHeight);

            //鼠标交互
            if (mouseLeftDown) {
                if (Main.mouseLeftRelease) {
                    //首次点击
                    if (indicatorRect.Contains(mousePosition)) {
                        IsDragging = true;
                    }
                    else if (barBg.Contains(mousePosition)) {
                        //点击滚动条背景，直接跳转
                        float clickRatio = (mousePosition.Y - barY) / (float)barHeight;
                        newScrollOffset = (int)(clickRatio * maxScroll);
                        newScrollOffset = Math.Clamp(newScrollOffset, 0, maxScroll);
                    }
                }
                else if (IsDragging) {
                    //拖动中
                    float dragRatio = (mousePosition.Y - barY - indicatorHeight * 0.5f) / (barHeight - indicatorHeight);
                    newScrollOffset = (int)(dragRatio * maxScroll);
                    newScrollOffset = Math.Clamp(newScrollOffset, 0, maxScroll);
                }
            }
            else {
                //松开鼠标
                IsDragging = false;
            }
        }

        /// <summary>
        /// 绘制滚动条
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, int barHeight,
            int scrollOffset, int maxScroll, int totalItems, int visibleItems,
            float uiAlpha, float sulfurPulseTimer) {
            if (maxScroll <= 0) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;

            //滚动条位置
            int barX = (int)(panelPosition.X + 520);
            int barY = (int)(panelPosition.Y + 140);

            Rectangle barBg = new Rectangle(barX, barY, 12, barHeight);

            //绘制滚动条背景
            DrawScrollBarBackground(spriteBatch, barBg, pixel, uiAlpha, sulfurPulseTimer);

            //滑块高度
            float indicatorHeightRatio = (float)visibleItems / totalItems;
            int indicatorHeight = Math.Max(30, (int)(barHeight * indicatorHeightRatio));

            //滑块位置
            float scrollRatio = maxScroll > 0 ? (float)scrollOffset / maxScroll : 0f;
            int indicatorY = barY + (int)((barHeight - indicatorHeight) * scrollRatio);

            Rectangle indicatorRect = new Rectangle(barX, indicatorY, 12, indicatorHeight);

            //绘制滑块
            DrawScrollIndicator(spriteBatch, indicatorRect, pixel, uiAlpha, sulfurPulseTimer);

            //绘制毒液流动效果
            DrawToxicFlow(spriteBatch, indicatorRect, pixel, uiAlpha);
        }

        private void DrawScrollBarBackground(SpriteBatch spriteBatch, Rectangle barBg,
            Texture2D pixel, float uiAlpha, float sulfurPulseTimer) {
            //背景
            Color bgColor = new Color(20, 30, 12) * (uiAlpha * 0.6f);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), bgColor);

            //边框
            Color borderColor = new Color(70, 100, 35) * (uiAlpha * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), borderColor);
        }

        private void DrawScrollIndicator(SpriteBatch spriteBatch, Rectangle indicatorRect,
            Texture2D pixel, float uiAlpha, float sulfurPulseTimer) {
            //滑块主体渐变
            float pulse = (float)Math.Sin(sulfurPulseTimer) * 0.5f + 0.5f;
            Color indicatorTop = Color.Lerp(new Color(50, 70, 25), new Color(80, 110, 45), pulse) * uiAlpha;
            Color indicatorBottom = Color.Lerp(new Color(35, 50, 18), new Color(60, 85, 35), pulse) * uiAlpha;

            int segments = 8;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                int y = indicatorRect.Y + (int)(t * indicatorRect.Height);
                int height = Math.Max(1, indicatorRect.Height / segments);
                Rectangle segRect = new Rectangle(indicatorRect.X, y, indicatorRect.Width, height);
                Color segColor = Color.Lerp(indicatorTop, indicatorBottom, t);
                spriteBatch.Draw(pixel, segRect, new Rectangle(0, 0, 1, 1), segColor);
            }

            //滑块边框（硫磺绿）
            Color borderColor = new Color(130, 160, 65) * (uiAlpha * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(indicatorRect.X, indicatorRect.Y, indicatorRect.Width, 2),
                new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(indicatorRect.X, indicatorRect.Bottom - 2, indicatorRect.Width, 2),
                new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(indicatorRect.X, indicatorRect.Y, 2, indicatorRect.Height),
                new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(indicatorRect.Right - 2, indicatorRect.Y, 2, indicatorRect.Height),
                new Rectangle(0, 0, 1, 1), borderColor);

            //拖动时高亮
            if (IsDragging) {
                Color highlightColor = new Color(160, 190, 80) * (uiAlpha * 0.4f);
                spriteBatch.Draw(pixel, indicatorRect, new Rectangle(0, 0, 1, 1), highlightColor);
            }
        }

        private void DrawToxicFlow(SpriteBatch spriteBatch, Rectangle indicatorRect,
            Texture2D pixel, float uiAlpha) {
            //毒液流动效果
            float flowTimer = (float)Main.timeForVisualEffects * 0.05f;
            int flowCount = 3;

            for (int i = 0; i < flowCount; i++) {
                float offset = (flowTimer + i * 0.33f) % 1f;
                int flowY = indicatorRect.Y + (int)(offset * indicatorRect.Height);
                float flowAlpha = (float)Math.Sin(offset * MathHelper.Pi) * 0.5f;

                Color flowColor = new Color(140, 170, 70) * (uiAlpha * 0.25f * flowAlpha);
                Rectangle flowRect = new Rectangle(indicatorRect.X + 2, flowY, indicatorRect.Width - 4, 2);
                spriteBatch.Draw(pixel, flowRect, new Rectangle(0, 0, 1, 1), flowColor);
            }
        }

        /// <summary>
        /// 重置滚动条状态
        /// </summary>
        public void Reset() {
            IsDragging = false;
        }
    }
}
