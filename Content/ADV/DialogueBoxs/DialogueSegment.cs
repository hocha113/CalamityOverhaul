using System;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 对话段数据，表示一条对话的完整信息
    /// </summary>
    public class DialogueSegment
    {
        /// <summary>
        /// 说话者名称（用于显示）
        /// </summary>
        public string Speaker;

        /// <summary>
        /// 对话内容
        /// </summary>
        public string Content;

        /// <summary>
        /// 对话开始时的回调
        /// </summary>
        public Action OnStart;

        /// <summary>
        /// 对话结束时的回调
        /// </summary>
        public Action OnFinish;

        /// <summary>
        /// 立绘键，如果为null则使用Speaker作为立绘键
        /// 允许角色名和立绘显示分离
        /// </summary>
        public string PortraitKey;

        /// <summary>
        /// 定时对话配置，如果为null则为普通对话（无时间限制）
        /// </summary>
        public TimedDialogueConfig TimedConfig;

        /// <summary>
        /// 是否为定时对话
        /// </summary>
        public bool IsTimed => TimedConfig != null;
    }
}
