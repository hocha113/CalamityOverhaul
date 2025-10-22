using System;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 管理当前使用的对话框类型，允许更换不同风格实现
    /// 默认返回深海风格 <see cref="SeaDialogueBox"/>
    /// </summary>
    internal static class DialogueUIRegistry
    {
        private static Func<DialogueBoxBase> _resolver;
        private static DialogueBoxBase _lastUsedBox;

        /// <summary>
        /// 设置一个解析委托，用于自定义返回哪种对话框实例 (需已在 UIHandleLoader 中注册)
        /// 传入 null 将恢复默认
        /// </summary>
        public static void SetResolver(Func<DialogueBoxBase> resolver) => _resolver = resolver;

        /// <summary>
        /// 获取默认对话框实例的提供器
        /// </summary>
        internal static Func<DialogueBoxBase> GetDefault => () => SeaDialogueBox.Instance;

        /// <summary>
        /// 获取当前应当使用的对话框实例
        /// </summary>
        public static DialogueBoxBase Current => _resolver?.Invoke() ?? SeaDialogueBox.Instance;

        /// <summary>
        /// 获取深海风格对话框
        /// </summary>
        public static DialogueBoxBase Sea => SeaDialogueBox.Instance;

        /// <summary>
        /// 获取硫磺火风格对话框
        /// </summary>
        public static DialogueBoxBase Brimstone => BrimstoneDialogueBox.Instance;

        /// <summary>
        /// 切换对话框样式，并迁移当前对话队列
        /// </summary>
        /// <param name="newBox">新的对话框实例</param>
        /// <param name="transferQueue">是否转移队列（默认 true）</param>
        public static void SwitchDialogueBox(DialogueBoxBase newBox, bool transferQueue = true) {
            if (newBox == null) {
                return;
            }

            var oldBox = _lastUsedBox ?? Current;

            //如果是同一个实例，不需要切换
            if (oldBox == newBox) {
                return;
            }

            //转移队列和当前对话
            if (transferQueue && oldBox != null && oldBox.Active) {
                TransferDialogueState(oldBox, newBox);
            }

            //强制关闭旧对话框（清空状态但不触发完成回调）
            if (oldBox != null && oldBox != newBox) {
                ForceCloseBox(oldBox);
            }

            //更新解析器指向新对话框
            SetResolver(() => newBox);
            _lastUsedBox = newBox;
        }

        /// <summary>
        /// 转移对话状态从旧对话框到新对话框
        /// </summary>
        private static void TransferDialogueState(DialogueBoxBase from, DialogueBoxBase to) {
            if (from == null || to == null || from.queue == null) {
                return;
            }

            //清空新对话框
            to.queue.Clear();
            to.current = null;

            //如果有当前对话，重新入队
            if (from.current != null) {
                to.EnqueueDialogue(from.current.Speaker, from.current.Content, from.current.OnFinish, from.current.OnStart);
            }

            //转移剩余队列
            foreach (var segment in from.queue) {
                to.EnqueueDialogue(segment.Speaker, segment.Content, segment.OnFinish, segment.OnStart);
            }

            //转移预处理器
            to.PreProcessor = from.PreProcessor;

            //转移播放计数（关键：保证 Index 正确）
            to.playedCount = from.playedCount;

            //转移显示进度（确保对话框保持显示状态）
            to.showProgress = from.showProgress;

            //转移隐藏进度
            to.hideProgress = from.hideProgress;

            //转移内容淡入进度（关键：避免内容重新淡入）
            //如果已经在显示内容，新对话框也应该直接显示
            to.contentFade = from.contentFade > 0.5f ? 1f : from.contentFade;

            //转移关闭状态
            to.closing = from.closing;

            //转移面板高度（避免尺寸跳变）
            to.panelHeight = from.panelHeight;

            //转移说话人切换状态（避免头像闪烁）
            to.lastSpeaker = from.lastSpeaker;
            to.speakerSwitchProgress = from.speakerSwitchProgress;
        }

        /// <summary>
        /// 强制关闭对话框，不触发完成回调
        /// </summary>
        private static void ForceCloseBox(DialogueBoxBase box) {
            if (box == null) {
                return;
            }

            //清空队列和当前对话
            box.queue.Clear();
            box.current = null;

            //重置状态
            box.closing = false;
            box.showProgress = 0f;
            box.hideProgress = 0f;
        }

        /// <summary>
        /// 重置所有对话框状态
        /// </summary>
        public static void ResetAll() {
            _resolver = null;
            _lastUsedBox = null;
        }
    }
}
