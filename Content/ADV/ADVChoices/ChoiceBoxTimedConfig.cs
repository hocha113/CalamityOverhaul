using System;

namespace CalamityOverhaul.Content.ADV.ADVChoices
{
    /// <summary>
    /// 选项框定时配置，支持灵活的定时选项框创建
    /// </summary>
    public class ChoiceBoxTimedConfig
    {
        /// <summary>
        /// 倒计时持续时间（秒）
        /// </summary>
        public float Duration { get; set; } = 10f;

        /// <summary>
        /// 是否显示进度指示器（描边倒计时）
        /// </summary>
        public bool ShowProgressIndicator { get; set; } = true;

        /// <summary>
        /// 时间耗尽时的回调
        /// </summary>
        public Action OnTimeExpired { get; set; }

        /// <summary>
        /// 进度更新时的回调（参数为剩余时间比例 0~1）
        /// </summary>
        public Action<float> OnProgressUpdate { get; set; }

        /// <summary>
        /// 进度条基础颜色（如果为null则使用当前样式的默认颜色）
        /// </summary>
        public Color? ProgressColor { get; set; }

        /// <summary>
        /// 进度条警告颜色（剩余时间较少时）
        /// </summary>
        public Color? WarningColor { get; set; }

        /// <summary>
        /// 进度条危险颜色（即将结束时）
        /// </summary>
        public Color? DangerColor { get; set; }

        /// <summary>
        /// 进入警告状态的阈值（剩余时间比例，默认35%）
        /// </summary>
        public float WarningThreshold { get; set; } = 0.35f;

        /// <summary>
        /// 进入危险状态的阈值（剩余时间比例，默认15%）
        /// </summary>
        public float DangerThreshold { get; set; } = 0.15f;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ChoiceBoxTimedConfig Default() => new();

        /// <summary>
        /// 创建指定时长的配置
        /// </summary>
        public static ChoiceBoxTimedConfig WithDuration(float seconds) => new() { Duration = seconds };

        /// <summary>
        /// 创建带超时回调的配置
        /// </summary>
        public static ChoiceBoxTimedConfig WithCallback(float seconds, Action onExpired) => new() {
            Duration = seconds,
            OnTimeExpired = onExpired
        };

        /// <summary>
        /// 从剩余帧数创建配置（用于从对话框继承）
        /// </summary>
        public static ChoiceBoxTimedConfig FromRemainingFrames(int frames, Action onExpired = null) => new() {
            Duration = frames / 60f,
            OnTimeExpired = onExpired
        };

        #region 流式API

        /// <summary>
        /// 设置持续时间
        /// </summary>
        public ChoiceBoxTimedConfig SetDuration(float seconds) {
            Duration = seconds;
            return this;
        }

        /// <summary>
        /// 设置超时回调
        /// </summary>
        public ChoiceBoxTimedConfig SetOnExpired(Action callback) {
            OnTimeExpired = callback;
            return this;
        }

        /// <summary>
        /// 设置进度更新回调
        /// </summary>
        public ChoiceBoxTimedConfig SetOnProgressUpdate(Action<float> callback) {
            OnProgressUpdate = callback;
            return this;
        }

        /// <summary>
        /// 设置是否显示进度指示器
        /// </summary>
        public ChoiceBoxTimedConfig SetShowIndicator(bool show) {
            ShowProgressIndicator = show;
            return this;
        }

        /// <summary>
        /// 设置进度条颜色
        /// </summary>
        public ChoiceBoxTimedConfig SetColors(
            Color? baseColor = null,
            Color? warningColor = null,
            Color? dangerColor = null) {
            ProgressColor = baseColor;
            WarningColor = warningColor;
            DangerColor = dangerColor;
            return this;
        }

        /// <summary>
        /// 设置阈值
        /// </summary>
        public ChoiceBoxTimedConfig SetThresholds(float warning = 0.35f, float danger = 0.15f) {
            WarningThreshold = warning;
            DangerThreshold = danger;
            return this;
        }

        #endregion
    }
}
