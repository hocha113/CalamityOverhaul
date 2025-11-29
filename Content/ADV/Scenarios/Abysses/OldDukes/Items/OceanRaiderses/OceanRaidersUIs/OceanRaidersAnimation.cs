using System;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>
    /// 海洋吞噬者UI动画状态管理器
    /// </summary>
    internal class OceanRaidersAnimation
    {
        //UI基础动画
        public float UIAlpha { get; set; } = 0f;
        public float PanelSlideProgress { get; set; } = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //硫磺海特效计时器
        public float ToxicWavePhase { get; private set; } = 0f;
        public float SulfurPulse { get; private set; } = 0f;
        public float MiasmaTimer { get; private set; } = 0f;
        public float BubbleTimer { get; private set; } = 0f;
        public float AcidFlowTimer { get; private set; } = 0f;

        //槽位悬停动画
        public float[] SlotHoverProgress { get; private set; } = new float[360]; //20x18=360格
        private const float HoverSpeed = 0.15f;

        /// <summary>
        /// 更新UI渐入渐出动画
        /// </summary>
        public void UpdateUIAnimation(bool isActive) {
            //淡入淡出
            if (isActive) {
                UIAlpha = Math.Min(1f, UIAlpha + FadeSpeed);
                PanelSlideProgress = Math.Min(1f, PanelSlideProgress + SlideSpeed);
            }
            else {
                UIAlpha = Math.Max(0f, UIAlpha - FadeSpeed);
                PanelSlideProgress = Math.Max(0f, PanelSlideProgress - SlideSpeed * 0.5f);
            }
        }

        /// <summary>
        /// 更新硫磺海效果计时器
        /// </summary>
        public void UpdateSulfseaEffects() {
            ToxicWavePhase += 0.022f;
            SulfurPulse += 0.015f;
            MiasmaTimer += 0.032f;
            BubbleTimer += 0.028f;
            AcidFlowTimer += 0.018f;

            if (ToxicWavePhase > MathHelper.TwoPi) ToxicWavePhase -= MathHelper.TwoPi;
            if (SulfurPulse > MathHelper.TwoPi) SulfurPulse -= MathHelper.TwoPi;
            if (MiasmaTimer > MathHelper.TwoPi) MiasmaTimer -= MathHelper.TwoPi;
            if (BubbleTimer > MathHelper.TwoPi) BubbleTimer -= MathHelper.TwoPi;
            if (AcidFlowTimer > MathHelper.TwoPi) AcidFlowTimer -= MathHelper.TwoPi;
        }

        /// <summary>
        /// 更新槽位悬停动画
        /// </summary>
        public void UpdateSlotHoverAnimations(int hoveredSlot) {
            for (int i = 0; i < SlotHoverProgress.Length; i++) {
                if (i == hoveredSlot) {
                    SlotHoverProgress[i] = Math.Min(1f, SlotHoverProgress[i] + HoverSpeed);
                }
                else {
                    SlotHoverProgress[i] = Math.Max(0f, SlotHoverProgress[i] - HoverSpeed);
                }
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
            Array.Clear(SlotHoverProgress, 0, SlotHoverProgress.Length);
        }
    }
}
