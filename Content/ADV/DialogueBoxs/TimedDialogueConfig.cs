using System;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 定时对话配置
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
        /// 是否允许点击提前结束
        /// false 须等超时或由选项结束
        /// </summary>
        public bool AllowManualAdvance { get; set; } = true;

        /// <summary>
        /// 超时是否跳过 OnFinish
        /// 带选项定时对话通常 true，OnTimeExpired 已处理选择
        /// </summary>
        public bool SkipOnFinishWhenExpired { get; set; } = false;

        /// <summary>
        /// 超时回调（推进下一条前）
        /// 可执行默认行为如随机选项
        /// </summary>
        public Action OnTimeExpired { get; set; }

        /// <summary>
        /// 进度回调，参数为剩余时间比 0~1
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
        /// 仅倒计时，不可手动跳过
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
