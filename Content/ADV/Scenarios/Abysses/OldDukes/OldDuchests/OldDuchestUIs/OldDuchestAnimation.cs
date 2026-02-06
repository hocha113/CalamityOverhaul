using System;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// 老箱子UI动画状态管理器
    /// </summary>
    internal class OldDuchestAnimation
    {
        //UI基础动画
        public float UIAlpha { get; set; } = 0f;
        public float PanelSlideProgress { get; set; } = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //木质箱子特效计时器
        public float WoodGrainPhase { get; private set; } = 0f;
        public float DustTimer { get; private set; } = 0f;
        public float GlowTimer { get; private set; } = 0f;

        //槽位悬停动画
        public float[] SlotHoverProgress { get; private set; } = new float[240]; //20x12=240格
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
        /// 更新老箱子特效计时器
        /// </summary>
        public void UpdateEffects() {
            WoodGrainPhase += 0.01f;
            DustTimer += 0.025f;
            GlowTimer += 0.018f;

            if (WoodGrainPhase > MathHelper.TwoPi) WoodGrainPhase -= MathHelper.TwoPi;
            if (DustTimer > MathHelper.TwoPi) DustTimer -= MathHelper.TwoPi;
            if (GlowTimer > MathHelper.TwoPi) GlowTimer -= MathHelper.TwoPi;
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
            WoodGrainPhase = 0f;
            DustTimer = 0f;
            GlowTimer = 0f;
            Array.Clear(SlotHoverProgress, 0, SlotHoverProgress.Length);
        }
    }
}
