using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    ///<summary>
    ///领域控制面板
    ///</summary>
    internal class DomainUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";
        public static LocalizedText TitleText;
        public static LocalizedText ExtraEyeTitleText;
        public static LocalizedText CrashedLabelText;
        internal static LocalizedText[] EyeLayerDescriptions = new LocalizedText[11];//1-10
        public static LocalizedText LayerTitleFormat;

        private readonly List<ExtraTooltipParticle> extraEyeTooltipParticles = [];
        private int extraEyeTooltipSpawnTimer = 0;
        private float extraEyeTooltipPulse = 0f;
        private float extraEyeTooltipRotation = 0f;
        private float extraEyeTooltipShock = 0f;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "海域领域");
            ExtraEyeTitleText = this.GetLocalization(nameof(ExtraEyeTitleText), () => "第 十 层");
            CrashedLabelText = this.GetLocalization(nameof(CrashedLabelText), () => "已死机");
            EyeLayerDescriptions[1] = this.GetLocalization("EyeDesc1", () => "初启领域之眼，微弱的潮汐感开始共鸣");
            EyeLayerDescriptions[2] = this.GetLocalization("EyeDesc2", () => "双目同开，水压在周遭缓慢聚集，力量渐显");
            EyeLayerDescriptions[3] = this.GetLocalization("EyeDesc3", () => "三重视界锁定海流，领域开始稳定成型");
            EyeLayerDescriptions[4] = this.GetLocalization("EyeDesc4", () => "第四层共鸣放大，涌动的寒意悄然扩散");
            EyeLayerDescriptions[5] = this.GetLocalization("EyeDesc5", () => "五层交织，环形水旋于脚下成形，给予守护");
            EyeLayerDescriptions[6] = this.GetLocalization("EyeDesc6", () => "第六层脉冲涌现，能量脉络变得清晰可辨");
            EyeLayerDescriptions[7] = this.GetLocalization("EyeDesc7", () => "七眼同辉，潮域对外界的侵蚀性显著增强");
            EyeLayerDescriptions[8] = this.GetLocalization("EyeDesc8", () => "第八层使水压几近凝实，力量几乎到达巅峰");
            EyeLayerDescriptions[9] = this.GetLocalization("EyeDesc9", () => "九层极境，海渊之形完全显现，伟力贯通");
            EyeLayerDescriptions[10] = this.GetLocalization("EyeDesc10", () => "十层无限叠加，神之境界");
            LayerTitleFormat = this.GetLocalization(nameof(LayerTitleFormat), () => "第 {0} 层");
        }

        public static DomainUI Instance => UIHandleLoader.GetUIHandleOfType<DomainUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//手动调用

        //展开控制
        private float expandProgress = 0f;//展开进度（0-1）
        private const float ExpandDuration = 10f;//展开动画持续帧数

        //面板尺寸（使用TooltipPanel的大小）
        private float PanelWidth => TooltipPanel.Width;//214
        private float PanelHeight => TooltipPanel.Height;//206

        //位置相关
        private Vector2 anchorPosition;//锚点位置（动态计算，跟随SkillTooltipPanel）
        private float currentWidth = 0f;//当前宽度（用于从右到左展开动画）
        private float targetWidth = 0f;//目标宽度
        private const float MinWidth = 8f;//最小宽度（完全收起时）

        //九只奈落之眼 + 额外中心第十眼
        internal List<SeaEyeButton> eyes => player.GetModPlayer<HalibutSave>().eyes;
        internal List<SeaEyeButton> activationSequence => player.GetModPlayer<HalibutSave>().activationSequence;
        internal const int MaxEyes = 9;//外圈仍然是9
        internal const float EyeOrbitRadius = 75f;//眼睛轨道半径
        private ExtraSeaEyeButton extraEye = new();//第十只中心额外之眼

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
        private bool extraEyeHovered = false;

        //内容淡入进度
        private float contentFadeProgress = 0f;
        private const float ContentFadeDelay = 0.4f;//内容在展开40%后开始淡入

        //激活动画（眼睛飞向中心并放大）
        private readonly List<EyeActivationAnimation> activationAnimations = [];

        //复苏增长相关常量
        private const float BaseResurrectionRatePerEye = 0.02f;//单层基础复苏速度
        private const float GeometricFactor = 1.2f;//几何倍率（每更高一层的额外提高倍率）
        private const float CrashedEyeSideEffectRate = 0.0001f;//死机眼睛的极小副作用

        ///<summary>
        ///获取当前激活的眼睛数量（即领域层数）
        ///</summary>
        public int ActiveEyeCount {
            get {
                int baseCount = 0;
                if (activationSequence != null) {
                    foreach (var eye in activationSequence) {
                        if (eye.IsActive) {
                            baseCount++;
                        }
                    }
                }
                if (extraEye != null && extraEye.IsActive) {
                    baseCount++;
                }
                return baseCount;
            }
        }

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

        ///<summary>
        ///纯逻辑更新 (由系统层调用)
        ///</summary>
        internal void LogicUpdate() {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            halibutPlayer.SeaDomainLayers = ActiveEyeCount;//同步层数
            UpdateResurrectionRate();//更新复苏速度
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
                anchorPosition = baseAnchor + new Vector2(skillPanelWidth - 10, 0);//-10重叠
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
            DrawPosition = anchorPosition + new Vector2(-6, -PanelHeight / 2 - 18);
            Size = new Vector2(currentWidth, PanelHeight);

            if (expandProgress < 0.01f) {
                return;//完全收起时不更新
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
                }
            }

            //第十眼出现条件：外圈9全部激活 且 TheOnlyBornOfAnEra 条件满足
            bool canShowExtra = false;
            if (activationSequence.Count >= 9) {
                if (HalibutPlayer.TheOnlyBornOfAnEra()) {
                    canShowExtra = true;
                }
            }

            //如果外圈未满足，强制关闭
            if (!canShowExtra) {
                extraEye.ForceClose();
            }

            extraEye.Update(halibutCenter, canShowExtra, easedProgress);
            extraEyeHovered = extraEye.IsHovered && hoveringPanel;
            if (extraEyeHovered && Main.mouseLeft && Main.mouseLeftRelease && canShowExtra) {
                extraEye.Toggle();
                SoundEngine.PlaySound(SoundID.MenuTick);
                if (extraEye.IsActive) {
                    halibutPulses.Add(new HalibutPulseEffect(halibutCenter));
                }
            }

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

            if (extraEye.IsActive) {
                bool crashed = 10 <= crashLevel;
                if (crashed) {
                    rate += CrashedEyeSideEffectRate;
                }
                else {
                    rate += BaseResurrectionRatePerEye * MathF.Pow(GeometricFactor, 9);
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

            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            if (halibutPlayer.SeaDomainActive) {
                halibutPlayer.SeaDomainLayers = ActiveEyeCount;//同步层数
                SeaDomain.Deactivate(player);
                halibutPlayer.OnStartSeaDomain = true;
            }
            if (halibutPlayer.CloneFishActive) {
                halibutPlayer.CloneCount = halibutPlayer.SeaDomainLayers;//同步数量
                CloneFish.Deactivate(player);
                halibutPlayer.OnStartClone = true;
            }
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
                    color = new Color(255, 100, 100);//红色粒子
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
            DrawExtraEye(spriteBatch, alpha);
            foreach (var anim in activationAnimations) {
                anim.Draw(spriteBatch, alpha);
            }
            if (expandProgress > 0.8f) {
                DrawTitle(spriteBatch, alpha);
            }
            if (hoveredEye != null && expandProgress >= 0.4f) {
                DrawEyeTooltip(spriteBatch, hoveredEye, alpha);
            }
            else if (extraEyeHovered && expandProgress >= 0.4f) {
                DrawExtraEyeTooltip(spriteBatch, alpha);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            //计算源矩形（从左到右展开的裁剪效果，与SkillTooltipPanel一致）
            float revealProgress = expandProgress;
            int revealWidth = (int)(PanelWidth * revealProgress);

            Rectangle sourceRect = new Rectangle(
                0,//从左侧开始显示
                0,
                revealWidth,
                (int)PanelHeight
            );

            Rectangle destRect = new Rectangle(
                (int)DrawPosition.X,//从左侧对齐
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
            if (extraEye.IsActive) {
                Vector2 start = halibutCenter + new Vector2(0, -HalibutSize * 0.2f);
                Vector2 end = halibutCenter;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                Color lineColor = Color.Lerp(new Color(150, 200, 255), new Color(220, 240, 255), (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f);
                lineColor *= alpha * 0.4f;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor, rotation, Vector2.Zero, new Vector2(length, 2.2f), SpriteEffects.None, 0f);
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

        private void DrawExtraEye(SpriteBatch spriteBatch, float alpha) {
            if (extraEye == null) {
                return;
            }
            bool canShow = activationSequence.Count >= 9 && HalibutPlayer.TheOnlyBornOfAnEra();
            if (!canShow) {
                return;
            }
            extraEye.Draw(spriteBatch, halibutCenter, alpha);
        }

        private void DrawTitle(SpriteBatch spriteBatch, float alpha) {
            if (contentFadeProgress < 0.5f) {
                return;
            }
            float titleAlpha = contentFadeProgress * alpha;
            string title = TitleText.Value;
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

        public static string GetDescription(int layer) {
            if (layer >= 1 && layer < EyeLayerDescriptions.Length) {
                var lt = EyeLayerDescriptions[layer];
                if (lt != null) {
                    string value = lt.Value;
                    value = value.Replace("[Halibut_Domain]", CWRKeySystem.Halibut_Domain.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Restart]", CWRKeySystem.Halibut_Restart.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Clone]", CWRKeySystem.Halibut_Clone.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Superposition]", CWRKeySystem.Halibut_Superposition.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Teleport]", CWRKeySystem.Halibut_Teleport.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Line]", "______________");
                    return value;
                }
            }
            return "Error";
        }

        private void DrawEyeTooltip(SpriteBatch spriteBatch, SeaEyeButton eye, float alpha) {
            //动态尺寸计算，参考 ResurrectionUI 的方式
            int displayLayer = eye.LayerNumberDisplay;
            string title = string.Format(LayerTitleFormat.Value, GetLayerNumeralText(displayLayer));
            string desc = GetDescription(displayLayer);
            float tooltipAlpha = alpha * 0.95f;

            //布局常量
            float minWidth = 250f;
            float maxWidth = 360f;
            float horizontalPadding = 12f; //左侧内边距
            float rightPadding = 14f;       //右侧内边距
            float topPadding = 8f;
            float bottomPadding = 12f;
            float titleExtra = 6f;          //标题下额外空隙
            float dividerSpacing = 6f;      //标题与分割线之间
            float textSpacingTop = 8f;      //正文上间距
            float lineHeight = 18f;         //正文行高

            //初始工作宽度
            float workingWidth = minWidth;
            float contentWidth = workingWidth - horizontalPadding - rightPadding; //有效文字宽度

            //先按最小宽度做一次换行
            string[] lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out int _);

            //测量最长行宽
            float longest = 0f;
            foreach (string l in lines) {
                if (string.IsNullOrWhiteSpace(l)) {
                    continue;
                }
                float w = FontAssets.MouseText.Value.MeasureString(l.TrimEnd('-', ' ')).X;
                if (w > longest) {
                    longest = w;
                }
            }

            //考虑标题和死机标签宽度
            float titleWidth = FontAssets.MouseText.Value.MeasureString(title).X;
            if (eye.IsCrashed) {
                string crashed = CrashedLabelText.Value;
                float crashWidth = FontAssets.MouseText.Value.MeasureString(crashed).X * 0.6f + 26f; //标签宽度预留
                titleWidth += crashWidth;
            }
            longest = Math.Max(longest, titleWidth);

            if (longest > contentWidth) {
                workingWidth = Math.Clamp(longest + horizontalPadding + rightPadding, minWidth, maxWidth);
                contentWidth = workingWidth - horizontalPadding - rightPadding;
                lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out _);
            }

            //统计有效行数
            int drawLines = 0;
            foreach (var l in lines) {
                if (!string.IsNullOrWhiteSpace(l)) {
                    drawLines++;
                }
            }

            //标题高度 (缩放0.85f后的实际高度)
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.85f;
            float dividerHeight = 1.2f;

            //面板高度计算
            float panelHeight = topPadding
                + titleHeight + titleExtra
                + dividerSpacing + dividerHeight
                + textSpacingTop + drawLines * lineHeight
                + bottomPadding;

            //屏幕限制
            float screenHeightLimit = Main.screenHeight - 40f;
            if (panelHeight > screenHeightLimit) {
                panelHeight = screenHeightLimit;
            }

            Vector2 panelSize = new Vector2(workingWidth, panelHeight);

            //定位（鼠标上方）
            Vector2 basePos = MousePosition + new Vector2(18, -panelSize.Y - 8);
            if (basePos.X + panelSize.X > Main.screenWidth - 20) {
                basePos.X = Main.screenWidth - panelSize.X - 20;
            }
            if (basePos.Y < 20) {
                basePos.Y = 20;
            }

            Rectangle panelRect = new Rectangle((int)basePos.X, (int)basePos.Y, (int)panelSize.X, (int)panelSize.Y);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            //阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (tooltipAlpha * 0.5f));

            float openProg = Math.Min(1f, contentFadeProgress * 1.3f);
            Color bgColor = new Color(25, 35, 55) * (tooltipAlpha * 0.92f);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgColor);

            Color borderGlow = Color.CornflowerBlue * (tooltipAlpha * 0.6f * openProg);
            DrawFancyBorder(spriteBatch, panelRect, borderGlow, tooltipAlpha);

            //标题
            Vector2 titlePos = new Vector2(panelRect.X + horizontalPadding, panelRect.Y + topPadding);
            Color titleGlow = Color.Gold * (tooltipAlpha * 0.55f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4;
                Vector2 offset = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow * 0.6f, 0.85f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * tooltipAlpha, 0.85f);

            //死机标签
            if (eye.IsCrashed) {
                string crashed = CrashedLabelText.Value;
                Vector2 crashSize = FontAssets.MouseText.Value.MeasureString(crashed) * 0.6f;
                Vector2 crashPos = new(panelRect.Right - rightPadding - crashSize.X, titlePos.Y + 2);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Vector2 off = ang.ToRotationVector2() * 1.1f;
                    Utils.DrawBorderString(spriteBatch, crashed, crashPos + off, new Color(255, 60, 60) * tooltipAlpha * 0.5f, 0.6f);
                }
                Utils.DrawBorderString(spriteBatch, crashed, crashPos, new Color(255, 120, 120) * tooltipAlpha, 0.6f);
            }

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + titleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - horizontalPadding - rightPadding, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, Color.Gold * tooltipAlpha * 0.8f, Color.Gold * tooltipAlpha * 0.1f, 1.2f);

            //正文
            Vector2 textStart = dividerStart + new Vector2(0, dividerSpacing + textSpacingTop);
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                string line = lines[i].TrimEnd('-', ' ');
                Vector2 lp = textStart + new Vector2(2, drawn * lineHeight);
                if (lp.Y + (lineHeight - 2f) > panelRect.Bottom - bottomPadding) {
                    break; //高度溢出
                }
                Utils.DrawBorderString(spriteBatch, line, lp + new Vector2(1, 1), Color.Black * tooltipAlpha * 0.5f, 0.7f);
                Utils.DrawBorderString(spriteBatch, line, lp, Color.White * tooltipAlpha, 0.7f);
                drawn++;
            }

            //装饰星星
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 14, panelRect.Y + 12);
            float s1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star1, 4f, Color.Gold * s1Alpha);
            Vector2 star2 = new Vector2(panelRect.Right - 20, panelRect.Bottom - 16);
            float s2Alpha = ((float)Math.Sin(starTime + MathHelper.Pi) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star2, 3f, Color.Gold * s2Alpha);
        }

        private void DrawExtraEyeTooltip(SpriteBatch spriteBatch, float alpha) {
            //动态尺寸 + 炫酷特效
            string title = ExtraEyeTitleText.Value;
            string desc = GetDescription(10);
            float tooltipAlpha = alpha * 0.98f;

            //布局常量
            float minWidth = 260f;
            float maxWidth = 420f;
            float horizontalPadding = 16f;
            float rightPadding = 18f;
            float topPadding = 12f;
            float bottomPadding = 16f;
            float titleExtra = 8f;
            float dividerSpacing = 8f;
            float textSpacingTop = 10f;
            float lineHeight = 18f;
            float titleScale = 0.95f;

            float workingWidth = minWidth;
            float contentWidth = workingWidth - horizontalPadding - rightPadding;

            string[] lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out _);

            float longest = 0f;
            foreach (var l in lines) {
                if (string.IsNullOrWhiteSpace(l)) continue;
                float w = FontAssets.MouseText.Value.MeasureString(l.TrimEnd('-', ' ')).X;
                if (w > longest) longest = w;
            }
            float titleWidth = FontAssets.MouseText.Value.MeasureString(title).X * titleScale;
            longest = Math.Max(longest, titleWidth);
            if (longest > contentWidth) {
                workingWidth = Math.Clamp(longest + horizontalPadding + rightPadding, minWidth, maxWidth);
                contentWidth = workingWidth - horizontalPadding - rightPadding;
                lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out _);
            }
            int drawLines = 0; foreach (var l in lines) if (!string.IsNullOrWhiteSpace(l)) drawLines++;
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * titleScale;
            float dividerHeight = 1.4f;
            float panelHeight = topPadding + titleHeight + titleExtra + dividerSpacing + dividerHeight + textSpacingTop + drawLines * lineHeight + bottomPadding;
            float screenLimit = Main.screenHeight - 40f; if (panelHeight > screenLimit) panelHeight = screenLimit;
            Vector2 panelSize = new Vector2(workingWidth, panelHeight);

            //位置
            Vector2 basePos = MousePosition + new Vector2(22, -panelSize.Y - 14);
            if (basePos.X + panelSize.X > Main.screenWidth - 16) basePos.X = Main.screenWidth - panelSize.X - 16;
            if (basePos.Y < 16) basePos.Y = 16;
            Rectangle panelRect = new Rectangle((int)basePos.X, (int)basePos.Y, (int)panelSize.X, (int)panelSize.Y);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            //状态更新
            extraEyeTooltipPulse += 0.04f;
            extraEyeTooltipRotation += 0.01f;
            if (extraEyeTooltipShock < 1f) {
                extraEyeTooltipShock += 0.08f;
            }
            float shockEase = 1f - (float)Math.Pow(1f - extraEyeTooltipShock, 3);

            //生成粒子
            extraEyeTooltipSpawnTimer++;
            int targetParticles = 42;
            if (extraEyeTooltipParticles.Count < targetParticles && extraEyeTooltipSpawnTimer % 2 == 0) {
                var center = panelRect.Center.ToVector2();
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float rad = Main.rand.NextFloat(12f, panelRect.Width * 0.45f);
                extraEyeTooltipParticles.Add(new ExtraTooltipParticle(center, ang, rad));
            }
            //更新粒子
            for (int i = extraEyeTooltipParticles.Count - 1; i >= 0; i--) {
                extraEyeTooltipParticles[i].Update();
                if (extraEyeTooltipParticles[i].Dead) extraEyeTooltipParticles.RemoveAt(i);
            }

            //阴影
            Rectangle shadowRect = panelRect; shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (tooltipAlpha * 0.55f));

            //背景渐变 (两层脉动)
            Color bgA = new Color(20, 32, 58) * (tooltipAlpha * 0.95f);
            Color bgB = new Color(40, 64, 96) * (tooltipAlpha * 0.25f * (0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f)));
            Color bgColor = new Color(
                (byte)Math.Clamp(bgA.R + bgB.R, 0, 255),
                (byte)Math.Clamp(bgA.G + bgB.G, 0, 255),
                (byte)Math.Clamp(bgA.B + bgB.B, 0, 255),
                (byte)Math.Clamp(bgA.A + bgB.A, 0, 255)
            );
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgColor);

            //内部径向辉光 (pulse)
            float radial = (float)Math.Sin(extraEyeTooltipPulse * 2.2f) * 0.5f + 0.5f;
            Rectangle innerGlow = panelRect; innerGlow.Inflate(-4, -4);
            spriteBatch.Draw(pixel, innerGlow, new Rectangle(0, 0, 1, 1), new Color(80, 140, 220) * (tooltipAlpha * 0.07f * radial));

            //外圈脉冲环 (两层旋转渐隐线条)
            float ringAlpha = 0.18f * tooltipAlpha;
            DrawRotatingRing(spriteBatch, panelRect, 0.92f + 0.02f * (float)Math.Sin(extraEyeTooltipPulse * 3f), ringAlpha, 34, 2f, 0.8f, 0f);
            DrawRotatingRing(spriteBatch, panelRect, 0.88f, ringAlpha * 0.8f, 20, 3f, 1.2f, extraEyeTooltipRotation * 1.6f);

            //边框 (动态颜色)
            float edgeWave = (float)Math.Sin(extraEyeTooltipPulse * 2.5f) * 0.5f + 0.5f;
            Color borderGlow = Color.Lerp(new Color(120, 200, 255), new Color(255, 210, 90), edgeWave) * (tooltipAlpha * 0.7f);
            DrawFancyBorder(spriteBatch, panelRect, borderGlow, tooltipAlpha);

            //标题
            Vector2 titlePos = new Vector2(panelRect.X + horizontalPadding, panelRect.Y + topPadding);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4;
                Vector2 offset = ang.ToRotationVector2() * (1.8f + 0.6f * shockEase);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, borderGlow * 0.55f, titleScale);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * tooltipAlpha, titleScale);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + titleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - horizontalPadding - rightPadding, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, borderGlow * 0.85f, borderGlow * 0.08f, 1.4f);

            //正文
            Vector2 textStart = dividerStart + new Vector2(0, dividerSpacing + textSpacingTop);
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string line = lines[i].TrimEnd('-', ' ');
                Vector2 lp = textStart + new Vector2(2, drawn * lineHeight);
                if (lp.Y + (lineHeight - 2f) > panelRect.Bottom - bottomPadding) break;
                Utils.DrawBorderString(spriteBatch, line, lp + new Vector2(1, 1), Color.Black * tooltipAlpha * 0.5f, 0.75f);
                Utils.DrawBorderString(spriteBatch, line, lp, new Color(235, 245, 255) * tooltipAlpha, 0.75f);
                drawn++;
            }

            //粒子绘制（在内容之上，边框之下）
            foreach (var p in extraEyeTooltipParticles) {
                p.Draw(spriteBatch, tooltipAlpha);
            }

            //角落星芒
            float starTime = Main.GlobalTimeWrappedHourly * 4f;
            Vector2 star1 = new Vector2(panelRect.Right - 18, panelRect.Y + 14);
            Vector2 star2 = new Vector2(panelRect.Left + 18, panelRect.Bottom - 16);
            float s1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * tooltipAlpha;
            float s2Alpha = ((float)Math.Sin(starTime + MathHelper.PiOver2) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star1, 5f, borderGlow * s1Alpha);
            DrawStar(spriteBatch, star2, 4f, borderGlow * s2Alpha);

            //中心能量脉冲 (轻微)
            float centerPulse = (float)Math.Sin(extraEyeTooltipPulse * 3.2f) * 0.5f + 0.5f;
            Rectangle core = panelRect;
            int shrinkX = (int)(panelRect.Width * 0.45f);
            int shrinkY = (int)(panelRect.Height * 0.55f);
            core.Inflate(-shrinkX, -shrinkY);
            if (core.Width > 2 && core.Height > 2) {
                spriteBatch.Draw(pixel, core, new Rectangle(0, 0, 1, 1), new Color(120, 210, 255) * (tooltipAlpha * 0.07f * centerPulse));
            }
        }

        private static void DrawRotatingRing(SpriteBatch sb, Rectangle rect, float radiusFactor, float alpha, int segments, float thickness, float distortion, float rotationOffset) {
            if (alpha <= 0f) return;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = rect.Center.ToVector2();
            float baseRadius = MathF.Min(rect.Width, rect.Height) * 0.5f * radiusFactor;
            float time = Main.GlobalTimeWrappedHourly + rotationOffset;
            float step = MathHelper.TwoPi / segments;
            Vector2 prev = center + (0f + time).ToRotationVector2() * baseRadius;
            for (int i = 1; i <= segments; i++) {
                float ang = i * step + time;
                float noise = (float)Math.Sin(ang * 3f + time * 4f) * distortion;
                float r = baseRadius + noise;
                Vector2 cur = center + ang.ToRotationVector2() * r;
                Vector2 diff = cur - prev; float len = diff.Length(); if (len > 0.01f) {
                    float rot = diff.ToRotation();
                    Color col = new Color(180, 230, 255) * (alpha * (0.6f + 0.4f * (float)Math.Sin(ang * 2f + time * 5f)));
                    sb.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                }
                prev = cur;
            }
        }

        private class ExtraTooltipParticle
        {
            private Vector2 center;
            private float angle;
            private float radius;
            private float speed;
            private float radialDrift;
            private float alpha;
            private float scale;
            private int life;
            private int maxLife;
            public bool Dead => alpha <= 0f || life > maxLife;
            public ExtraTooltipParticle(Vector2 c, float ang, float r) {
                center = c;
                angle = ang;
                radius = r;
                speed = Main.rand.NextFloat(0.01f, 0.035f) * (Main.rand.NextBool() ? 1f : -1f);
                radialDrift = Main.rand.NextFloat(-0.05f, 0.05f);
                alpha = 0f;
                scale = Main.rand.NextFloat(1.2f, 2.4f);
                life = 0;
                maxLife = Main.rand.Next(90, 160);
            }
            public void Update() {
                life++;
                angle += speed;
                radius += radialDrift;
                if (radius < 6f) radius = 6f;
                if (life < 20) alpha = MathHelper.Lerp(alpha, 1f, 0.15f);
                else if (life > maxLife - 25) alpha *= 0.9f;
                else alpha *= 0.995f;
            }
            public void Draw(SpriteBatch sb, float globalAlpha) {
                if (Dead) return;
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                Color col = Color.Lerp(new Color(120, 200, 255), new Color(255, 220, 140), (float)Math.Sin(angle * 2f) * 0.5f + 0.5f) * (alpha * globalAlpha * 0.6f);
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col, angle, new Vector2(0.5f, 0.5f), new Vector2(scale * 3f, scale), SpriteEffects.None, 0f);
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col * 0.5f, angle + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale * 2f, scale * 0.6f), SpriteEffects.None, 0f);
            }
        }

        private static string GetLayerNumeralText(int i) {
            if (Language.ActiveCulture.LegacyId != (int)GameCulture.CultureName.Chinese) {
                return i switch {
                    1 => "I",
                    2 => "II",
                    3 => "III",
                    4 => "IV",
                    5 => "V",
                    6 => "VI",
                    7 => "VII",
                    8 => "VIII",
                    9 => "IX",
                    10 => "X",
                    _ => i.ToString()
                };
            }
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

        private static void DrawFancyBorder(SpriteBatch spriteBatch, Rectangle rect, Color glow, float alpha) {
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

        private static void DrawCorner(SpriteBatch spriteBatch, Vector2 pos, Color color, float rot) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < 3; i++) {
                float len = 6 - i * 2;
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * (0.9f - i * 0.3f), rot, new Vector2(0, 0.5f), new Vector2(len, 1f), SpriteEffects.None, 0f);
            }
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
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
}
