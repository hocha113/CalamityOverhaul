using System;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>
    /// 箱子UI动画基类，管理通用的淡入淡出、滑动、槽位悬停动画
    /// </summary>
    internal abstract class BaseChestAnimation
    {
        //UI基础动画
        public float UIAlpha { get; set; } = 0f;
        public float PanelSlideProgress { get; set; } = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //槽位悬停动画
        public float[] SlotHoverProgress { get; private set; }
        private const float HoverSpeed = 0.15f;

        protected BaseChestAnimation(int totalSlots) {
            SlotHoverProgress = new float[totalSlots];
        }

        /// <summary>
        /// 更新UI渐入渐出动画
        /// </summary>
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

        /// <summary>
        /// 更新主题特有的效果计时器，子类覆写以不同视觉风格
        /// </summary>
        public abstract void UpdateThemeEffects();

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
        public virtual void Reset() {
            UIAlpha = 0f;
            PanelSlideProgress = 0f;
            Array.Clear(SlotHoverProgress, 0, SlotHoverProgress.Length);
        }
    }
}
