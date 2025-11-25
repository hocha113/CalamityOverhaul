using System;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.Animation
{
    /// <summary>
    /// UI动画控制器，负责管理UI的开关动画和悬停效果
    /// </summary>
    public class UIAnimationController
    {
        private float _openProgress;
        private int _closeDelayTimer;
        private readonly float[] _slotHoverProgress;

        public float OpenProgress => _openProgress;
        public bool IsFullyOpen => _openProgress >= 1f;
        public bool IsFullyClosed => _openProgress <= 0f;

        public UIAnimationController(int slotCount = SupertableConstants.TOTAL_SLOTS) {
            _openProgress = 0f;
            _closeDelayTimer = 0;
            _slotHoverProgress = new float[slotCount];
        }

        /// <summary>
        /// 更新开启/关闭动画
        /// </summary>
        public void UpdateOpenAnimation(bool shouldBeOpen) {
            if (shouldBeOpen && _closeDelayTimer <= 0) {
                if (_openProgress < 1f) {
                    _openProgress += SupertableConstants.ANIMATION_SPEED_OPEN;
                }
            }
            else {
                if (_openProgress > 0f) {
                    _openProgress -= SupertableConstants.ANIMATION_SPEED_CLOSE;
                }
            }

            _openProgress = MathHelper.Clamp(_openProgress, 0f, 1f);

            if (_closeDelayTimer > 0) {
                _closeDelayTimer--;
            }
        }

        /// <summary>
        /// 更新槽位悬停动画
        /// </summary>
        public void UpdateSlotHoverAnimation(int hoveredSlotIndex) {
            for (int i = 0; i < _slotHoverProgress.Length; i++) {
                float targetProgress = i == hoveredSlotIndex && hoveredSlotIndex >= 0 ? 1f : 0f;

                if (_slotHoverProgress[i] < targetProgress) {
                    _slotHoverProgress[i] += SupertableConstants.HOVER_ANIMATION_SPEED;
                }
                else if (_slotHoverProgress[i] > targetProgress) {
                    _slotHoverProgress[i] -= SupertableConstants.HOVER_ANIMATION_SPEED;
                }

                _slotHoverProgress[i] = MathHelper.Clamp(_slotHoverProgress[i], 0f, 1f);
            }
        }

        /// <summary>
        /// 获取槽位悬停进度
        /// </summary>
        public float GetSlotHoverProgress(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= _slotHoverProgress.Length)
                return 0f;

            return _slotHoverProgress[slotIndex];
        }

        /// <summary>
        /// 请求延迟关闭
        /// </summary>
        public void RequestDelayedClose(int delayFrames = 30) {
            _closeDelayTimer = delayFrames;
        }

        /// <summary>
        /// 立即关闭
        /// </summary>
        public void ForceClose() {
            _closeDelayTimer = 0;
            _openProgress = 0f;
        }

        /// <summary>
        /// 立即打开
        /// </summary>
        public void ForceOpen() {
            _closeDelayTimer = 0;
            _openProgress = 1f;
        }

        /// <summary>
        /// 重置所有动画
        /// </summary>
        public void Reset() {
            _openProgress = 0f;
            _closeDelayTimer = 0;
            Array.Fill(_slotHoverProgress, 0f);
        }

        /// <summary>
        /// 获取缓动后的开启进度(用于平滑动画)
        /// </summary>
        public float GetEasedOpenProgress(EasingType easingType = EasingType.EaseOutCubic) {
            return ApplyEasing(_openProgress, easingType);
        }

        private float ApplyEasing(float t, EasingType easingType) {
            return easingType switch {
                EasingType.Linear => t,
                EasingType.EaseInCubic => t * t * t,
                EasingType.EaseOutCubic => 1f - MathF.Pow(1f - t, 3f),
                EasingType.EaseInOutCubic => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f,
                _ => t
            };
        }
    }

    /// <summary>
    /// 缓动类型
    /// </summary>
    public enum EasingType
    {
        Linear,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic
    }
}
