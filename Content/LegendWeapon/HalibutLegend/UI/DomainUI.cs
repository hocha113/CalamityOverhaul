using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 领域控制面板
    /// </summary>
    internal class DomainUI : UIHandle
    {
        public static DomainUI Instance => UIHandleLoader.GetUIHandleOfType<DomainUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; // 手动调用
        
        // 展开控制
        private float expandProgress = 0f; // 展开进度（0-1）
        private const float ExpandDuration = 10f; // 展开动画持续帧数
        
        // 面板尺寸（使用TooltipPanel的大小）
        private float PanelWidth => TooltipPanel.Width; // 214
        private float PanelHeight => TooltipPanel.Height; // 206
        
        // 位置相关
        private Vector2 anchorPosition; // 锚点位置（动态计算，跟随SkillTooltipPanel）
        private float currentWidth = 0f; // 当前宽度（用于从右到左展开动画）
        private float targetWidth = 0f; // 目标宽度
        private const float MinWidth = 8f; // 最小宽度（完全收起时）
        
        // 九只奈落之眼
        private readonly List<SeaEyeButton> eyes = [];
        private const int MaxEyes = 9;
        private const float EyeOrbitRadius = 75f; // 眼睛轨道半径
        
        // 大比目鱼中心图标
        private Vector2 halibutCenter;
        private const float HalibutSize = 45f;
        private float halibutRotation = 0f;
        private float halibutPulse = 0f;
        
        // 圆环效果
        private readonly List<DomainRing> rings = [];
        private int lastActiveEyeCount = 0;
        
        // 粒子效果
        private readonly List<EyeParticle> particles = [];
        private int particleTimer = 0;
        
        // 悬停和交互
        private bool hoveringPanel = false;
        private SeaEyeButton hoveredEye = null;
        
        // 内容淡入进度
        private float contentFadeProgress = 0f;
        private const float ContentFadeDelay = 0.4f; // 内容在展开40%后开始淡入
        
        /// <summary>
        /// 获取当前激活的眼睛数量（即领域层数）
        /// </summary>
        public int ActiveEyeCount => eyes.FindAll(e => e.IsActive).Count;
        
        /// <summary>
        /// 是否应该显示面板
        /// </summary>
        public bool ShouldShow => HalibutUIPanel.Instance.Sengs >= 1f;
        
        public DomainUI()
        {
            // 初始化九只眼睛
            for (int i = 0; i < MaxEyes; i++)
            {
                float angle = (i / (float)MaxEyes) * MathHelper.TwoPi - MathHelper.PiOver2; // 从顶部开始
                eyes.Add(new SeaEyeButton(i, angle));
            }
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
            // 计算锚点位置（动态跟随SkillTooltipPanel）
            Vector2 mainPanelPos = HalibutUIPanel.Instance.DrawPosition;
            Vector2 mainPanelSize = HalibutUIPanel.Instance.Size;
            
            // 基础锚点：主面板右侧中心
            Vector2 baseAnchor = mainPanelPos + new Vector2(mainPanelSize.X, mainPanelSize.Y / 2);
            
            // 如果SkillTooltipPanel正在显示，锚点需要右移
            if (SkillTooltipPanel.Instance.IsShowing)
            {
                // 获取SkillTooltipPanel的实际宽度
                float skillPanelWidth = SkillTooltipPanel.Instance.Size.X;
                anchorPosition = baseAnchor + new Vector2(skillPanelWidth - 10, 0); // -10是为了与技能面板重叠
            }
            else
            {
                anchorPosition = baseAnchor;
            }
            
            // 展开/收起动画
            if (ShouldShow)
            {
                if (expandProgress < 1f)
                {
                    expandProgress += 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }
            else
            {
                if (expandProgress > 0f)
                {
                    expandProgress -= 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }
            
            // 使用缓动函数
            float easedProgress = ShouldShow ? EaseOutBack(expandProgress) : EaseInCubic(expandProgress);
            
            // 计算当前宽度（从右到左展开）
            targetWidth = PanelWidth;
            currentWidth = MinWidth + (targetWidth - MinWidth) * easedProgress;
            
            // 计算位置（从右向左滑出）
            DrawPosition = anchorPosition + new Vector2(-6, -PanelHeight / 2 - 18); // -6是为了与前面的面板重叠
            Size = new Vector2(currentWidth, PanelHeight);
            
            if (expandProgress < 0.01f) return; // 完全收起时不更新
            
            // 更新中心位置（相对于当前实际显示宽度的中心）
            // currentWidth是动画宽度，但实际显示区域是从DrawPosition.X开始的revealWidth
            float revealWidth = PanelWidth * expandProgress;
            halibutCenter = DrawPosition + new Vector2(revealWidth / 2, PanelHeight / 2);
            
            // 检测面板悬停
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 
                (int)Size.X, (int)Size.Y);
            hoveringPanel = panelRect.Contains(Main.MouseScreen.ToPoint());
            
            if (hoveringPanel)
            {
                player.mouseInterface = true;
            }
            
            // 更新大比目鱼动画
            halibutRotation += 0.005f;
            halibutPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.1f + 0.9f;
            
            // 内容淡入（延迟开始）
            if (expandProgress > ContentFadeDelay && contentFadeProgress < 1f)
            {
                float adjustedProgress = (expandProgress - ContentFadeDelay) / (1f - ContentFadeDelay);
                contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
            }
            else if (expandProgress <= ContentFadeDelay && contentFadeProgress > 0f)
            {
                contentFadeProgress -= 0.15f;
                contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
            }
            
            // 更新眼睛
            hoveredEye = null;
            foreach (var eye in eyes)
            {
                eye.Update(halibutCenter, EyeOrbitRadius * easedProgress, easedProgress);
                
                if (eye.IsHovered && hoveringPanel)
                {
                    hoveredEye = eye;
                }
                
                // 处理点击
                if (eye.IsHovered && Main.mouseLeft && Main.mouseLeftRelease)
                {
                    eye.Toggle();
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    
                    // 生成粒子效果
                    SpawnEyeToggleParticles(eye);
                }
            }
            
            // 更新圆环
            int currentActiveCount = ActiveEyeCount;
            if (currentActiveCount != lastActiveEyeCount)
            {
                UpdateRings(currentActiveCount);
                lastActiveEyeCount = currentActiveCount;
            }
            
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                rings[i].Update();
                if (rings[i].ShouldRemove)
                {
                    rings.RemoveAt(i);
                }
            }
            
            // 更新粒子
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].Update();
                if (particles[i].Life >= particles[i].MaxLife)
                {
                    particles.RemoveAt(i);
                }
            }
            
            // 生成环境粒子
            if (expandProgress >= 1f && currentActiveCount > 0)
            {
                particleTimer++;
                if (particleTimer % 15 == 0)
                {
                    SpawnAmbientParticle();
                }
            }
        }
        
        private void UpdateRings(int targetCount)
        {
            // 移除多余的圆环
            while (rings.Count > targetCount)
            {
                rings.RemoveAt(rings.Count - 1);
            }
            
            // 添加新圆环
            while (rings.Count < targetCount)
            {
                int index = rings.Count;
                float radius = 30f + index * 12f;
                rings.Add(new DomainRing(halibutCenter, radius, index));
            }
        }
        
        private void SpawnEyeToggleParticles(SeaEyeButton eye)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = (i / 12f) * MathHelper.TwoPi;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);
                Color color = eye.IsActive ? new Color(100, 220, 255) : new Color(80, 80, 100);
                particles.Add(new EyeParticle(eye.Position, velocity, color));
            }
        }
        
        private void SpawnAmbientParticle()
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(30f, 70f);
            Vector2 pos = halibutCenter + angle.ToRotationVector2() * radius;
            Vector2 velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            particles.Add(new EyeParticle(pos, velocity, new Color(120, 200, 255, 200)));
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            if (expandProgress < 0.01f) return; // 完全收起时不绘制
            
            float alpha = Math.Min(expandProgress * 2f, 1f); // 前50%快速淡入
            
            // 绘制面板背景（使用TooltipPanel纹理，从右到左展开）
            DrawPanel(spriteBatch, alpha);
            
            // 绘制圆环（在所有内容下方）
            foreach (var ring in rings)
            {
                ring.Draw(spriteBatch, alpha);
            }
            
            // 绘制连接线（从眼睛到中心）
            DrawConnectionLines(spriteBatch, alpha);
            
            // 绘制粒子
            foreach (var particle in particles)
            {
                particle.Draw(spriteBatch, alpha);
            }
            
            // 绘制大比目鱼
            DrawHalibut(spriteBatch, alpha);
            
            // 绘制眼睛
            foreach (var eye in eyes)
            {
                eye.Draw(spriteBatch, alpha);
            }
            
            // 绘制悬停提示
            if (hoveredEye != null && expandProgress >= 1f)
            {
                DrawEyeTooltip(spriteBatch, hoveredEye, alpha);
            }
            
            // 绘制顶部标题
            if (expandProgress > 0.8f)
            {
                DrawTitle(spriteBatch, alpha);
            }
        }
        
        private void DrawPanel(SpriteBatch spriteBatch, float alpha)
        {
            // 计算源矩形（从左到右展开的裁剪效果，与SkillTooltipPanel一致）
            float revealProgress = expandProgress;
            int revealWidth = (int)(PanelWidth * revealProgress);
            
            Rectangle sourceRect = new Rectangle(
                0, // 从左侧开始显示
                0,
                revealWidth,
                (int)PanelHeight
            );
            
            Rectangle destRect = new Rectangle(
                (int)DrawPosition.X, // 从左侧对齐
                (int)DrawPosition.Y,
                revealWidth,
                (int)PanelHeight
            );
            
            // 绘制阴影
            Rectangle shadowRect = destRect;
            shadowRect.Offset(3, 3);
            Color shadowColor = Color.Black * (alpha * 0.4f);
            spriteBatch.Draw(TooltipPanel, shadowRect, sourceRect, shadowColor);
            
            // 绘制面板主体（带脉动效果）
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color panelColor = Color.White * (alpha * pulse);
            spriteBatch.Draw(TooltipPanel, destRect, sourceRect, panelColor);
            
            // 绘制左侧连接边缘的发光效果
            if (expandProgress > 0.1f)
            {
                Vector2 edgeStart = DrawPosition + new Vector2(0, 0);
                Vector2 edgeEnd = DrawPosition + new Vector2(0, Size.Y);
                float edgeGlowAlpha = Math.Min((expandProgress - 0.1f) / 0.2f, 1f);
                DrawVerticalGlow(spriteBatch, edgeStart, edgeEnd, Color.Gold * alpha * edgeGlowAlpha * 0.6f, 4f);
            }
            
            // 绘制边框发光（只在完全展开后）
            if (expandProgress > 0.9f)
            {
                Color glowColor = Color.Gold with { A = 0 } * (alpha * 0.3f * pulse);
                Rectangle glowRect = destRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(TooltipPanel, glowRect, sourceRect, glowColor);
            }
        }
        
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
        
        private void DrawConnectionLines(SpriteBatch spriteBatch, float alpha)
        {
            if (ActiveEyeCount == 0 || expandProgress < 0.5f) return;
            
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            foreach (var eye in eyes)
            {
                if (!eye.IsActive) continue;
                
                Vector2 start = halibutCenter;
                Vector2 end = eye.Position;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                
                // 计算连接线的波动
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + eye.Index * 0.5f) * 1.5f;
                
                // 绘制发光连接线
                Color lineColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255), 
                    (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index) * 0.5f + 0.5f);
                lineColor *= alpha * 0.35f * expandProgress;
                
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), 
                    lineColor, rotation, Vector2.Zero, new Vector2(length, 1.5f + wave), SpriteEffects.None, 0f);
            }
        }
        
        private void DrawHalibut(SpriteBatch spriteBatch, float alpha)
        {
            if (contentFadeProgress < 0.01f) return; // 内容未开始淡入时不绘制
            
            Texture2D halibutTex = TextureAssets.Item[HalibutOverride.ID].Value;
            
            float halibutAlpha = contentFadeProgress * alpha;
            
            // 绘制发光光环（2层）
            for (int i = 0; i < 2; i++)
            {
                float glowScale = (HalibutSize / halibutTex.Width) * (1.2f + i * 0.15f) * halibutPulse;
                Color glowColor = Color.Lerp(new Color(100, 200, 255), new Color(80, 160, 240), i / 2f);
                glowColor *= halibutAlpha * (0.3f - i * 0.1f);
                
                spriteBatch.Draw(halibutTex, halibutCenter, null, 
                    glowColor, halibutRotation + i * 0.1f, halibutTex.Size() / 2, glowScale, SpriteEffects.None, 0f);
            }
            
            // 绘制主体
            float mainScale = (HalibutSize / halibutTex.Width) * halibutPulse;
            Color mainColor = Color.White * halibutAlpha;
            spriteBatch.Draw(halibutTex, halibutCenter, null, 
                mainColor, halibutRotation, halibutTex.Size() / 2, mainScale, SpriteEffects.None, 0f);
            
            // 绘制层数文字
            if (ActiveEyeCount > 0 && expandProgress >= 1f)
            {
                string layerText = $"{ActiveEyeCount}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(layerText);
                Vector2 textPos = halibutCenter - textSize / 2 + new Vector2(0, HalibutSize * 0.55f);
                
                // 文字发光
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4;
                    Vector2 offset = angle.ToRotationVector2() * 1.5f;
                    Utils.DrawBorderString(spriteBatch, layerText, textPos + offset, 
                        Color.Gold * halibutAlpha * 0.6f, 1f);
                }
                
                Utils.DrawBorderString(spriteBatch, layerText, textPos, Color.White * halibutAlpha, 1f);
            }
        }
        
        private void DrawTitle(SpriteBatch spriteBatch, float alpha)
        {
            if (contentFadeProgress < 0.5f) return; // 内容淡入50%后才显示标题
            
            float titleAlpha = contentFadeProgress * alpha;
            string title = "海域领域";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title);
            Vector2 titlePos = DrawPosition + new Vector2(currentWidth / 2 - titleSize.X / 2, 4);
            
            // 标题发光
            Color titleGlow = Color.Gold * titleAlpha * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4;
                Vector2 offset = angle.ToRotationVector2() * 1.2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow, 0.85f);
            }
            
            Color titleColor = Color.Lerp(Color.Gold, Color.White, 0.3f) * titleAlpha;
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor, 0.85f);
        }
        
        private void DrawEyeTooltip(SpriteBatch spriteBatch, SeaEyeButton eye, float alpha)
        {
            string text = eye.IsActive ? $"第 {eye.Index + 1} 层" : $"第 {eye.Index + 1} 层";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
            Vector2 textPos = eye.Position + new Vector2(-textSize.X / 2, -55);
            
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 背景
            Rectangle bgRect = new Rectangle((int)textPos.X - 3, (int)textPos.Y - 1, 
                (int)textSize.X + 6, (int)textSize.Y + 2);
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), new Color(0, 0, 0) * (alpha * 0.75f));
            
            // 边框
            Color borderColor = new Color(100, 200, 255) * alpha;
            // 上边框
            spriteBatch.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, 1), 
                new Rectangle(0, 0, 1, 1), borderColor);
            // 下边框
            spriteBatch.Draw(pixel, new Rectangle(bgRect.X, bgRect.Bottom - 1, bgRect.Width, 1), 
                new Rectangle(0, 0, 1, 1), borderColor);
            // 左边框
            spriteBatch.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, 1, bgRect.Height), 
                new Rectangle(0, 0, 1, 1), borderColor);
            // 右边框
            spriteBatch.Draw(pixel, new Rectangle(bgRect.Right - 1, bgRect.Y, 1, bgRect.Height), 
                new Rectangle(0, 0, 1, 1), borderColor);
            
            // 文字
            Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * alpha, 0.7f);
        }
    }
    
    /// <summary>
    /// 奈落之眼按钮
    /// </summary>
    internal class SeaEyeButton
    {
        public int Index;
        public float Angle;
        public bool IsActive;
        public Vector2 Position;
        public bool IsHovered;
        
        private float hoverScale = 1f;
        private float glowIntensity = 0f;
        private float blinkTimer = 0f;
        private const float EyeSize = 20f;
        
        public SeaEyeButton(int index, float angle)
        {
            Index = index;
            Angle = angle;
            IsActive = false;
        }
        
        public void Toggle()
        {
            IsActive = !IsActive;
            blinkTimer = 15f; // 眨眼动画
        }
        
        public void Update(Vector2 center, float orbitRadius, float panelAlpha)
        {
            // 计算位置（带轻微波动）
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + Index * 0.3f) * 2f;
            Position = center + Angle.ToRotationVector2() * (orbitRadius + wave);
            
            // 检测悬停
            Rectangle hitbox = new Rectangle((int)(Position.X - EyeSize / 2), (int)(Position.Y - EyeSize / 2), 
                (int)EyeSize, (int)EyeSize);
            IsHovered = hitbox.Contains(Main.MouseScreen.ToPoint()) && panelAlpha >= 1f;
            
            // 悬停缩放动画
            float targetScale = IsHovered ? 1.2f : 1f;
            hoverScale = MathHelper.Lerp(hoverScale, targetScale, 0.15f);
            
            // 发光强度
            float targetGlow = IsActive ? 1f : 0.3f;
            if (IsHovered) targetGlow += 0.3f;
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.1f);
            
            // 眨眼动画
            if (blinkTimer > 0f)
            {
                blinkTimer--;
            }
        }
        
        public void Draw(SpriteBatch spriteBatch, float alpha)
        {
            if (SeaEye == null) return;
            
            // 计算源矩形（第一帧：闭眼，第二帧：睁眼）
            int frameHeight = SeaEye.Height / 2;
            bool shouldBlink = blinkTimer > 0f && blinkTimer % 10 < 5;
            int frame = (IsActive && !shouldBlink) ? 1 : 0;
            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, SeaEye.Width, frameHeight);
            
            Vector2 drawPos = Position;
            Vector2 origin = new Vector2(SeaEye.Width / 2, frameHeight / 2);
            float scale = (EyeSize / SeaEye.Width) * hoverScale;
            
            // 绘制外圈发光（仅激活时）
            if (IsActive)
            {
                Color glowColor = new Color(100, 220, 255) * (alpha * glowIntensity * 0.5f);
                float glowPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + Index) * 0.3f + 0.7f;
                spriteBatch.Draw(SeaEye, drawPos, sourceRect, glowColor * glowPulse, 
                    0f, origin, scale * 1.25f, SpriteEffects.None, 0f);
            }
            
            // 绘制主体
            Color eyeColor = IsActive ? Color.White : new Color(100, 100, 120);
            eyeColor *= alpha * glowIntensity;
            spriteBatch.Draw(SeaEye, drawPos, sourceRect, eyeColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }
    
    /// <summary>
    /// 领域圆环效果
    /// </summary>
    internal class DomainRing
    {
        public Vector2 Center;
        public float TargetRadius;
        public float CurrentRadius;
        public int LayerIndex;
        public float Alpha;
        public bool ShouldRemove;
        
        private float rotation = 0f;
        private float spawnProgress = 0f;
        private const float SpawnDuration = 30f;
        
        public DomainRing(Vector2 center, float radius, int index)
        {
            Center = center;
            TargetRadius = radius;
            CurrentRadius = 0f;
            LayerIndex = index;
            Alpha = 0f;
            ShouldRemove = false;
        }
        
        public void Update()
        {
            // 生成动画
            if (spawnProgress < 1f)
            {
                spawnProgress += 1f / SpawnDuration;
                spawnProgress = Math.Clamp(spawnProgress, 0f, 1f);
                
                float easedProgress = EaseOutCubic(spawnProgress);
                CurrentRadius = TargetRadius * easedProgress;
                Alpha = easedProgress;
            }
            else
            {
                CurrentRadius = TargetRadius;
                Alpha = 1f;
            }
            
            // 旋转
            rotation += 0.005f * (1f + LayerIndex * 0.1f);
        }
        
        private static float EaseOutCubic(float t)
        {
            return 1f - (float)Math.Pow(1f - t, 3);
        }
        
        public void Draw(SpriteBatch spriteBatch, float panelAlpha)
        {
            if (Alpha < 0.01f) return;
            
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 48;
            float angleStep = MathHelper.TwoPi / segments;
            
            // 颜色渐变（从内到外）
            float colorProgress = LayerIndex / 9f;
            Color baseColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255), colorProgress);
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep + rotation;
                float angle2 = (i + 1) * angleStep + rotation;
                
                // 波动效果
                float wave1 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle1 * 2f) * 2f;
                float wave2 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle2 * 2f) * 2f;
                
                Vector2 p1 = Center + angle1.ToRotationVector2() * (CurrentRadius + wave1);
                Vector2 p2 = Center + angle2.ToRotationVector2() * (CurrentRadius + wave2);
                
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float segRotation = diff.ToRotation();
                
                // 亮度波动
                float brightness = 0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + angle1 * 3f) * 0.3f;
                Color segColor = baseColor * brightness * Alpha * panelAlpha * 0.6f;
                
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), 
                    segColor, segRotation, Vector2.Zero, new Vector2(length, 1.5f), SpriteEffects.None, 0f);
            }
        }
    }
    
    /// <summary>
    /// 眼睛粒子效果
    /// </summary>
    internal class EyeParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;
        public float Rotation;
        public Color Color;
        
        public EyeParticle(Vector2 pos, Vector2 vel, Color color)
        {
            Position = pos;
            Velocity = vel;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(30f, 50f);
            Scale = Main.rand.NextFloat(0.5f, 1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Color = color;
        }
        
        public void Update()
        {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Rotation += 0.05f;
        }
        
        public void Draw(SpriteBatch spriteBatch, float panelAlpha)
        {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * panelAlpha * 0.7f;
            
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Color drawColor = Color * alpha;
            
            spriteBatch.Draw(tex, Position, null, drawColor, Rotation, 
                tex.Size() / 2, Scale * (0.3f + progress * 0.2f), SpriteEffects.None, 0f);
        }
    }
}
