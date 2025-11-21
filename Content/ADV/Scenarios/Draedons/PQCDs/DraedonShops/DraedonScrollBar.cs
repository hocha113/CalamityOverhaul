using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops
{
    /// <summary>
    /// 可拖动的科技风格滚动条
    /// </summary>
    internal class DraedonScrollBar
    {
        //滚动条状态
        private bool isDragging = false;
        private float dragStartY = 0f;
        private int dragStartOffset = 0;
        
        //动画效果
        private float hoverProgress = 0f;
        private float glowIntensity = 0f;
        private float pulseTimer = 0f;
        
        //滚动条尺寸
        private const int BarWidth = 4;
        private const int IndicatorWidth = 6;
        private const int MinIndicatorHeight = 20;
        
        //交互区域扩展（方便点击）
        private const int InteractionPadding = 8;
        
        /// <summary>
        /// 获取滚动条背景矩形
        /// </summary>
        public Rectangle GetBarBackground(Vector2 panelPosition, int barHeight) {
            Vector2 barPos = panelPosition + new Vector2(GetBarX(panelPosition), 120);
            return new Rectangle((int)barPos.X, (int)barPos.Y, BarWidth, barHeight);
        }
        
        /// <summary>
        /// 获取滚动条指示器矩形
        /// </summary>
        public Rectangle GetIndicatorRect(Vector2 panelPosition, int barHeight, 
            int scrollOffset, int maxScroll, int totalItems, int visibleItems) {
            Vector2 barPos = panelPosition + new Vector2(GetBarX(panelPosition), 120);
            float scrollProgress = maxScroll > 0 ? scrollOffset / (float)maxScroll : 0f;
            
            int indicatorHeight = Math.Max(MinIndicatorHeight, barHeight * visibleItems / totalItems);
            int indicatorY = (int)(barPos.Y + scrollProgress * (barHeight - indicatorHeight));
            
            return new Rectangle((int)barPos.X - 1, indicatorY, IndicatorWidth, indicatorHeight);
        }
        
        /// <summary>
        /// 获取交互区域矩形（扩展的点击区域）
        /// </summary>
        public Rectangle GetInteractionRect(Rectangle indicatorRect) {
            return new Rectangle(
                indicatorRect.X - InteractionPadding,
                indicatorRect.Y - InteractionPadding,
                indicatorRect.Width + InteractionPadding * 2,
                indicatorRect.Height + InteractionPadding * 2
            );
        }
        
        /// <summary>
        /// 更新滚动条状态
        /// </summary>
        public void Update(Vector2 panelPosition, int barHeight, int scrollOffset, int maxScroll, 
            int totalItems, int visibleItems, Point mousePosition, bool mouseLeftDown, 
            bool mouseLeftRelease, out int newScrollOffset) {
            newScrollOffset = scrollOffset;
            
            Rectangle indicatorRect = GetIndicatorRect(panelPosition, barHeight, scrollOffset, 
                maxScroll, totalItems, visibleItems);
            Rectangle interactionRect = GetInteractionRect(indicatorRect);
            Rectangle barBg = GetBarBackground(panelPosition, barHeight);
            
            bool mouseOverIndicator = interactionRect.Contains(mousePosition);
            bool mouseOverBar = barBg.Contains(mousePosition);
            
            //更新悬停动画
            float targetHover = (mouseOverIndicator || isDragging) ? 1f : 0f;
            hoverProgress = MathHelper.Lerp(hoverProgress, targetHover, 0.2f);
            
            //更新发光强度
            float targetGlow = isDragging ? 1f : (mouseOverIndicator ? 0.6f : 0f);
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.15f);
            
            //更新脉冲计时器
            pulseTimer += 0.08f;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            
            //处理拖动开始
            if (mouseLeftDown && !isDragging && mouseOverIndicator) {
                isDragging = true;
                dragStartY = mousePosition.Y;
                dragStartOffset = scrollOffset;
                
                //播放抓取音效
                SoundEngine.PlaySound(SoundID.MenuTick with { 
                    Volume = 0.3f, 
                    Pitch = 0.5f,
                    MaxInstances = 1
                });
            }
            
            //处理拖动中
            if (isDragging && mouseLeftDown) {
                float dragDelta = mousePosition.Y - dragStartY;
                int indicatorHeight = Math.Max(MinIndicatorHeight, barHeight * visibleItems / totalItems);
                float availableHeight = barHeight - indicatorHeight;
                
                if (availableHeight > 0) {
                    float progressDelta = dragDelta / availableHeight;
                    int offsetDelta = (int)(progressDelta * maxScroll);
                    newScrollOffset = Math.Clamp(dragStartOffset + offsetDelta, 0, maxScroll);
                    
                    //如果滚动位置发生变化，播放细微音效
                    if (newScrollOffset != scrollOffset) {
                        if (Main.GameUpdateCount % 3 == 0) {
                            SoundEngine.PlaySound(SoundID.MenuTick with { 
                                Volume = 0.1f, 
                                Pitch = 0.3f,
                                MaxInstances = 1
                            });
                        }
                    }
                }
            }
            
            //处理拖动结束
            if (!mouseLeftDown && isDragging) {
                isDragging = false;
                
                //播放释放音效
                SoundEngine.PlaySound(SoundID.MenuTick with { 
                    Volume = 0.2f, 
                    Pitch = -0.3f,
                    MaxInstances = 1
                });
            }
            
            //处理点击滚动条背景直接跳转
            if (mouseLeftRelease && mouseOverBar && !mouseOverIndicator && maxScroll > 0) {
                float clickY = mousePosition.Y;
                float barTopY = barBg.Y;
                float clickProgress = (clickY - barTopY) / barHeight;
                clickProgress = MathHelper.Clamp(clickProgress, 0f, 1f);
                
                newScrollOffset = (int)(clickProgress * maxScroll);
                
                //播放跳转音效
                SoundEngine.PlaySound(SoundID.MenuTick with { 
                    Volume = 0.25f, 
                    Pitch = 0.0f,
                    MaxInstances = 1
                });
            }
        }
        
        /// <summary>
        /// 绘制滚动条
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, int barHeight, 
            int scrollOffset, int maxScroll, int totalItems, int visibleItems, 
            float uiAlpha, float circuitPulseTimer) {
            if (totalItems <= visibleItems) return;
            
            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            Rectangle barBg = GetBarBackground(panelPosition, barHeight);
            Rectangle indicatorRect = GetIndicatorRect(panelPosition, barHeight, scrollOffset, 
                maxScroll, totalItems, visibleItems);
            
            //绘制滚动条背景
            DrawScrollBarBackground(spriteBatch, barBg, pixel, uiAlpha, circuitPulseTimer);
            
            //绘制指示器
            DrawScrollIndicator(spriteBatch, indicatorRect, pixel, uiAlpha, circuitPulseTimer);
            
            //绘制拖动提示效果
            if (isDragging || hoverProgress > 0.01f) {
                DrawDragHint(spriteBatch, indicatorRect, pixel, uiAlpha);
            }
        }
        
        private void DrawScrollBarBackground(SpriteBatch spriteBatch, Rectangle barBg, 
            Texture2D pixel, float uiAlpha, float circuitPulseTimer) {
            //背景轨道
            Color bgColor = new Color(40, 100, 150) * (uiAlpha * 0.3f);
            spriteBatch.Draw(pixel, barBg, bgColor);
            
            //轨道边缘高光
            float edgePulse = (float)Math.Sin(circuitPulseTimer * 0.5f) * 0.5f + 0.5f;
            Color edgeColor = new Color(60, 140, 200) * (uiAlpha * 0.15f * edgePulse);
            
            Rectangle leftEdge = new Rectangle(barBg.X, barBg.Y, 1, barBg.Height);
            Rectangle rightEdge = new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height);
            
            spriteBatch.Draw(pixel, leftEdge, edgeColor);
            spriteBatch.Draw(pixel, rightEdge, edgeColor);
        }
        
        private void DrawScrollIndicator(SpriteBatch spriteBatch, Rectangle indicatorRect, 
            Texture2D pixel, float uiAlpha, float circuitPulseTimer) {
            //基础颜色随拖动和悬停变化
            Color baseColor = new Color(80, 200, 255);
            Color hoverColor = new Color(120, 220, 255);
            Color dragColor = new Color(150, 255, 200);
            
            Color currentColor = baseColor;
            if (isDragging) {
                currentColor = Color.Lerp(hoverColor, dragColor, 0.7f);
            }
            else if (hoverProgress > 0.01f) {
                currentColor = Color.Lerp(baseColor, hoverColor, hoverProgress);
            }
            
            //主指示器
            Color indicatorColor = currentColor * (uiAlpha * (0.8f + hoverProgress * 0.2f));
            spriteBatch.Draw(pixel, indicatorRect, indicatorColor);
            
            //脉冲效果
            float pulse = (float)Math.Sin(pulseTimer + circuitPulseTimer * 0.5f) * 0.5f + 0.5f;
            float pulseIntensity = 0.3f + glowIntensity * 0.4f;
            Color pulseColor = currentColor * (uiAlpha * pulse * pulseIntensity);
            
            Rectangle pulseRect = indicatorRect;
            pulseRect.Inflate(1, 0);
            spriteBatch.Draw(pixel, pulseRect, pulseColor * 0.5f);
            
            //发光边缘
            if (glowIntensity > 0.01f) {
                Color glowColor = currentColor * (uiAlpha * glowIntensity * 0.6f);
                
                Rectangle topGlow = new Rectangle(indicatorRect.X, indicatorRect.Y, indicatorRect.Width, 2);
                Rectangle bottomGlow = new Rectangle(indicatorRect.X, indicatorRect.Bottom - 2, indicatorRect.Width, 2);
                
                spriteBatch.Draw(pixel, topGlow, glowColor);
                spriteBatch.Draw(pixel, bottomGlow, glowColor * 0.8f);
            }
            
            //中心高光线
            if (hoverProgress > 0.01f || isDragging) {
                int centerY = indicatorRect.Y + indicatorRect.Height / 2;
                Rectangle centerLine = new Rectangle(indicatorRect.X + 1, centerY, indicatorRect.Width - 2, 1);
                Color centerColor = Color.White * (uiAlpha * 0.4f * (0.5f + hoverProgress * 0.5f));
                spriteBatch.Draw(pixel, centerLine, centerColor);
            }
            
            //能量流动效果（拖动时）
            if (isDragging) {
                DrawEnergyFlow(spriteBatch, indicatorRect, pixel, uiAlpha);
            }
        }
        
        private void DrawEnergyFlow(SpriteBatch spriteBatch, Rectangle indicatorRect, 
            Texture2D pixel, float uiAlpha) {
            int flowSegments = 3;
            for (int i = 0; i < flowSegments; i++) {
                float t = (pulseTimer * 0.5f + i * MathHelper.TwoPi / flowSegments) % MathHelper.TwoPi;
                float flowPos = (float)Math.Sin(t) * 0.5f + 0.5f;
                
                int flowY = indicatorRect.Y + (int)(flowPos * indicatorRect.Height);
                Rectangle flowRect = new Rectangle(indicatorRect.X, flowY, indicatorRect.Width, 2);
                
                float flowAlpha = (float)Math.Sin(t) * 0.5f + 0.5f;
                Color flowColor = new Color(150, 255, 200) * (uiAlpha * 0.6f * flowAlpha);
                
                spriteBatch.Draw(pixel, flowRect, flowColor);
            }
        }
        
        private void DrawDragHint(SpriteBatch spriteBatch, Rectangle indicatorRect, 
            Texture2D pixel, float uiAlpha) {
            //外发光框
            Rectangle outerGlow = indicatorRect;
            outerGlow.Inflate(2, 2);
            
            Color glowColor = new Color(100, 220, 255) * (uiAlpha * 0.2f * hoverProgress);
            if (isDragging) {
                glowColor = new Color(150, 255, 200) * (uiAlpha * 0.3f);
            }
            
            //绘制扩散的发光效果
            for (int i = 0; i < 3; i++) {
                Rectangle glowRect = outerGlow;
                glowRect.Inflate(i, i);
                float intensity = 1f - (i / 3f);
                spriteBatch.Draw(pixel, glowRect, glowColor * intensity * 0.3f);
            }
        }
        
        /// <summary>
        /// 重置滚动条状态
        /// </summary>
        public void Reset() {
            isDragging = false;
            hoverProgress = 0f;
            glowIntensity = 0f;
            pulseTimer = 0f;
        }
        
        /// <summary>
        /// 获取滚动条X坐标（相对于面板）
        /// </summary>
        private static float GetBarX(Vector2 panelPosition) {
            return 660; //PanelWidth (680) - 20
        }
        
        /// <summary>
        /// 是否正在拖动
        /// </summary>
        public bool IsDragging => isDragging;
    }
}
