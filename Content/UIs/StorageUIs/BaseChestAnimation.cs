using System;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>箱子UI动画，淡入滑入+槽悬停</summary>
    internal abstract class BaseChestAnimation
    {
        public float UIAlpha { get; set; } = 0f;
        public float PanelSlideProgress { get; set; } = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        public float[] SlotHoverProgress { get; private set; }
        private const float HoverSpeed = 0.15f;

        protected BaseChestAnimation(int totalSlots) {
            SlotHoverProgress = new float[totalSlots];
        }

        public void UpdateUIAnimation(bool isActive) {
            if (isActive) {
                UIAlpha = Math.Min(1f, UIAlpha + FadeSpeed);
                PanelSlideProgress = Math.Min(1f, PanelSlideProgress + SlideSpeed);
            }
            else {
                UIAlpha = Math.Max(0f, UIAlpha - FadeSpeed);
                PanelSlideProgress = Math.Max(0f, PanelSlideProgress - SlideSpeed * 0.5f);
            }
        }

        /// <summary>主题计时器，子类覆写</summary>
        public abstract void UpdateThemeEffects();

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

        public virtual void Reset() {
            UIAlpha = 0f;
            PanelSlideProgress = 0f;
            Array.Clear(SlotHoverProgress, 0, SlotHoverProgress.Length);
        }
    }
}
