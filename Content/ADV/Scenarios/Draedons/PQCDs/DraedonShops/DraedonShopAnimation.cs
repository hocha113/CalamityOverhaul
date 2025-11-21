using System;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops
{
    /// <summary>
    /// 动画状态管理器
    /// </summary>
    internal class DraedonShopAnimation
    {
        //UI动画参数
        public float UIAlpha { get; set; } = 0f;
        public float PanelSlideProgress { get; set; } = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //科技动画参数
        public float ScanLineTimer { get; private set; } = 0f;
        public float DataStreamTimer { get; private set; } = 0f;
        public float CircuitPulseTimer { get; private set; } = 0f;
        public float HologramFlicker { get; private set; } = 0f;
        public float HexGridPhase { get; private set; } = 0f;
        public float EnergyFlowTimer { get; private set; } = 0f;
        public float CoinDisplayPulse { get; private set; } = 0f;

        //槽位悬停动画
        public float[] SlotHoverProgress { get; private set; } = new float[short.MaxValue];

        /// <summary>
        /// 更新UI动画状态
        /// </summary>
        public void UpdateUIAnimation(bool isActive) {
            if (isActive) {
                if (UIAlpha < 1f) UIAlpha += FadeSpeed;
                if (PanelSlideProgress < 1f) PanelSlideProgress += SlideSpeed;
            }
            else {
                if (UIAlpha > 0f) UIAlpha -= FadeSpeed * 1.5f;
                if (PanelSlideProgress > 0f) PanelSlideProgress -= SlideSpeed * 1.5f;
            }

            UIAlpha = MathHelper.Clamp(UIAlpha, 0f, 1f);
            PanelSlideProgress = MathHelper.Clamp(PanelSlideProgress, 0f, 1f);
        }

        /// <summary>
        /// 更新科技特效动画
        /// </summary>
        public void UpdateTechEffects() {
            ScanLineTimer += 0.048f;
            DataStreamTimer += 0.055f;
            CircuitPulseTimer += 0.025f;
            HologramFlicker += 0.12f;
            HexGridPhase += 0.015f;
            EnergyFlowTimer += 0.035f;
            CoinDisplayPulse += 0.08f;

            if (ScanLineTimer > MathHelper.TwoPi) ScanLineTimer -= MathHelper.TwoPi;
            if (DataStreamTimer > MathHelper.TwoPi) DataStreamTimer -= MathHelper.TwoPi;
            if (CircuitPulseTimer > MathHelper.TwoPi) CircuitPulseTimer -= MathHelper.TwoPi;
            if (HologramFlicker > MathHelper.TwoPi) HologramFlicker -= MathHelper.TwoPi;
            if (HexGridPhase > MathHelper.TwoPi) HexGridPhase -= MathHelper.TwoPi;
            if (EnergyFlowTimer > MathHelper.TwoPi) EnergyFlowTimer -= MathHelper.TwoPi;
            if (CoinDisplayPulse > MathHelper.TwoPi) CoinDisplayPulse -= MathHelper.TwoPi;
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
            Array.Clear(SlotHoverProgress, 0, SlotHoverProgress.Length);
        }
    }
}
