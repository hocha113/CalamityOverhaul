using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using System;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>当前对话框类型管理，默认 <see cref="SeaDialogueBox"/></summary>
    internal static class DialogueUIRegistry
    {
        private static Func<DialogueBoxBase> _resolver;
        private static DialogueBoxBase _lastUsedBox;

        /// <summary>自定义对话框实例解析委托（需已注册），null 恢复默认</summary>
        public static void SetResolver(Func<DialogueBoxBase> resolver) => _resolver = resolver;

        /// <summary>获取默认对话框实例的提供器</summary>
        internal static Func<DialogueBoxBase> GetDefault => () => SeaDialogueBox.Instance;

        /// <summary>获取当前应当使用的对话框实例</summary>
        public static DialogueBoxBase Current => _resolver?.Invoke() ?? SeaDialogueBox.Instance;

        /// <summary>切换对话框样式，并迁移当前对话队列</summary>
        /// <param name="newBox">新的对话框实例</param>
        /// <param name="transferQueue">是否转移队列（默认 true）</param>
        public static void SwitchDialogueBox(DialogueBoxBase newBox, bool transferQueue = true) {
            if (newBox == null) {
                return;
            }

            var oldBox = _lastUsedBox ?? Current;

            //同实例无需切换
            if (oldBox == newBox) {
                return;
            }

            //转移队列与当前段
            if (transferQueue && oldBox != null && oldBox.Active) {
                TransferDialogueState(oldBox, newBox);
            }

            //强制关旧框，不触发完成回调
            if (oldBox != null && oldBox != newBox) {
                ForceCloseBox(oldBox);
            }

            //解析器指向新框
            SetResolver(() => newBox);
            _lastUsedBox = newBox;
        }

        /// <summary>转移对话状态从旧对话框到新对话框</summary>
        private static void TransferDialogueState(DialogueBoxBase from, DialogueBoxBase to) {
            if (from == null || to == null || from.queue == null) {
                return;
            }

            //清空新框
            to.queue.Clear();
            to.current = null;

            //当前段重新入队
            if (from.current != null) {
                var moved = to.EnqueueDialogue(from.current.Speaker, from.current.Content, from.current.OnFinish, from.current.OnStart);
                moved.IsSpecial = from.current.IsSpecial;
            }

            //转移剩余队列
            foreach (var segment in from.queue) {
                var moved = to.EnqueueDialogue(segment.Speaker, segment.Content, segment.OnFinish, segment.OnStart);
                moved.IsSpecial = segment.IsSpecial;
            }

            //转移预处理器
            to.PreProcessor = from.PreProcessor;

            //转移 playedCount，保持 Index
            to.playedCount = from.playedCount;

            //转移 showProgress
            to.showProgress = from.showProgress;

            //转移 hideProgress
            to.hideProgress = from.hideProgress;

            //转移 contentFade，已显示则直接 1
            to.contentFade = from.contentFade > 0.5f ? 1f : from.contentFade;

            //转移 closing
            to.closing = from.closing;

            //转移 panelHeight
            to.panelHeight = from.panelHeight;

            //转移说话人切换状态
            to.lastSpeaker = from.lastSpeaker;
            to.speakerSwitchProgress = from.speakerSwitchProgress;
        }

        /// <summary>强制关闭对话框，不触发完成回调</summary>
        internal static void ForceCloseBox(DialogueBoxBase box) {
            if (box == null) {
                return;
            }

            //ForceClose 生命周期
            box.ForceClose(clearQueue: true, triggerCallbacks: false);
        }

        /// <summary>优雅地关闭对话框（播放关闭动画）</summary>
        /// <param name="box">要关闭的对话框，如果为 null 则关闭当前对话框</param>
        /// <returns>是否成功开始关闭</returns>
        public static bool CloseBox(DialogueBoxBase box = null) {
            box ??= Current;
            return box?.Close() ?? false;
        }

        /// <summary>关闭所有对话框</summary>
        /// <param name="force">是否强制关闭（跳过动画）</param>
        public static void CloseAll(bool force = false) {
            var current = Current;
            if (current != null) {
                if (force) {
                    current.ForceClose(clearQueue: true, triggerCallbacks: false);
                }
                else {
                    current.Close();
                }
            }

            if (_lastUsedBox != null && _lastUsedBox != current) {
                if (force) {
                    _lastUsedBox.ForceClose(clearQueue: true, triggerCallbacks: false);
                }
                else {
                    _lastUsedBox.Close();
                }
            }

            _resolver = null;
            _lastUsedBox = null;
        }

        /// <summary>重置所有对话框状态</summary>
        public static void ResetAll() {
            CloseAll(force: true);
            _resolver = null;
            _lastUsedBox = null;
        }
    }
}
