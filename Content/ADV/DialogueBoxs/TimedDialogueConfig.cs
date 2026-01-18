using System;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 定时对话配置，用于设置有时间限制的对话回合
    /// </summary>
    public class TimedDialogueConfig
    {
        /// <summary>
        /// 对话持续时间（秒）
        /// </summary>
        public float Duration { get; set; } = 6f;

        /// <summary>
        /// 是否显示进度指示器
        /// </summary>
        public bool ShowProgressIndicator { get; set; } = true;

        /// <summary>
        /// 是否允许用户点击提前结束定时对话
        /// 如果为 false，用户必须等待时间耗尽或通过其他方式（如选择选项）结束
        /// </summary>
        public bool AllowManualAdvance { get; set; } = true;

        /// <summary>
        /// 时间耗尽后是否跳过 OnFinish 回调
        /// 对于带选项的定时对话，通常设置为 true，因为 OnTimeExpired 已经处理了选择逻辑
        /// </summary>
        public bool SkipOnFinishWhenExpired { get; set; } = false;

        /// <summary>
        /// 时间耗尽时的回调（在推进到下一条对话之前调用）
        /// 可用于执行超时后的默认行为（如随机选择）
        /// </summary>
        public Action OnTimeExpired { get; set; }

        /// <summary>
        /// 进度更新时的回调（参数为剩余时间比例 0~1）
        /// </summary>
        public Action<float> OnProgressUpdate { get; set; }

        /// <summary>
        /// 创建默认配置（6秒后自动推进）
        /// </summary>
        public static TimedDialogueConfig Default() => new();

        /// <summary>
        /// 创建指定时长的配置
        /// </summary>
        /// <param name="durationSeconds">持续秒数</param>
        public static TimedDialogueConfig WithDuration(float durationSeconds) => new() { Duration = durationSeconds };

        /// <summary>
        /// 创建仅倒计时的配置（用户不能手动跳过，必须等待或选择）
        /// </summary>
        /// <param name="durationSeconds">持续秒数</param>
        /// <param name="onExpired">时间耗尽回调</param>
        public static TimedDialogueConfig CountdownOnly(float durationSeconds, Action onExpired = null) => new() {
            Duration = durationSeconds,
            AllowManualAdvance = false,
            OnTimeExpired = onExpired
        };
    }
}
