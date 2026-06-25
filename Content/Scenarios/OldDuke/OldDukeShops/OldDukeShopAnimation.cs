using System;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDukeShops
{
    /// <summary>老公爵商店动画状态管理器</summary>
    internal class OldDukeShopAnimation
    {
        public float UIAlpha { get; set; }
        public float PanelSlideProgress { get; set; }
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        public float AcidFlowTimer { get; private set; }
        public float CurrencyDisplayPulse { get; private set; }

        public float[] SlotHoverProgress { get; } = new float[OldDukeShopInteraction.MaxVisibleItems];
        public float[] SlotFailFlash { get; } = new float[OldDukeShopInteraction.MaxVisibleItems];

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

            AcidFlowTimer += 0.038f;
            CurrencyDisplayPulse += 0.05f;
            if (AcidFlowTimer > MathHelper.TwoPi) AcidFlowTimer -= MathHelper.TwoPi;
            if (CurrencyDisplayPulse > MathHelper.TwoPi) CurrencyDisplayPulse -= MathHelper.TwoPi;

            UpdateFailFlash();
        }

        public void UpdateSlotHoverAnimations(int hoveredIndex, int scrollOffset) {
            int visibleSlotIndex = hoveredIndex >= 0 ? hoveredIndex - scrollOffset : -1;

            for (int i = 0; i < SlotHoverProgress.Length; i++) {
                float target = i == visibleSlotIndex ? 1f : 0f;
                float rate = target > SlotHoverProgress[i] ? 0.28f : 0.16f;
                SlotHoverProgress[i] = MathHelper.Lerp(SlotHoverProgress[i], target, rate);
            }
        }

        public void TriggerFailFlash(int visibleSlotIndex) {
            if (visibleSlotIndex >= 0 && visibleSlotIndex < SlotFailFlash.Length) {
                SlotFailFlash[visibleSlotIndex] = 1f;
            }
        }

        private void UpdateFailFlash() {
            for (int i = 0; i < SlotFailFlash.Length; i++) {
                if (SlotFailFlash[i] > 0f) {
                    SlotFailFlash[i] = Math.Max(0f, SlotFailFlash[i] - 0.14f);
                }
            }
        }

        public void Reset() {
            UIAlpha = 0f;
            PanelSlideProgress = 0f;
            AcidFlowTimer = 0f;
            CurrencyDisplayPulse = 0f;

            for (int i = 0; i < SlotHoverProgress.Length; i++) {
                SlotHoverProgress[i] = 0f;
                SlotFailFlash[i] = 0f;
            }
        }
    }
}
