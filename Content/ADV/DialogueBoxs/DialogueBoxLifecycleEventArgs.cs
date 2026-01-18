using System;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 对话框生命周期事件参数
    /// </summary>
    public class DialogueBoxLifecycleEventArgs : EventArgs
    {
        /// <summary>
        /// 触发事件的对话框
        /// </summary>
        public DialogueBoxBase DialogueBox { get; }

        /// <summary>
        /// 之前的状态
        /// </summary>
        public DialogueBoxState PreviousState { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public DialogueBoxState CurrentState { get; }

        /// <summary>
        /// 是否被强制关闭
        /// </summary>
        public bool WasForceClosed { get; }

        public DialogueBoxLifecycleEventArgs(DialogueBoxBase box, DialogueBoxState previous, DialogueBoxState current, bool forceClosed = false) {
            DialogueBox = box;
            PreviousState = previous;
            CurrentState = current;
            WasForceClosed = forceClosed;
        }
    }
}
