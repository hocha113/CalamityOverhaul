﻿using InnoVault.UIHandles;
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
    ///<summary>
    ///领域控制面板
    ///</summary>
    internal class DomainUI : UIHandle
    {
        public static DomainUI Instance => UIHandleLoader.GetUIHandleOfType<DomainUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; //手动调用

        //展开控制
        private float expandProgress = 0f; //展开进度（0-1）
        private const float ExpandDuration = 10f; //展开动画持续帧数

        //面板尺寸（使用TooltipPanel的大小）
        private float PanelWidth => TooltipPanel.Width; //214
        private float PanelHeight => TooltipPanel.Height; //206

        //位置相关
        private Vector2 anchorPosition; //锚点位置（动态计算，跟随SkillTooltipPanel）
        private float currentWidth = 0f; //当前宽度（用于从右到左展开动画）
        private float targetWidth = 0f; //目标宽度
        private const float MinWidth = 8f; //最小宽度（完全收起时）

        //九只奈落之眼
        internal List<SeaEyeButton> eyes => player.GetModPlayer<HalibutSave>().eyes;
        internal List<SeaEyeButton> activationSequence => player.GetModPlayer<HalibutSave>().activationSequence;
        internal const int MaxEyes = 9;
        internal const float EyeOrbitRadius = 75f; //眼睛轨道半径

        //大比目鱼中心图标
        internal Vector2 halibutCenter;
        private const float HalibutSize = 45f;
        private float halibutRotation = 0f;
        private float halibutPulse = 0f;
        private readonly List<HalibutPulseEffect> halibutPulses = [];

        //圆环效果
        private readonly List<DomainRing> rings = [];
        internal int lastActiveEyeCount = 0;

        //粒子效果
        private readonly List<EyeParticle> particles = [];
        private int particleTimer = 0;

        //悬停和交互
        private bool hoveringPanel = false;
        private SeaEyeButton hoveredEye = null;

        //内容淡入进度
        private float contentFadeProgress = 0f;
        private const float ContentFadeDelay = 0.4f; //内容在展开40%后开始淡入

        //激活动画（眼睛飞向中心并放大）
        private readonly List<EyeActivationAnimation> activationAnimations = [];

        //复苏增长相关常量
        private const float BaseResurrectionRatePerEye = 0.02f; //单层基础复苏速度
        private const float GeometricFactor = 1.2f; //几何倍率（每更高一层的额外提高倍率）
        private const float CrashedEyeSideEffectRate = 0.0002f; //死机眼睛的极小副作用

        ///<summary>
        ///获取当前激活的眼睛数量（即领域层数）
        ///</summary>
        public int ActiveEyeCount => activationSequence.Count;

        ///<summary>
        ///是否应该显示面板
        ///</summary>
        public bool ShouldShow => HalibutUIPanel.Instance.Sengs >= 1f;

        ///<summary>
        ///EaseOutBack缓动 - 带回弹效果
        ///</summary>
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        ///<summary>
        ///EaseInCubic缓动 - 快速收起
        ///</summary>
        private static float EaseInCubic(float t) {
            return t * t * t;
        }

        public override void Update() {
            if (eyes.Count == 0) {
                for (int i = 0; i < MaxEyes; i++) {
                    float angle = (i / (float)MaxEyes) * MathHelper.TwoPi - MathHelper.PiOver2;
                    eyes.Add(new SeaEyeButton(i, angle));
                }
            }

            //计算锚点位置（动态跟随SkillTooltipPanel）
            Vector2 mainPanelPos = HalibutUIPanel.Instance.DrawPosition;
            Vector2 mainPanelSize = HalibutUIPanel.Instance.Size;
            Vector2 baseAnchor = mainPanelPos + new Vector2(mainPanelSize.X, mainPanelSize.Y / 2);

            //如果SkillTooltipPanel正在显示，锚点需要右移
            if (SkillTooltipPanel.Instance.IsShowing) {
                //获取SkillTooltipPanel的实际宽度
                float skillPanelWidth = SkillTooltipPanel.Instance.Size.X;
                anchorPosition = baseAnchor + new Vector2(skillPanelWidth - 10, 0); //-10是为了与技能面板重叠
            }
            else {
                anchorPosition = baseAnchor;
            }

            //展开/收起动画
            if (ShouldShow) {
                if (expandProgress < 1f) {
                    expandProgress += 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }
            else {
                if (expandProgress > 0f) {
                    expandProgress -= 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }

            //使用缓动函数
            float easedProgress = ShouldShow ? EaseOutBack(expandProgress) : EaseInCubic(expandProgress);

            //计算当前宽度（从右到左展开）
            targetWidth = PanelWidth;
            currentWidth = MinWidth + (targetWidth - MinWidth) * easedProgress;

            //计算位置（从右向左滑出）
            DrawPosition = anchorPosition + new Vector2(-6, -PanelHeight / 2 - 18); //-6是为了与前面的面板重叠
            Size = new Vector2(currentWidth, PanelHeight);

            if (expandProgress < 0.01f) {
                return; //完全收起时不更新
            }

            //更新中心位置（相对于当前实际显示宽度的中心）
            //currentWidth是动画宽度，但实际显示区域是从DrawPosition.X开始的revealWidth
            float revealWidth = PanelWidth * expandProgress;
            halibutCenter = DrawPosition + new Vector2(revealWidth / 2, PanelHeight / 2);

            //检测面板悬停
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)Size.X, (int)Size.Y);
            hoveringPanel = panelRect.Contains(Main.MouseScreen.ToPoint());
            if (hoveringPanel) {
                player.mouseInterface = true;
            }

            //更新大比目鱼动画
            halibutRotation += 0.005f;
            halibutPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.1f + 0.9f;

            //内容淡入（延迟开始）
            if (expandProgress > ContentFadeDelay && contentFadeProgress < 1f) {
                float adjustedProgress = (expandProgress - ContentFadeDelay) / (1f - ContentFadeDelay);
                contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
            }
            else if (expandProgress <= ContentFadeDelay && contentFadeProgress > 0f) {
                contentFadeProgress -= 0.15f;
                contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
            }

            //更新眼睛
            hoveredEye = null;
            foreach (var eye in eyes) {
                eye.Update(halibutCenter, EyeOrbitRadius * easedProgress, easedProgress);
                if (eye.IsHovered && hoveringPanel) {
                    hoveredEye = eye;
                }
                if (eye.IsHovered && Main.mouseLeft && Main.mouseLeftRelease) {
                    HandleEyeToggle(eye);
                    player.GetOverride<HalibutPlayer>().SeaDomainLayers = activationSequence.Count;
                }
            }

            //更新圆环
            int currentActiveCount = ActiveEyeCount;
            if (currentActiveCount != lastActiveEyeCount) {
                UpdateRings(currentActiveCount);
                lastActiveEyeCount = currentActiveCount;
            }

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Center = halibutCenter;
                rings[i].Update();
                if (rings[i].ShouldRemove) {
                    rings.RemoveAt(i);
                }
            }

            //更新粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].Life >= particles[i].MaxLife) {
                    particles.RemoveAt(i);
                }
            }

            //生成环境粒子
            if (expandProgress >= 1f && currentActiveCount > 0) {
                particleTimer++;
                if (particleTimer % 15 == 0) {
                    SpawnAmbientParticle();
                }
            }

            for (int i = activationAnimations.Count - 1; i >= 0; i--) {
                activationAnimations[i].Update(halibutCenter);
                if (activationAnimations[i].Finished) {
                    halibutPulses.Add(new HalibutPulseEffect(halibutCenter));
                    activationAnimations.RemoveAt(i);
                }
            }

            for (int i = halibutPulses.Count - 1; i >= 0; i--) {
                halibutPulses[i].Update();
                if (halibutPulses[i].Finished) {
                    halibutPulses.RemoveAt(i);
                }
            }

            //同步HalibutPlayer
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.SeaDomainLayers = activationSequence.Count;
            }

            //每帧更新复苏速度，确保死机状态变化能及时反映
            UpdateResurrectionRate();
        }

        /// <summary>
        /// 根据激活眼睛数量与层级计算复苏速度：
        /// 未死机的眼睛：Base * GeometricFactor^(层级-1)
        /// 死机的眼睛：仅添加极小副作用（CrashedEyeSideEffectRate）
        /// 结果为绝对设置，不进行累加，保证稳定性
        /// </summary>
        private void UpdateResurrectionRate() {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            float rate = 0f;
            int crashLevel = halibutPlayer.CrashesLevel();

            for (int i = 0; i < activationSequence.Count; i++) {
                SeaEyeButton eye = activationSequence[i];
                if (!eye.IsActive) {
                    continue;
                }

                int layer = eye.LayerNumber ?? 1;
                bool isCrashed = layer <= crashLevel;

                if (isCrashed) {
                    rate += CrashedEyeSideEffectRate;
                }
                else {
                    float eyeRate = BaseResurrectionRatePerEye * MathF.Pow(GeometricFactor, layer - 1);
                    rate += eyeRate;
                }
            }

            halibutPlayer.ResurrectionSystem.ResurrectionRate = rate;
        }

        private void HandleEyeToggle(SeaEyeButton eye) {
            bool wasActive = eye.IsActive;
            eye.Toggle();
            SoundEngine.PlaySound(SoundID.MenuTick);
            if (!wasActive && eye.IsActive) {
                if (!activationSequence.Contains(eye)) {
                    activationSequence.Add(eye);
                    eye.LayerNumber = activationSequence.Count;
                }
                activationAnimations.Add(new EyeActivationAnimation(eye.Position, eye.LayerNumber ?? 1));
                SpawnEyeToggleParticles(eye, true);
            }
            else if (wasActive && !eye.IsActive) {
                activationSequence.Remove(eye);
                RecalculateLayerNumbers();
                SpawnEyeToggleParticles(eye, false);
            }

            //切换时立即刷新复苏速度
            UpdateResurrectionRate();
        }

        private void RecalculateLayerNumbers() {
            for (int i = 0; i < activationSequence.Count; i++) {
                activationSequence[i].LayerNumber = i + 1;
            }
        }

        internal void UpdateRings(int targetCount) {
            //移除多余的圆环
            while (rings.Count > targetCount) {
                rings.RemoveAt(rings.Count - 1);
            }

            //添加新圆环
            while (rings.Count < targetCount) {
                int index = rings.Count;
                float radius = 30f + index * 12f;
                rings.Add(new DomainRing(halibutCenter, radius, index));
            }
        }

        private void SpawnEyeToggleParticles(SeaEyeButton eye, bool activating) {
            //判断是否死机
            bool isCrashed = false;
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                int crashLevel = halibutPlayer.CrashesLevel();
                isCrashed = eye.LayerNumberDisplay <= crashLevel;
            }

            for (int i = 0; i < 12; i++) {
                float angle = (i / 12f) * MathHelper.TwoPi;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                //根据死机状态使用不同颜色
                Color color;
                if (isCrashed && activating) {
                    color = new Color(255, 100, 100); //红色粒子
                }
                else {
                    color = activating ? new Color(100, 220, 255) : new Color(80, 80, 100);
                }

                particles.Add(new EyeParticle(eye.Position, velocity, color));
            }
        }

        private void SpawnAmbientParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(30f, 70f);
            Vector2 pos = halibutCenter + angle.ToRotationVector2() * radius;
            Vector2 velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            particles.Add(new EyeParticle(pos, velocity, new Color(120, 200, 255, 200)));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (expandProgress < 0.01f) {
                return;
            }
            float alpha = Math.Min(expandProgress * 2f, 1f);
            DrawPanel(spriteBatch, alpha);
            foreach (var ring in rings) {
                ring.Draw(spriteBatch, alpha);
            }
            DrawConnectionLines(spriteBatch, alpha);
            foreach (var particle in particles) {
                particle.Draw(spriteBatch, alpha);
            }
            foreach (var pulse in halibutPulses) {
                pulse.Draw(spriteBatch, alpha);
            }
            DrawHalibut(spriteBatch, alpha);
            foreach (var eye in eyes) {
                eye.Draw(spriteBatch, alpha);
            }
            foreach (var anim in activationAnimations) {
                anim.Draw(spriteBatch, alpha);
            }
            if (expandProgress > 0.8f) {
                DrawTitle(spriteBatch, alpha);
            }
            if (hoveredEye != null && expandProgress >= 0.4f) {
                DrawEyeTooltip(spriteBatch, hoveredEye, alpha);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            //计算源矩形（从左到右展开的裁剪效果，与SkillTooltipPanel一致）
            float revealProgress = expandProgress;
            int revealWidth = (int)(PanelWidth * revealProgress);

            Rectangle sourceRect = new Rectangle(
                0, //从左侧开始显示
                0,
                revealWidth,
                (int)PanelHeight
            );

            Rectangle destRect = new Rectangle(
                (int)DrawPosition.X, //从左侧对齐
                (int)DrawPosition.Y,
                revealWidth,
                (int)PanelHeight
            );

            //绘制阴影
            Rectangle shadowRect = destRect;
            shadowRect.Offset(3, 3);
            Color shadowColor = Color.Black * (alpha * 0.4f);
            spriteBatch.Draw(TooltipPanel, shadowRect, sourceRect, shadowColor);

            //绘制面板主体（带脉动效果）
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color panelColor = Color.White * (alpha * pulse);
            spriteBatch.Draw(TooltipPanel, destRect, sourceRect, panelColor);

            //绘制边框发光（只在完全展开后）
            if (expandProgress > 0.9f) {
                Color glowColor = Color.Gold with { A = 0 } * (alpha * 0.3f * pulse);
                Rectangle glowRect = destRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(TooltipPanel, glowRect, sourceRect, glowColor);
            }
        }

        private void DrawConnectionLines(SpriteBatch spriteBatch, float alpha) {
            if (ActiveEyeCount == 0 || expandProgress < 0.5f) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            //获取死机等级
            int crashLevel = 0;
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                crashLevel = halibutPlayer.CrashesLevel();
            }

            foreach (var eye in activationSequence) {
                if (!eye.IsActive) {
                    continue;
                }
                Vector2 start = halibutCenter;
                Vector2 end = eye.Position;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + eye.Index * 0.5f) * 1.5f;

                //判断这个眼睛是否死机
                bool isCrashed = eye.LayerNumberDisplay <= crashLevel;

                //根据死机状态使用不同颜色
                Color lineColor;
                if (isCrashed) {
                    lineColor = Color.Lerp(new Color(255, 100, 100), new Color(200, 80, 80),
                        (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index) * 0.5f + 0.5f);
                }
                else {
                    lineColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255),
                        (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index) * 0.5f + 0.5f);
                }

                lineColor *= alpha * 0.35f * expandProgress;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor, rotation, Vector2.Zero, new Vector2(length, 1.5f + wave), SpriteEffects.None, 0f);
            }
        }

        private void DrawHalibut(SpriteBatch spriteBatch, float alpha) {
            if (contentFadeProgress < 0.01f) {
                return;
            }
            Texture2D halibutTex = TextureAssets.Item[HalibutOverride.ID].Value;
            float halibutAlpha = contentFadeProgress * alpha;
            for (int i = 0; i < 2; i++) {
                float glowScale = (HalibutSize / halibutTex.Width) * (1.2f + i * 0.15f) * halibutPulse;
                Color glowColor = Color.Lerp(new Color(100, 200, 255), new Color(80, 160, 240), i / 2f);
                glowColor *= halibutAlpha * (0.3f - i * 0.1f);
                spriteBatch.Draw(halibutTex, halibutCenter, null, glowColor, halibutRotation + i * 0.1f, halibutTex.Size() / 2, glowScale, SpriteEffects.None, 0f);
            }
            float mainScale = (HalibutSize / halibutTex.Width) * halibutPulse;
            Color mainColor = Color.White * halibutAlpha;
            spriteBatch.Draw(halibutTex, halibutCenter, null, mainColor, halibutRotation, halibutTex.Size() / 2, mainScale, SpriteEffects.None, 0f);
            if (ActiveEyeCount > 0 && expandProgress >= 1f) {
                string layerText = $"{ActiveEyeCount}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(layerText);
                Vector2 textPos = halibutCenter - textSize / 2 + new Vector2(0, HalibutSize * 0.55f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4;
                    Vector2 offset = angle.ToRotationVector2() * 1.5f;
                    Utils.DrawBorderString(spriteBatch, layerText, textPos + offset, Color.Gold * halibutAlpha * 0.6f, 1f);
                }
                Utils.DrawBorderString(spriteBatch, layerText, textPos, Color.White * halibutAlpha, 1f);
            }
        }

        private void DrawTitle(SpriteBatch spriteBatch, float alpha) {
            if (contentFadeProgress < 0.5f) {
                return;
            }
            float titleAlpha = contentFadeProgress * alpha;
            string title = "海域领域";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title);
            Vector2 titlePos = DrawPosition + new Vector2(currentWidth / 2 - titleSize.X / 2, 4);
            Color titleGlow = Color.Gold * titleAlpha * 0.5f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4;
                Vector2 offset = angle.ToRotationVector2() * 1.2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow, 0.85f);
            }
            Color titleColor = Color.Lerp(Color.Gold, Color.White, 0.3f) * titleAlpha;
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor, 0.85f);
        }

        private void DrawEyeTooltip(SpriteBatch spriteBatch, SeaEyeButton eye, float alpha) {
            int displayLayer = eye.LayerNumberDisplay;
            string layerChinese = ChineseNumeral(displayLayer);
            string title = $"第 {layerChinese} 层";
            string desc = DomainEyeDescriptions.GetDescription(displayLayer);
            float tooltipAlpha = alpha * 0.95f;
            Vector2 panelSize = new Vector2(160, 110);
            Vector2 basePos = MousePosition + new Vector2(18, -panelSize.Y - 8);
            if (basePos.X + panelSize.X > Main.screenWidth - 20) {
                basePos.X = Main.screenWidth - panelSize.X - 20;
            }
            if (basePos.Y < 20) {
                basePos.Y = 20;
            }
            Rectangle panelRect = new Rectangle((int)basePos.X, (int)basePos.Y, (int)panelSize.X, (int)panelSize.Y);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color shadow = Color.Black * (tooltipAlpha * 0.5f);
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), shadow);
            float openProg = Math.Min(1f, contentFadeProgress * 1.3f);
            Color bgColor = new Color(25, 35, 55) * (tooltipAlpha * 0.92f);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgColor);
            Color borderGlow = Color.CornflowerBlue * (tooltipAlpha * 0.6f * openProg);
            DrawFancyBorder(spriteBatch, panelRect, borderGlow, tooltipAlpha);
            Vector2 titlePos = basePos + new Vector2(10, 8);
            Color titleGlow = Color.Gold * (tooltipAlpha * 0.55f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4;
                Vector2 offset = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow * 0.6f, 0.85f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * tooltipAlpha, 0.85f);

            // 已死机标签（右上角）
            if (eye.IsCrashed) {
                string crashed = "已死机";
                Vector2 crashSize = FontAssets.MouseText.Value.MeasureString(crashed) * 0.6f;
                Vector2 crashPos = new(panelRect.Right - 10 - crashSize.X, titlePos.Y + 2);
                // 发光底
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Vector2 off = ang.ToRotationVector2() * 1.1f;
                    Utils.DrawBorderString(spriteBatch, crashed, crashPos + off, new Color(255, 60, 60) * tooltipAlpha * 0.5f, 0.6f);
                }
                Utils.DrawBorderString(spriteBatch, crashed, crashPos, new Color(255, 120, 120) * tooltipAlpha, 0.6f);
            }

            Vector2 dividerStart = titlePos + new Vector2(0, 24);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - 20, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, Color.Gold * tooltipAlpha * 0.8f, Color.Gold * tooltipAlpha * 0.1f, 1.2f);
            Vector2 textPos = dividerStart + new Vector2(0, 8);
            int wrapWidth = (int)panelSize.X - 20;
            string[] lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, wrapWidth + 40, 20, out int _);
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                string line = lines[i].TrimEnd('-', ' ');
                Vector2 lp = textPos + new Vector2(4, drawn * 18);
                if (lp.Y + 16 > panelRect.Bottom - 8) {
                    break;
                }
                Utils.DrawBorderString(spriteBatch, line, lp + new Vector2(1, 1), Color.Black * tooltipAlpha * 0.5f, 0.7f);
                Utils.DrawBorderString(spriteBatch, line, lp, Color.White * tooltipAlpha, 0.7f);
                drawn++;
            }
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 14, panelRect.Y + 12);
            float s1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star1, 4f, Color.Gold * s1Alpha);
            Vector2 star2 = new Vector2(panelRect.Right - 20, panelRect.Bottom - 16);
            float s2Alpha = ((float)Math.Sin(starTime + MathHelper.Pi) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star2, 3f, Color.Gold * s2Alpha);
        }

        private static string ChineseNumeral(int i) {
            return i switch {
                1 => "一",
                2 => "二",
                3 => "三",
                4 => "四",
                5 => "五",
                6 => "六",
                7 => "七",
                8 => "八",
                9 => "九",
                10 => "十",
                _ => i.ToString()
            };
        }

        private void DrawFancyBorder(SpriteBatch spriteBatch, Rectangle rect, Color glow, float alpha) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle top = new Rectangle(rect.X, rect.Y, rect.Width, 1);
            Rectangle bottom = new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1);
            Rectangle left = new Rectangle(rect.X, rect.Y, 1, rect.Height);
            Rectangle right = new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height);
            spriteBatch.Draw(pixel, top, new Rectangle(0, 0, 1, 1), glow);
            spriteBatch.Draw(pixel, bottom, new Rectangle(0, 0, 1, 1), glow * 0.8f);
            spriteBatch.Draw(pixel, left, new Rectangle(0, 0, 1, 1), glow * 0.9f);
            spriteBatch.Draw(pixel, right, new Rectangle(0, 0, 1, 1), glow * 0.9f);
            Color corner = Color.White * (alpha * 0.6f);
            DrawCorner(spriteBatch, new Vector2(rect.Left, rect.Top), corner, 0f);
            DrawCorner(spriteBatch, new Vector2(rect.Right, rect.Top), corner, MathHelper.PiOver2);
            DrawCorner(spriteBatch, new Vector2(rect.Right, rect.Bottom), corner, MathHelper.Pi);
            DrawCorner(spriteBatch, new Vector2(rect.Left, rect.Bottom), corner, MathHelper.Pi + MathHelper.PiOver2);
        }

        private void DrawCorner(SpriteBatch spriteBatch, Vector2 pos, Color color, float rot) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < 3; i++) {
                float len = 6 - i * 2;
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * (0.9f - i * 0.3f), rot, new Vector2(0, 0.5f), new Vector2(len, 1f), SpriteEffects.None, 0f);
            }
        }

        private void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private void DrawStar(SpriteBatch spriteBatch, Vector2 position, float size, Color color) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
        }
    }

    internal class SeaEyeButton
    {
        public int Index;
        public float Angle;
        public bool IsActive;
        public Vector2 Position;
        public bool IsHovered;
        public int? LayerNumber; //激活层数（激活顺序）
        public int LayerNumberDisplay {
            get {
                int num;
                if (IsActive) {
                    num = LayerNumber ?? 1;
                }
                else {
                    num = DomainUI.Instance.ActiveEyeCount + 1;
                }
                return num;
            }
        }

        private float hoverScale = 1f;
        private float glowIntensity = 0f;
        private float blinkTimer = 0f;
        private const float EyeSize = 20f;

        /// <summary>
        /// 判断当前眼睛是否处于死机状态
        /// </summary>
        public bool IsCrashed {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }
                int crashLevel = halibutPlayer.CrashesLevel();
                return LayerNumberDisplay <= crashLevel;
            }
        }

        public SeaEyeButton(int index, float angle) {
            Index = index;
            Angle = angle;
            IsActive = false;
            LayerNumber = null;
        }

        public void Toggle() {
            IsActive = !IsActive;
            blinkTimer = 15f;
            if (!IsActive) {
                LayerNumber = null;
            }
            PlayEyeDeactivationSound(this);
        }

        /// <summary>
        /// 播放眼睛关闭音效
        /// </summary>
        private static void PlayEyeDeactivationSound(SeaEyeButton eye) {
            bool wasCrashed = eye.IsCrashed;

            if (!wasCrashed) {
                //机械停止音
                SoundEngine.PlaySound(SoundID.Item1 with { //金属音
                    Volume = 0.4f,
                    Pitch = -0.6f,
                    MaxInstances = 3
                });

                //压力释放
                SoundEngine.PlaySound(SoundID.Item54 with { //柔和破裂音
                    Volume = 0.3f,
                    Pitch = 0.1f,
                    MaxInstances = 2
                });
            }
            else {
                //能量消散音
                SoundEngine.PlaySound(SoundID.Item30 with {
                    Volume = 0.35f,
                    Pitch = -0.2f,
                    MaxInstances = 3
                });

                //轻柔的气息音
                SoundEngine.PlaySound(SoundID.Item85 with { //魔法消散音
                    Volume = 0.3f,
                    Pitch = -0.3f,
                    MaxInstances = 2
                });

                //封印音（淡出感）
                SoundEngine.PlaySound(SoundID.Item20 with { //钩爪收回音
                    Volume = 0.25f,
                    Pitch = -0.5f,
                    MaxInstances = 2
                });
            }

            // 通用：轻微的回响消失
            SoundEngine.PlaySound(SoundID.MenuClose with {
                Volume = 0.2f,
                Pitch = -0.4f,
                MaxInstances = 5
            });
        }

        public void Update(Vector2 center, float orbitRadius, float panelAlpha) {
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + Index * 0.3f) * 2f;
            Position = center + Angle.ToRotationVector2() * (orbitRadius + wave);
            Rectangle hitbox = new Rectangle((int)(Position.X - EyeSize / 2), (int)(Position.Y - EyeSize / 2), (int)EyeSize, (int)EyeSize);
            IsHovered = hitbox.Contains(Main.MouseScreen.ToPoint()) && panelAlpha >= 1f;
            float targetScale = IsHovered ? 1.2f : 1f;
            hoverScale = MathHelper.Lerp(hoverScale, targetScale, 0.15f);
            float targetGlow = IsActive ? 1f : 0.3f;
            if (IsHovered) {
                targetGlow += 0.3f;
            }
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.1f);
            if (blinkTimer > 0f) {
                blinkTimer--;
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (SeaEye == null) {
                return;
            }

            int frameHeight = SeaEye.Height / 4; //现在是4帧
            bool shouldBlink = blinkTimer > 0f && blinkTimer % 10 < 5;

            //判断使用哪组帧
            bool isCrashed = IsCrashed;
            int baseFrame = isCrashed ? 2 : 0; //死机使用帧2-3，正常使用帧0-1

            //选择睁眼或闭眼帧
            int frame = (IsActive && !shouldBlink) ? (baseFrame + 1) : baseFrame;

            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, SeaEye.Width, frameHeight);
            Vector2 drawPos = Position;
            Vector2 origin = new Vector2(SeaEye.Width / 2, frameHeight / 2);
            float scale = (EyeSize / SeaEye.Width) * hoverScale;

            if (IsActive) {
                //死机状态使用不同的发光颜色
                Color glowColor = isCrashed
                    ? new Color(255, 80, 80) * (alpha * glowIntensity * 0.5f) //红色发光
                    : new Color(100, 220, 255) * (alpha * glowIntensity * 0.5f); //正常蓝色发光

                float glowPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + Index) * 0.3f + 0.7f;
                spriteBatch.Draw(SeaEye, drawPos, sourceRect, glowColor * glowPulse, 0f, origin, scale * 1.25f, SpriteEffects.None, 0f);
            }

            //死机状态的眼睛颜色偏暗红
            Color eyeColor = IsActive ? Color.White : new Color(100, 100, 120);
            if (isCrashed && IsActive) {
                eyeColor = Color.Lerp(eyeColor, new Color(255, 100, 100), 0.5f); //混合红色
            }

            eyeColor *= alpha * glowIntensity;
            spriteBatch.Draw(SeaEye, drawPos, sourceRect, eyeColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }

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
        public DomainRing(Vector2 center, float radius, int index) {
            Center = center;
            TargetRadius = radius;
            CurrentRadius = 0f;
            LayerIndex = index;
            Alpha = 0f;
            ShouldRemove = false;
        }
        public void Update() {
            if (spawnProgress < 1f) {
                spawnProgress += 1f / SpawnDuration;
                spawnProgress = Math.Clamp(spawnProgress, 0f, 1f);
                float easedProgress = EaseOutCubic(spawnProgress);
                CurrentRadius = TargetRadius * easedProgress;
                Alpha = easedProgress;
            }
            else {
                CurrentRadius = TargetRadius;
                Alpha = 1f;
            }
            rotation += 0.005f * (1f + LayerIndex * 0.1f);
        }
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3);
        }
        public void Draw(SpriteBatch spriteBatch, float panelAlpha) {
            if (Alpha < 0.01f) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 48;
            float angleStep = MathHelper.TwoPi / segments;
            float colorProgress = LayerIndex / 9f;
            Color baseColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255), colorProgress);
            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep + rotation;
                float angle2 = (i + 1) * angleStep + rotation;
                float wave1 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle1 * 2f) * 2f;
                float wave2 = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle2 * 2f) * 2f;
                Vector2 p1 = Center + angle1.ToRotationVector2() * (CurrentRadius + wave1);
                Vector2 p2 = Center + angle2.ToRotationVector2() * (CurrentRadius + wave2);
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float segRotation = diff.ToRotation();
                float brightness = 0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + angle1 * 3f) * 0.3f;
                Color segColor = baseColor * brightness * Alpha * panelAlpha * 0.6f;
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), segColor, segRotation, Vector2.Zero, new Vector2(length, 1.5f), SpriteEffects.None, 0f);
            }
        }
    }

    internal class EyeParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;
        public float Rotation;
        public Color Color;
        public EyeParticle(Vector2 pos, Vector2 vel, Color color) {
            Position = pos;
            Velocity = vel;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(30f, 50f);
            Scale = Main.rand.NextFloat(0.5f, 1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Color = color;
        }
        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Rotation += 0.05f;
        }
        public void Draw(SpriteBatch spriteBatch, float panelAlpha) {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * panelAlpha * 0.7f;
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Color drawColor = Color * alpha;
            spriteBatch.Draw(tex, Position, null, drawColor, Rotation, tex.Size() / 2, Scale * (0.3f + progress * 0.2f), SpriteEffects.None, 0f);
        }
    }

    internal class EyeActivationAnimation
    {
        private Vector2 startPos;
        private float progress;
        private const float Duration = 25f;
        private int eyeLayerNumber; //记录这个眼睛的层数

        public bool Finished { get; private set; }

        public EyeActivationAnimation(Vector2 start, int layerNumber = 1) {
            startPos = start;
            progress = 0f;
            Finished = false;
            eyeLayerNumber = layerNumber;
        }

        public void Update(Vector2 target) {
            if (Finished) {
                return;
            }
            progress += 1f / Duration;
            progress = Math.Clamp(progress, 0f, 1f);
            if (progress >= 1f) {
                Finished = true;
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (Finished) {
                return;
            }
            if (SeaEye == null) {
                return;
            }

            Texture2D tex = SeaEye;
            int frameHeight = tex.Height / 4; //现在是4帧

            //判断是否死机
            bool isCrashed = false;
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                int crashLevel = halibutPlayer.CrashesLevel();
                isCrashed = eyeLayerNumber <= crashLevel;
            }

            //使用对应的睁眼帧
            int baseFrame = isCrashed ? 2 : 0;
            int frame = baseFrame + 1; //使用睁眼帧

            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, tex.Width, frameHeight);
            Vector2 center = DomainUI.Instance.halibutCenter;
            Vector2 pos = Vector2.Lerp(startPos, center, EaseOut(progress));
            float scale = MathHelper.Lerp(0.8f, 1.8f, EaseOutBack(progress));
            float fade = 1f - Math.Abs(progress - 0.5f) * 2f;
            Color color = Color.White * (alpha * fade);
            Vector2 origin = new Vector2(tex.Width / 2, frameHeight / 2);
            spriteBatch.Draw(tex, pos, sourceRect, color, 0f, origin, scale * 0.4f, SpriteEffects.None, 0f);

            //根据死机状态使用不同的光环颜色
            Color ringColor = isCrashed
                ? new Color(255, 100, 100) * (alpha * fade * 0.5f) //红色光环
                : new Color(120, 220, 255) * (alpha * fade * 0.5f); //蓝色光环

            DrawPulseRing(spriteBatch, pos, scale * 22f, ringColor, 2f);
        }

        private static float EaseOut(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        private void DrawPulseRing(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float thickness) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segs = 40;
            float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) {
                float a1 = i * step;
                float a2 = (i + 1) * step;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), color, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
            }
        }
    }

    internal class HalibutPulseEffect
    {
        private float progress;
        private const float Duration = 35f;
        private Vector2 center;
        public bool Finished => progress >= 1f;
        public HalibutPulseEffect(Vector2 c) {
            center = c;
            progress = 0f;
        }
        public void Update() {
            if (Finished) {
                return;
            }
            progress += 1f / Duration;
            progress = Math.Clamp(progress, 0f, 1f);
        }
        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (Finished) {
                return;
            }
            float eased = 1f - (float)Math.Pow(1f - progress, 3f);
            float radius = MathHelper.Lerp(18f, 80f, eased);
            float fade = 1f - eased;
            Color col = new Color(110, 210, 255) * (alpha * fade * 0.6f);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segs = 64;
            float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) {
                float a1 = i * step;
                float a2 = (i + 1) * step;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, 2f), SpriteEffects.None, 0f);
            }
        }
    }

    internal static class DomainEyeDescriptions
    {
        private static readonly Dictionary<int, string> descriptions = new Dictionary<int, string> {
            { 1, "初启领域之眼，微弱的潮汐感开始共鸣" },
            { 2, "双目同开，水压在周遭缓慢聚集，力量渐显" },
            { 3, "三重视界锁定海流，领域开始稳定成型" },
            { 4, "第四层共鸣放大，涌动的寒意悄然扩散" },
            { 5, "五层交织，环形水旋于脚下成形，给予守护" },
            { 6, "第六层脉冲涌现，能量脉络变得清晰可辨" },
            { 7, "七眼同辉，潮域对外界的侵蚀性显著增强" },
            { 8, "第八层使水压几近凝实，力量几乎到达巅峰" },
            { 9, "九层极境——海渊之形完全显现，伟力贯通" },
            { 10, "十层神之境界" }
        };
        public static string GetDescription(int layer) {
            if (descriptions.TryGetValue(layer, out string value)) {
                return value;
            }
            return "Error";
        }
    }
}
