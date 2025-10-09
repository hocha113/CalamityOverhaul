﻿using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 技能介绍面板 - 从主面板右侧滑出展开
    /// </summary>
    internal class SkillTooltipPanel : UIHandle
    {
        public static SkillTooltipPanel Instance => UIHandleLoader.GetUIHandleOfType<SkillTooltipPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; // 手动调用
        
        // 显示控制
        public FishSkill CurrentSkill; // 当前要显示的技能
        private FishSkill lastSkill; // 上一次显示的技能（用于检测切换）
        private bool shouldShow = false; // 内部状态：是否应该显示
        
        // 收回延迟
        private int hideDelayTimer = 0; // 收回延迟计时器
        private const int HideDelay = 15; // 收回延迟（15帧 = 0.25秒）
        private bool pendingHide = false; // 是否等待收回
        
        // 动画相关
        private float expandProgress = 0f; // 展开进度（0-1）
        private const float ExpandDuration = 15f; // 展开动画持续帧数
        private float contentFadeProgress = 0f; // 内容淡入进度
        private const float ContentFadeDelay = 0.4f; // 内容在展开40%后开始淡入
        
        // 面板尺寸
        private float currentWidth = 0f; // 当前宽度（动画中）
        private float targetWidth = 0f; // 目标宽度
        private const float MinWidth = 8f; // 最小宽度（完全收起时）
        
        // 位置相关
        private Vector2 anchorPosition; // 锚点位置（主面板右侧）
        
        // 内容布局
        private const int Padding = 12; // 内边距（从16减少到12）
        private const int LineSpacing = 6; // 行间距（从8减少到6）
        private const int IconSize = 48; // 图标大小
        
        /// <summary>
        /// 是否正在显示或展开中
        /// </summary>
        public bool IsShowing => shouldShow || expandProgress > 0.01f;
        
        /// <summary>
        /// 是否完全收起
        /// </summary>
        public bool IsFullyClosed => expandProgress <= 0.01f;
        
        /// <summary>
        /// 显示指定技能的介绍面板
        /// </summary>
        public void Show(FishSkill skill, Vector2 mainPanelPosition, Vector2 mainPanelSize)
        {
            if (skill == null) return;
            
            // 检测技能切换
            if (CurrentSkill != skill)
            {
                lastSkill = CurrentSkill;
                CurrentSkill = skill;
                
                // 如果之前没有显示，从头开始动画
                if (!shouldShow)
                {
                    expandProgress = 0f;
                    contentFadeProgress = 0f;
                }
                else
                {
                    // 如果切换技能，内容立即淡出再淡入
                    contentFadeProgress = 0f;
                }
            }
            
            shouldShow = true;
            pendingHide = false; // 取消待收回状态
            hideDelayTimer = 0; // 重置延迟计时器
            
            // 计算锚点位置（主面板右侧中心）
            anchorPosition = mainPanelPosition + new Vector2(mainPanelSize.X, mainPanelSize.Y / 2);
            
            // 计算目标宽度
            targetWidth = TooltipPanel.Width;
        }
        
        /// <summary>
        /// 隐藏介绍面板（带延迟）
        /// </summary>
        public void Hide()
        {
            if (!pendingHide && shouldShow)
            {
                pendingHide = true;
                hideDelayTimer = 0;
            }
        }
        
        /// <summary>
        /// 强制立即隐藏（无延迟）
        /// </summary>
        public void ForceHide()
        {
            shouldShow = false;
            pendingHide = false;
            hideDelayTimer = 0;
        }

        /// <summary>
        /// EaseOutBack缓动 - 带回弹效果
        /// </summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }
        
        /// <summary>
        /// EaseInCubic缓动 - 快速收起
        /// </summary>
        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }
        
        public override void Update()
        {
            // 处理延迟收回逻辑
            if (pendingHide)
            {
                hideDelayTimer++;
                if (hideDelayTimer >= HideDelay)
                {
                    shouldShow = false;
                    pendingHide = false;
                    hideDelayTimer = 0;
                }
            }
            
            if (shouldShow && CurrentSkill != null)
            {
                // 展开动画
                if (expandProgress < 1f)
                {
                    expandProgress += 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
                
                // 内容淡入（延迟开始）
                if (expandProgress > ContentFadeDelay && contentFadeProgress < 1f)
                {
                    float adjustedProgress = (expandProgress - ContentFadeDelay) / (1f - ContentFadeDelay);
                    contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
                }
            }
            else
            {
                // 收起动画（更快）
                if (expandProgress > 0f)
                {
                    expandProgress -= 1f / (ExpandDuration * 0.5f);
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
                
                // 内容快速淡出
                if (contentFadeProgress > 0f)
                {
                    contentFadeProgress -= 0.15f;
                    contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
                }
            }
            
            // 计算当前宽度（使用缓动函数）
            float easedProgress = shouldShow ? EaseOutBack(expandProgress) : EaseInCubic(expandProgress);
            currentWidth = MinWidth + (targetWidth - MinWidth) * easedProgress;
            
            // 更新位置和尺寸
            float panelHeight = TooltipPanel.Height;
            DrawPosition = anchorPosition + new Vector2(-4, -panelHeight / 2); // -4是为了与主面板稍微重叠
            Size = new Vector2(currentWidth, panelHeight);
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            // 完全收起时不绘制
            if (expandProgress <= 0.01f || CurrentSkill == null) return;
            
            float alpha = Math.Min(expandProgress * 2f, 1f); // 前50%进度快速淡入
            
            // 绘制面板主体（使用裁剪来实现从左到右展开的效果）
            Rectangle panelRect = new Rectangle(
                (int)DrawPosition.X,
                (int)DrawPosition.Y,
                (int)currentWidth,
                (int)Size.Y
            );
            
            // 计算源矩形（从左侧开始裁剪）
            Rectangle sourceRect = new Rectangle(
                0,
                0,
                (int)(TooltipPanel.Width * MathHelper.Clamp((currentWidth / targetWidth), 0, 1f)),
                TooltipPanel.Height
            );
            
            // 绘制阴影（略微偏移）
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            Color shadowColor = Color.Black * (alpha * 0.4f);
            spriteBatch.Draw(TooltipPanel, shadowRect, sourceRect, shadowColor);
            
            // 绘制面板主体
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color panelColor = Color.White * (alpha * pulse);
            spriteBatch.Draw(TooltipPanel, panelRect, sourceRect, panelColor);
            
            // 绘制左侧连接边缘的发光效果
            if (expandProgress > 0.1f)
            {
                Vector2 edgeStart = DrawPosition + new Vector2(0, 0);
                Vector2 edgeEnd = DrawPosition + new Vector2(0, Size.Y);
                float edgeGlowAlpha = Math.Min((expandProgress - 0.1f) / 0.2f, 1f);
                DrawVerticalGlow(spriteBatch, edgeStart, edgeEnd, Color.Gold * alpha * edgeGlowAlpha * 0.6f, 4f);
            }
            
            // 绘制边框高光
            if (expandProgress > 0.3f)
            {
                Color glowColor = Color.Gold with { A = 0 } * (alpha * 0.2f * pulse);
                Rectangle glowRect = panelRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(TooltipPanel, glowRect, sourceRect, glowColor);
            }
            
            // 绘制舞动的星星粒子（在面板上方）
            if (expandProgress > 0.5f)
            {
                DrawFloatingStars(spriteBatch, alpha);
            }
            
            // 绘制内容（只在展开足够时）
            if (expandProgress > ContentFadeDelay && currentWidth > targetWidth * 0.5f)
            {
                DrawContent(spriteBatch, alpha);
            }
        }
        
        /// <summary>
        /// 绘制舞动的星星粒子
        /// </summary>
        private void DrawFloatingStars(SpriteBatch spriteBatch, float panelAlpha)
        {
            float starTime = Main.GlobalTimeWrappedHourly * 4f;
            Vector2 panelCenter = DrawPosition + Size / 2;

            const float starCount = 3;
            // 绘制3个围绕面板舞动的星星
            for (int i = 0; i < starCount; i++)
            {
                float starPhase = starTime + i * MathHelper.TwoPi / starCount;
                
                // 计算椭圆轨迹位置
                float radiusX = currentWidth * 0.4f;
                float radiusY = Size.Y * 0.35f;
                Vector2 starPos = panelCenter + new Vector2(
                    (float)Math.Cos(starPhase) * radiusX,
                    (float)Math.Sin(starPhase) * radiusY
                );
                
                // 星星透明度：根据位置动态变化
                float starAlpha = ((float)Math.Sin(starPhase) * 0.5f + 0.5f) * panelAlpha * 0.6f;
                
                // 星星大小：远近效果
                float starSize = 3f + (float)Math.Sin(starPhase * 2f) * 1f;
                
                // 星星颜色：金色到白色渐变
                Color starColor = Color.Lerp(Color.Gold, Color.White, (float)Math.Sin(starPhase * 2f) * 0.5f + 0.5f);
                starColor *= starAlpha;
                
                // 绘制星星（带轻微的拖尾效果）
                DrawStarWithTrail(spriteBatch, starPos, starSize, starColor, starPhase);
            }
        }
        
        /// <summary>
        /// 绘制带拖尾效果的星星
        /// </summary>
        private void DrawStarWithTrail(SpriteBatch spriteBatch, Vector2 position, float size, Color color, float rotation)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 绘制拖尾（3个渐弱的残影）
            for (int i = 1; i <= 3; i++)
            {
                float trailPhase = rotation - i * 0.2f;
                Vector2 panelCenter = DrawPosition + Size / 2;
                float radiusX = currentWidth * 0.4f;
                float radiusY = Size.Y * 0.35f;
                Vector2 trailPos = panelCenter + new Vector2(
                    (float)Math.Cos(trailPhase) * radiusX,
                    (float)Math.Sin(trailPhase) * radiusY
                );
                
                float trailAlpha = (4 - i) / 4f * 0.4f;
                float trailSize = size * (1f - i * 0.15f);
                DrawStar(spriteBatch, trailPos, trailSize, color * trailAlpha);
            }
            
            // 绘制主星星（更亮）
            DrawStar(spriteBatch, position, size, color);
            
            // 绘制中心高光
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.8f, 
                0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.5f, size * 0.5f), SpriteEffects.None, 0);
        }
        
        /// <summary>
        /// 绘制垂直发光线条
        /// </summary>
        private void DrawVerticalGlow(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float length = Vector2.Distance(start, end);
            
            // 主光线
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, 
                MathHelper.PiOver2, new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
            
            // 外层柔和光晕
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color * 0.5f, 
                MathHelper.PiOver2, new Vector2(0, 0.5f), new Vector2(length, thickness * 2f), SpriteEffects.None, 0);
        }
        
        /// <summary>
        /// 绘制面板内容
        /// </summary>
        private void DrawContent(SpriteBatch spriteBatch, float panelAlpha)
        {
            if (CurrentSkill?.Icon == null) return;
            
            // 内容透明度
            float contentAlpha = contentFadeProgress * panelAlpha;
            if (contentAlpha <= 0.01f) return;
            
            // 内容区域起始位置
            Vector2 contentStart = DrawPosition + new Vector2(Padding, Padding);
            float availableWidth = currentWidth - Padding * 2;
            
            // 如果宽度不够，不绘制内容
            if (availableWidth < 100) return;
            
            // 1. 绘制技能图标（左上角）
            Vector2 iconPos = contentStart;
            Vector2 iconCenter = iconPos + new Vector2(IconSize / 2);
            
            // 图标旋转光晕
            float glowRotation = Main.GlobalTimeWrappedHourly * 0.5f;
            Color iconGlow = Color.Lerp(Color.Gold, Color.Orange, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f);
            iconGlow = iconGlow with { A = 0 } * (contentAlpha * 0.5f);
            
            for (int i = 0; i < 4; i++)
            {
                float angle = glowRotation + MathHelper.PiOver2 * i;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 2f;
                spriteBatch.Draw(CurrentSkill.Icon, iconCenter + offset, null, iconGlow, 0f, 
                    CurrentSkill.Icon.Size() / 2, (float)IconSize / CurrentSkill.Icon.Width * 1.1f, SpriteEffects.None, 0);
            }
            
            // 图标主体
            spriteBatch.Draw(CurrentSkill.Icon, iconCenter, null, Color.White * contentAlpha, 0f, 
                CurrentSkill.Icon.Size() / 2, (float)IconSize / CurrentSkill.Icon.Width, SpriteEffects.None, 0);
            
            // 2. 绘制技能名称（图标右侧）
            string displayName = CurrentSkill.DisplayName?.Value ?? "未知技能";
            Vector2 namePos = contentStart + new Vector2(IconSize + 12, IconSize / 4);
            
            // 限制名称宽度
            float nameMaxWidth = availableWidth - IconSize - 16;
            if (nameMaxWidth > 0)
            {
                // 名称发光轮廓
                Color nameGlowColor = Color.Gold with { A = 0 } * (contentAlpha * 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 1.2f;
                    Utils.DrawBorderString(spriteBatch, displayName, namePos + offset, nameGlowColor, 0.85f);
                }
                
                // 名称主体（渐变色）
                Color nameColor = Color.Lerp(Color.Gold, Color.White, 0.4f) * contentAlpha;
                Utils.DrawBorderString(spriteBatch, displayName, namePos, nameColor, 0.85f);
            }
            
            // 3. 绘制装饰分隔线
            Vector2 dividerStart = contentStart + new Vector2(0, IconSize + 12);
            Vector2 dividerEnd = dividerStart + new Vector2(availableWidth, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, 
                Color.Gold * contentAlpha * 0.7f, Color.Gold * contentAlpha * 0.1f, 1.5f);
            
            // 4. 绘制技能描述
            string tooltip = CurrentSkill.Tooltip?.Value ?? "暂无描述";
            Vector2 tooltipPos = dividerStart + new Vector2(0, 10);
            
            // 计算实际可用文本宽度
            int textMaxWidth = Math.Max(100, (int)availableWidth - 8);
            
            // 使用WordwrapString进行换行
            string[] lines = Utils.WordwrapString(tooltip, FontAssets.MouseText.Value, textMaxWidth, 20, out int lineCount);
            
            // 绘制每一行文本
            for (int i = 0; i < Math.Min(lines.Length, 7); i++) // 最多显示7行
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                
                // 移除末尾的连字符和空格（Terraria的WordwrapString会在某些情况下添加连字符）
                string line = lines[i].TrimEnd('-', ' ');
                if (string.IsNullOrEmpty(line)) continue;
                
                Vector2 linePos = tooltipPos + new Vector2(4, i * (LineSpacing + 14)); // 行高调整为14
                
                // 检查是否超出面板底部
                if (linePos.Y + 14 > DrawPosition.Y + Size.Y - Padding) break;
                
                // 文字阴影
                Utils.DrawBorderString(spriteBatch, line, linePos + new Vector2(1, 1), 
                    Color.Black * contentAlpha * 0.5f, 0.75f);
                
                // 文字主体
                Color textColor = Color.White * contentAlpha;
                Utils.DrawBorderString(spriteBatch, line, linePos, textColor, 0.75f);
            }
            
            // 5. 绘制装饰星星（在角落轻微闪烁）
            if (contentAlpha > 0.8f)
            {
                float starTime = Main.GlobalTimeWrappedHourly * 3f;
                
                // 右上角星星
                Vector2 topRightStar = DrawPosition + new Vector2(currentWidth - 12, 12);
                float star1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * contentAlpha * 0.7f;
                DrawStar(spriteBatch, topRightStar, 4f, Color.Gold * star1Alpha);
                
                // 右下角星星
                Vector2 bottomRightStar = DrawPosition + new Vector2(currentWidth - 16, Size.Y - 16);
                float star2Alpha = ((float)Math.Sin(starTime + MathHelper.Pi) * 0.5f + 0.5f) * contentAlpha * 0.7f;
                DrawStar(spriteBatch, bottomRightStar, 3f, Color.Gold * star2Alpha);
            }
        }
        
        /// <summary>
        /// 绘制渐变线条
        /// </summary>
        private void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            
            if (length < 1f) return;
            
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            
            // 分段绘制渐变
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, 
                    rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        
        /// <summary>
        /// 绉绘制星星装饰
        /// </summary>
        private void DrawStar(SpriteBatch spriteBatch, Vector2 position, float size, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 绘制八芒星
            // 横线
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, 
                0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            
            // 竖线
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, 
                MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            
            // 斜线1
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, 
                MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
            
            // 斜线2
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, 
                -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
        }
    }
}
