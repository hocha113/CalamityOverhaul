using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
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
            
            // 如果是同一个实例，不需要切换
            if (oldBox == newBox) {
                return;
            }

            // 转移队列和当前对话
            if (transferQueue && oldBox != null && oldBox.Active) {
                TransferDialogueState(oldBox, newBox);
            }

            // 强制关闭旧对话框（清空状态但不触发完成回调）
            if (oldBox != null && oldBox != newBox) {
                ForceCloseBox(oldBox);
            }

            // 更新解析器指向新对话框
            SetResolver(() => newBox);
            _lastUsedBox = newBox;
        }

        /// <summary>
        /// 转移对话状态从旧对话框到新对话框
        /// </summary>
        private static void TransferDialogueState(DialogueBoxBase from, DialogueBoxBase to) {
            // 使用反射访问 protected 成员进行状态迁移
            var queueField = typeof(DialogueBoxBase).GetField("queue", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentField = typeof(DialogueBoxBase).GetField("current", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var preProcessorField = typeof(DialogueBoxBase).GetProperty("PreProcessor");
            var playedCountField = typeof(DialogueBoxBase).GetField("playedCount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (queueField == null || currentField == null) {
                return;
            }

            // 获取旧对话框的队列
            var oldQueue = queueField.GetValue(from) as Queue<DialogueBoxBase.DialogueSegment>;
            var oldCurrent = currentField.GetValue(from) as DialogueBoxBase.DialogueSegment;

            if (oldQueue == null) {
                return;
            }

            // 清空新对话框
            var newQueue = queueField.GetValue(to) as Queue<DialogueBoxBase.DialogueSegment>;
            newQueue?.Clear();
            currentField.SetValue(to, null);

            // 如果有当前对话，重新入队
            if (oldCurrent != null) {
                to.EnqueueDialogue(oldCurrent.Speaker, oldCurrent.Content, oldCurrent.OnFinish, oldCurrent.OnStart);
            }

            // 转移剩余队列
            foreach (var segment in oldQueue) {
                to.EnqueueDialogue(segment.Speaker, segment.Content, segment.OnFinish, segment.OnStart);
            }

            // 转移预处理器
            if (preProcessorField != null) {
                var oldProcessor = preProcessorField.GetValue(from);
                preProcessorField.SetValue(to, oldProcessor);
            }

            // 转移播放计数（关键修复）
            if (playedCountField != null) {
                var oldPlayedCount = playedCountField.GetValue(from);
                playedCountField.SetValue(to, oldPlayedCount);
            }
        }

        /// <summary>
        /// 强制关闭对话框，不触发完成回调
        /// </summary>
        private static void ForceCloseBox(DialogueBoxBase box) {
            if (box == null) {
                return;
            }

            var queueField = typeof(DialogueBoxBase).GetField("queue", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentField = typeof(DialogueBoxBase).GetField("current", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var closingField = typeof(DialogueBoxBase).GetField("closing", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var showProgressField = typeof(DialogueBoxBase).GetField("showProgress", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hideProgressField = typeof(DialogueBoxBase).GetField("hideProgress", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // 清空队列和当前对话
            if (queueField?.GetValue(box) is Queue<DialogueBoxBase.DialogueSegment> queue) {
                queue.Clear();
            }
            currentField?.SetValue(box, null);
            
            // 重置状态
            closingField?.SetValue(box, false);
            showProgressField?.SetValue(box, 0f);
            hideProgressField?.SetValue(box, 0f);
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
