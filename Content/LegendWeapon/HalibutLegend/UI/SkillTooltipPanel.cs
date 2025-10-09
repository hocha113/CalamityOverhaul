using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 技能介绍面板 - 当鼠标悬停在技能图标上时显示
    /// </summary>
    internal class SkillTooltipPanel : UIHandle
    {
        public static SkillTooltipPanel Instance => UIHandleLoader.GetUIHandleOfType<SkillTooltipPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; // 手动调用
        
        // 显示控制
        public FishSkill CurrentSkill; // 当前要显示的技能
        private FishSkill lastSkill; // 上一次显示的技能（用于检测切换）
        public bool ShouldShow; // 是否应该显示
        
        // 动画相关
        private float fadeProgress = 0f; // 淡入淡出进度（0-1）
        private const float FadeDuration = 10f; // 淡入淡出持续帧数
        private float scaleProgress = 0f; // 缩放进度（0-1）
        
        // 布局相关
        private Vector2 targetPosition; // 目标位置
        private Vector2 currentPosition; // 当前位置（平滑移动）
        private Vector2 positionVelocity; // 位置速度（用于平滑）
        
        // 内容尺寸
        private Vector2 contentSize; // 实际内容大小
        private const int Padding = 12; // 内边距
        private const int LineSpacing = 6; // 行间距
        private const int MaxTextWidth = 180; // 最大文本宽度
        
        /// <summary>
        /// 显示指定技能的介绍面板
        /// </summary>
        public void Show(FishSkill skill, Vector2 hoverPosition)
        {
            if (skill == null) return;
            
            // 检测技能切换
            if (CurrentSkill != skill)
            {
                lastSkill = CurrentSkill;
                CurrentSkill = skill;
                fadeProgress = 0f; // 重新开始淡入动画
                scaleProgress = 0f;
            }
            
            ShouldShow = true;
            
            // 计算面板位置（在悬停位置右侧偏上）
            targetPosition = hoverPosition + new Vector2(40, -20);
            
            // 边界检测，确保面板不会超出屏幕
            if (targetPosition.X + TooltipPanel.Width > Main.screenWidth - 20)
            {
                targetPosition.X = hoverPosition.X - TooltipPanel.Width - 20; // 显示在左侧
            }
            if (targetPosition.Y < 20)
            {
                targetPosition.Y = 20;
            }
            if (targetPosition.Y + TooltipPanel.Height > Main.screenHeight - 20)
            {
                targetPosition.Y = Main.screenHeight - TooltipPanel.Height - 20;
            }
        }
        
        /// <summary>
        /// 隐藏介绍面板
        /// </summary>
        public void Hide()
        {
            ShouldShow = false;
        }
        
        /// <summary>
        /// 平滑阻尼函数
        /// </summary>
        private Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 velocity, float smoothTime)
        {
            float deltaTime = 1f;
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            
            Vector2 change = current - target;
            Vector2 originalTo = target;
            
            float maxChange = float.MaxValue * smoothTime;
            change = Vector2.Clamp(change, new Vector2(-maxChange), new Vector2(maxChange));
            target = current - change;
            
            Vector2 temp = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temp) * exp;
            Vector2 output = target + (change + temp) * exp;
            
            if (Vector2.Dot(originalTo - current, output - originalTo) > 0)
            {
                output = originalTo;
                velocity = Vector2.Zero;
            }
            
            return output;
        }
        
        /// <summary>
        /// EaseOutCubic缓动
        /// </summary>
        private float EaseOutCubic(float t)
        {
            return 1 - (float)Math.Pow(1 - t, 3);
        }
        
        /// <summary>
        /// EaseOutBack缓动（带回弹）
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }
        
        public override void Update()
        {
            if (ShouldShow && CurrentSkill != null)
            {
                // 淡入动画
                if (fadeProgress < 1f)
                {
                    fadeProgress += 1f / FadeDuration;
                    fadeProgress = Math.Clamp(fadeProgress, 0f, 1f);
                }
                
                // 缩放动画
                if (scaleProgress < 1f)
                {
                    scaleProgress += 1f / FadeDuration;
                    scaleProgress = Math.Clamp(scaleProgress, 0f, 1f);
                }
                
                // 平滑移动到目标位置
                currentPosition = SmoothDamp(currentPosition, targetPosition, ref positionVelocity, 0.15f);
            }
            else
            {
                // 淡出动画
                if (fadeProgress > 0f)
                {
                    fadeProgress -= 1f / (FadeDuration * 0.5f); // 淡出更快
                    fadeProgress = Math.Clamp(fadeProgress, 0f, 1f);
                }
                
                if (scaleProgress > 0f)
                {
                    scaleProgress -= 1f / (FadeDuration * 0.5f);
                    scaleProgress = Math.Clamp(scaleProgress, 0f, 1f);
                }
            }
            
            DrawPosition = currentPosition;
            Size = TooltipPanel.Size();
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            // 完全不可见时不绘制
            if (fadeProgress <= 0f || CurrentSkill == null) return;
            
            // 计算动画参数
            float easedFade = EaseOutCubic(fadeProgress);
            float easedScale = EaseOutBack(scaleProgress);
            
            // 透明度
            float alpha = easedFade;
            
            // 缩放（从0.8到1.0，带回弹效果）
            float scale = 0.8f + easedScale * 0.2f;
            
            // 计算绘制中心点
            Vector2 panelCenter = DrawPosition + Size / 2;
            Vector2 origin = Size / 2;
            
            // 绘制阴影
            Color shadowColor = Color.Black * (alpha * 0.5f);
            Vector2 shadowOffset = new Vector2(4, 4);
            spriteBatch.Draw(TooltipPanel, panelCenter + shadowOffset, null, shadowColor, 0f, origin, scale, SpriteEffects.None, 0);
            
            // 绘制面板背景（带脉动效果）
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.1f + 0.9f;
            Color panelColor = Color.White * (alpha * pulse);
            spriteBatch.Draw(TooltipPanel, panelCenter, null, panelColor, 0f, origin, scale, SpriteEffects.None, 0);
            
            // 绘制边框发光效果
            Color glowColor = Color.Gold with { A = 0 } * (alpha * 0.3f * pulse);
            spriteBatch.Draw(TooltipPanel, panelCenter, null, glowColor, 0f, origin, scale * 1.05f, SpriteEffects.None, 0);
            
            // 只有完全显示时才绘制文本
            if (fadeProgress > 0.8f && scale > 0.95f)
            {
                DrawContent(spriteBatch, panelCenter, scale, alpha);
            }
        }
        
        /// <summary>
        /// 绘制面板内容（技能名称、图标、描述等）
        /// </summary>
        private void DrawContent(SpriteBatch spriteBatch, Vector2 center, float scale, float alpha)
        {
            if (CurrentSkill?.Icon == null) return;
            
            // 内容起始位置（考虑缩放）
            Vector2 contentStart = center - (Size / 2) * scale + new Vector2(Padding, Padding) * scale;
            
            // 1. 绘制技能图标
            Vector2 iconPos = contentStart;
            float iconSize = 40f * scale;
            Vector2 iconCenter = iconPos + new Vector2(iconSize / 2);
            
            // 图标背景光晕
            Color iconGlow = Color.Lerp(Color.Gold, Color.Orange, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f);
            iconGlow = iconGlow with { A = 0 } * (alpha * 0.4f);
            spriteBatch.Draw(CurrentSkill.Icon, iconCenter, null, iconGlow, 0f, CurrentSkill.Icon.Size() / 2, (iconSize / CurrentSkill.Icon.Width) * 1.2f, SpriteEffects.None, 0);
            
            // 图标主体
            spriteBatch.Draw(CurrentSkill.Icon, iconCenter, null, Color.White * alpha, 0f, CurrentSkill.Icon.Size() / 2, iconSize / CurrentSkill.Icon.Width, SpriteEffects.None, 0);
            
            // 2. 绘制技能名称
            string displayName = CurrentSkill.DisplayName?.Value ?? "未知技能";
            Vector2 namePos = contentStart + new Vector2(iconSize + 8 * scale, 0);
            
            // 名称发光效果
            Color nameGlowColor = Color.Gold with { A = 0 } * (alpha * 0.6f);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 1.5f * scale;
                Utils.DrawBorderString(spriteBatch, displayName, namePos + offset, nameGlowColor, scale * 0.9f);
            }
            
            // 名称主体
            Color nameColor = Color.Lerp(Color.Gold, Color.White, 0.3f) * alpha;
            Utils.DrawBorderString(spriteBatch, displayName, namePos, nameColor, scale * 0.9f);
            
            // 3. 绘制分隔线
            Vector2 dividerStart = contentStart + new Vector2(0, iconSize + 8 * scale);
            Vector2 dividerEnd = dividerStart + new Vector2(TooltipPanel.Width - Padding * 2, 0) * scale;
            
            // 使用渐变色绘制分隔线
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, Color.Gold * alpha * 0.6f, Color.Gold * alpha * 0.1f, 2f * scale);
            
            // 4. 绘制技能描述
            string tooltip = CurrentSkill.Tooltip?.Value ?? "暂无描述";
            Vector2 tooltipPos = dividerStart + new Vector2(4 * scale, 12 * scale);
            
            // 文本换行处理
            string[] lines = Utils.WordwrapString(tooltip, FontAssets.MouseText.Value, (int)(MaxTextWidth * scale), 10, out int lineCount);
            
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) {
                    continue;
                }

                Vector2 linePos = tooltipPos + new Vector2(0, i * 20 * scale);
                Color textColor = Color.White * alpha;
                
                // 文字阴影
                Utils.DrawBorderString(spriteBatch, lines[i], linePos + new Vector2(1, 1) * scale, Color.Black * alpha * 0.5f, scale * 0.8f);
                
                // 文字主体
                Utils.DrawBorderString(spriteBatch, lines[i], linePos, textColor, scale * 0.8f);
            }
            
            // 5. 绘制装饰性元素（角落的小星星）
            float starTime = Main.GlobalTimeWrappedHourly * 4f;
            for (int i = 0; i < 3; i++)
            {
                float starPhase = starTime + i * MathHelper.TwoPi / 3f;
                float starAlpha = ((float)Math.Sin(starPhase) * 0.5f + 0.5f) * alpha * 0.6f;
                
                Vector2 starPos = center + new Vector2(
                    (float)Math.Cos(starPhase) * 80f * scale,
                    (float)Math.Sin(starPhase) * 60f * scale
                );
                
                Color starColor = Color.Lerp(Color.Gold, Color.White, (float)Math.Sin(starPhase * 2f) * 0.5f + 0.5f) * starAlpha;
                
                // 绘制简单的十字星
                float starSize = 3f * scale;
                DrawCross(spriteBatch, starPos, starSize, starColor);
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
            edge.Normalize();
            
            // 分段绘制以实现渐变效果
            int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                float nextT = (float)(i + 1) / segments;
                
                Vector2 segStart = start + edge * (length * t);
                Vector2 segEnd = start + edge * (length * nextT);
                float segLength = Vector2.Distance(segStart, segEnd);
                
                Color color = Color.Lerp(startColor, endColor, t);
                
                float rotation = (float)Math.Atan2(edge.Y, edge.X);
                spriteBatch.Draw(pixel, segStart, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        
        /// <summary>
        /// 绘制十字星装饰
        /// </summary>
        private void DrawCross(SpriteBatch spriteBatch, Vector2 position, float size, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 横线
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0);
            
            // 竖线
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0);
        }
    }
}
