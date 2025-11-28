using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes.OldDukeShops
{
    /// <summary>
    /// 老公爵商店动画状态管理器
    /// </summary>
    internal class OldDukeShopAnimation
    {
        //UI动画参数
        public float UIAlpha { get; set; } = 0f;
        public float PanelSlideProgress { get; set; } = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //硫磺海动画参数
        public float ToxicWavePhase { get; private set; } = 0f;
        public float SulfurPulse { get; private set; } = 0f;
        public float MiasmaTimer { get; private set; } = 0f;
        public float BubbleTimer { get; private set; } = 0f;
        public float AcidFlowTimer { get; private set; } = 0f;
        public float CorrosionPulse { get; private set; } = 0f;
        public float CurrencyDisplayPulse { get; private set; } = 0f;

        //槽位悬停动画
        public float[] SlotHoverProgress { get; private set; } = new float[20];//支持20个可见槽位

        /// <summary>
        /// 更新UI激活状态
        /// </summary>
        public void UpdateUIAnimation(bool isActive) {
            if (isActive) {
                if (UIAlpha < 1f) {
                    UIAlpha += FadeSpeed;
                    UIAlpha = Math.Clamp(UIAlpha, 0f, 1f);
                }
                if (PanelSlideProgress < 1f) {
                    PanelSlideProgress += SlideSpeed;
                    PanelSlideProgress = Math.Clamp(PanelSlideProgress, 0f, 1f);
                }
            }
            else {
                if (UIAlpha > 0f) {
                    UIAlpha -= FadeSpeed * 1.2f;
                    UIAlpha = Math.Clamp(UIAlpha, 0f, 1f);
                }
                if (PanelSlideProgress > 0f) {
                    PanelSlideProgress -= SlideSpeed * 1.2f;
                    PanelSlideProgress = Math.Clamp(PanelSlideProgress, 0f, 1f);
                }
            }
        }

        /// <summary>
        /// 更新硫磺海效果动画
        /// </summary>
        public void UpdateSulfseaEffects() {
            ToxicWavePhase += 0.022f;
            SulfurPulse += 0.015f;
            MiasmaTimer += 0.032f;
            BubbleTimer += 0.045f;
            AcidFlowTimer += 0.038f;
            CorrosionPulse += 0.028f;
            CurrencyDisplayPulse += 0.05f;

            if (ToxicWavePhase > MathHelper.TwoPi) ToxicWavePhase -= MathHelper.TwoPi;
            if (SulfurPulse > MathHelper.TwoPi) SulfurPulse -= MathHelper.TwoPi;
            if (MiasmaTimer > MathHelper.TwoPi) MiasmaTimer -= MathHelper.TwoPi;
            if (BubbleTimer > MathHelper.TwoPi) BubbleTimer -= MathHelper.TwoPi;
            if (AcidFlowTimer > MathHelper.TwoPi) AcidFlowTimer -= MathHelper.TwoPi;
            if (CorrosionPulse > MathHelper.TwoPi) CorrosionPulse -= MathHelper.TwoPi;
            if (CurrencyDisplayPulse > MathHelper.TwoPi) CurrencyDisplayPulse -= MathHelper.TwoPi;
        }

        /// <summary>
        /// 更新槽位悬停动画
        /// </summary>
        public void UpdateSlotHoverAnimations(int hoveredIndex) {
            for (int i = 0; i < SlotHoverProgress.Length; i++) {
                float target = i == hoveredIndex ? 1f : 0f;
                SlotHoverProgress[i] = MathHelper.Lerp(SlotHoverProgress[i], target, 0.15f);
            }
        }

        /// <summary>
        /// 重置所有动画状态
        /// </summary>
        public void Reset() {
            UIAlpha = 0f;
            PanelSlideProgress = 0f;
            ToxicWavePhase = 0f;
            SulfurPulse = 0f;
            MiasmaTimer = 0f;
            BubbleTimer = 0f;
            AcidFlowTimer = 0f;
            CorrosionPulse = 0f;

            for (int i = 0; i < SlotHoverProgress.Length; i++) {
                SlotHoverProgress[i] = 0f;
            }
        }
    }
}
