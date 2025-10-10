using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
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
    /// 领域控制面板 - 从主面板上方展开，控制海域领域的层数
    /// </summary>
    internal class DomainUI : UIHandle
    {
        public static DomainUI Instance => UIHandleLoader.GetUIHandleOfType<DomainUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; // 手动调用
        
        // 展开控制
        private float expandProgress = 0f; // 展开进度（0-1）
        private const float ExpandDuration = 20f; // 展开动画持续帧数
        
        // 面板尺寸和位置
        private const int PanelWidth = 420; // 面板宽度
        private const int PanelHeight = 320; // 面板高度
        private Vector2 anchorPosition; // 锚点位置（主面板上方中心）
        
        // 九只奈落之眼
        private readonly List<SeaEyeButton> eyes = [];
        private const int MaxEyes = 9;
        private const float EyeOrbitRadius = 140f; // 眼睛轨道半径
        
        // 大比目鱼中心图标
        private Vector2 halibutCenter;
        private const float HalibutSize = 80f;
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
            // 计算锚点位置（主面板上方中心）
            Vector2 mainPanelPos = HalibutUIPanel.Instance.DrawPosition;
            Vector2 mainPanelSize = HalibutUIPanel.Instance.Size;
            anchorPosition = mainPanelPos + new Vector2(mainPanelSize.X / 2, -PanelHeight - 10);
            
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
                    expandProgress -= 1f / (ExpandDuration * 0.6f); // 收起稍快
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }
            
            // 计算当前位置和尺寸
            float easedProgress = expandProgress < 1f ? EaseOutBack(expandProgress) : 1f;
            if (!ShouldShow && expandProgress > 0f)
            {
                easedProgress = EaseInCubic(expandProgress);
            }
            
            // 从上方滑下并缩放
            float verticalOffset = (1f - easedProgress) * -150f;
            DrawPosition = anchorPosition + new Vector2(-PanelWidth / 2, verticalOffset);
            Size = new Vector2(PanelWidth * easedProgress, PanelHeight * easedProgress);
            
            if (expandProgress < 0.01f) return; // 完全收起时不更新
            
            // 更新中心位置
            halibutCenter = DrawPosition + new Vector2(PanelWidth / 2, PanelHeight / 2);
            
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
                if (particleTimer % 10 == 0)
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
                float radius = 60f + index * 25f; // 基础半径 + 层级间距
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
            float radius = Main.rand.NextFloat(50f, 150f);
            Vector2 pos = halibutCenter + angle.ToRotationVector2() * radius;
            Vector2 velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            particles.Add(new EyeParticle(pos, velocity, new Color(120, 200, 255, 200)));
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            if (expandProgress < 0.01f) return; // 完全收起时不绘制
            
            float alpha = Math.Min(expandProgress * 1.5f, 1f);

            // 绘制背景面板
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
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 
                (int)Size.X, (int)Size.Y);
            
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.5f));
            
            // 主背景（深蓝色半透明）
            Color bgColor = new Color(20, 40, 80) * (alpha * 0.85f);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgColor);
            
            // 边框发光（绘制4条线）
            Color borderColor = new Color(80, 160, 255) * (alpha * 0.6f);
            int borderWidth = 2;
            
            // 上边框
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, borderWidth), 
                new Rectangle(0, 0, 1, 1), borderColor);
            // 下边框
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - borderWidth, panelRect.Width, borderWidth), 
                new Rectangle(0, 0, 1, 1), borderColor);
            // 左边框
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, borderWidth, panelRect.Height), 
                new Rectangle(0, 0, 1, 1), borderColor);
            // 右边框
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - borderWidth, panelRect.Y, borderWidth, panelRect.Height), 
                new Rectangle(0, 0, 1, 1), borderColor);
            
            // 内边框
            Rectangle innerBorder = panelRect;
            innerBorder.Inflate(-4, -4);
            Color innerColor = new Color(120, 200, 255) * (alpha * 0.3f);
            
            // 上边框
            spriteBatch.Draw(pixel, new Rectangle(innerBorder.X, innerBorder.Y, innerBorder.Width, 1), 
                new Rectangle(0, 0, 1, 1), innerColor);
            // 下边框
            spriteBatch.Draw(pixel, new Rectangle(innerBorder.X, innerBorder.Bottom - 1, innerBorder.Width, 1), 
                new Rectangle(0, 0, 1, 1), innerColor);
            // 左边框
            spriteBatch.Draw(pixel, new Rectangle(innerBorder.X, innerBorder.Y, 1, innerBorder.Height), 
                new Rectangle(0, 0, 1, 1), innerColor);
            // 右边框
            spriteBatch.Draw(pixel, new Rectangle(innerBorder.Right - 1, innerBorder.Y, 1, innerBorder.Height), 
                new Rectangle(0, 0, 1, 1), innerColor);
        }
        
        private void DrawConnectionLines(SpriteBatch spriteBatch, float alpha)
        {
            if (ActiveEyeCount == 0) return;
            
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
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + eye.Index * 0.5f) * 2f;
                
                // 绘制发光连接线
                Color lineColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255), 
                    (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index) * 0.5f + 0.5f);
                lineColor *= alpha * 0.4f;
                
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), 
                    lineColor, rotation, Vector2.Zero, new Vector2(length, 2f + wave), SpriteEffects.None, 0f);
            }
        }
        
        private void DrawHalibut(SpriteBatch spriteBatch, float alpha)
        {
            Texture2D halibutTex = TextureAssets.Item[HalibutOverride.ID].Value;

            // 绘制发光光环（多层）
            for (int i = 0; i < 3; i++)
            {
                float glowScale = (HalibutSize / halibutTex.Width) * (1.3f + i * 0.2f) * halibutPulse;
                Color glowColor = Color.Lerp(new Color(100, 200, 255), new Color(80, 160, 240), i / 3f);
                glowColor *= alpha * (0.3f - i * 0.08f);
                
                spriteBatch.Draw(halibutTex, halibutCenter, null, 
                    glowColor, halibutRotation + i * 0.1f, halibutTex.Size() / 2, glowScale, SpriteEffects.None, 0f);
            }
            
            // 绘制主体
            float mainScale = (HalibutSize / halibutTex.Width) * halibutPulse;
            Color mainColor = Color.White * alpha;
            spriteBatch.Draw(halibutTex, halibutCenter, null, 
                mainColor, halibutRotation, halibutTex.Size() / 2, mainScale, SpriteEffects.None, 0f);
            
            // 绘制层数文字
            if (ActiveEyeCount > 0 && expandProgress >= 1f)
            {
                string layerText = $"{ActiveEyeCount}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(layerText);
                Vector2 textPos = halibutCenter - textSize / 2 + new Vector2(0, HalibutSize * 0.6f);
                
                // 文字发光
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4;
                    Vector2 offset = angle.ToRotationVector2() * 2f;
                    Utils.DrawBorderString(spriteBatch, layerText, textPos + offset, 
                        Color.Gold * alpha * 0.6f, 1.2f);
                }
                
                Utils.DrawBorderString(spriteBatch, layerText, textPos, Color.White * alpha, 1.2f);
            }
        }
        
        private void DrawTitle(SpriteBatch spriteBatch, float alpha)
        {
            string title = "海域领域";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title);
            Vector2 titlePos = DrawPosition + new Vector2(PanelWidth / 2 - titleSize.X / 2, 10);
            
            // 标题发光
            Color titleGlow = Color.Gold * alpha * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow, 1f);
            }
            
            Color titleColor = Color.Lerp(Color.Gold, Color.White, 0.3f) * alpha;
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor, 1f);
        }
        
        private void DrawEyeTooltip(SpriteBatch spriteBatch, SeaEyeButton eye, float alpha)
        {
            string text = eye.IsActive ? $"第 {eye.Index + 1} 层 (点击关闭)" : $"第 {eye.Index + 1} 层 (点击开启)";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
            Vector2 textPos = eye.Position + new Vector2(-textSize.X / 2, -30);
            
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            // 背景
            Rectangle bgRect = new Rectangle((int)textPos.X - 4, (int)textPos.Y - 2, 
                (int)textSize.X + 8, (int)textSize.Y + 4);
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), new Color(0, 0, 0) * (alpha * 0.8f));
            
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
            Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * alpha, 0.8f);
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
        private const float EyeSize = 32f;
        
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
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + Index * 0.3f) * 3f;
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
                Color glowColor = new Color(100, 220, 255) * (alpha * glowIntensity * 0.6f);
                float glowPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + Index) * 0.3f + 0.7f;
                spriteBatch.Draw(SeaEye, drawPos, sourceRect, glowColor * glowPulse, 
                    0f, origin, scale * 1.3f, SpriteEffects.None, 0f);
            }
            
            // 绘制主体
            Color eyeColor = IsActive ? Color.White : new Color(100, 100, 120);
            eyeColor *= alpha * glowIntensity;
            spriteBatch.Draw(SeaEye, drawPos, sourceRect, eyeColor, 0f, origin, scale, SpriteEffects.None, 0f);
            
            // 绘制序号（小字）
            string indexText = $"{Index + 1}";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(indexText);
            Vector2 textPos = drawPos + new Vector2(0, EyeSize * 0.65f) - textSize / 2;
            Utils.DrawBorderString(spriteBatch, indexText, textPos, Color.White * alpha * 0.7f, 0.6f);
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
            int segments = 60;
            float angleStep = MathHelper.TwoPi / segments;
            
            // 颜色渐变（从内到外）
            float colorProgress = LayerIndex / 9f;
            Color baseColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255), colorProgress);
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep + rotation;
                float angle2 = (i + 1) * angleStep + rotation;
                
                // 波动效果
                float wave1 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle1 * 2f) * 4f;
                float wave2 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle2 * 2f) * 4f;
                
                Vector2 p1 = Center + angle1.ToRotationVector2() * (CurrentRadius + wave1);
                Vector2 p2 = Center + angle2.ToRotationVector2() * (CurrentRadius + wave2);
                
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float segRotation = diff.ToRotation();
                
                // 亮度波动
                float brightness = 0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + angle1 * 3f) * 0.3f;
                Color segColor = baseColor * brightness * Alpha * panelAlpha * 0.7f;
                
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), 
                    segColor, segRotation, Vector2.Zero, new Vector2(length, 2f), SpriteEffects.None, 0f);
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
            MaxLife = Main.rand.NextFloat(30f, 60f);
            Scale = Main.rand.NextFloat(0.6f, 1.2f);
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
            float alpha = (1f - progress) * panelAlpha * 0.8f;
            
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Color drawColor = Color * alpha;
            
            spriteBatch.Draw(tex, Position, null, drawColor, Rotation, 
                tex.Size() / 2, Scale * (0.3f + progress * 0.2f), SpriteEffects.None, 0f);
        }
    }
}
