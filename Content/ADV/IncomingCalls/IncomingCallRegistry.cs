using CalamityOverhaul.Content.ADV.IncomingCalls.Styles;
using System;

namespace CalamityOverhaul.Content.ADV.IncomingCalls
{
    /// <summary>来电 UI 注册器，默认 <see cref="DraedonIncomingCall"/></summary>
    internal static class IncomingCallRegistry
    {
        private static Func<IncomingCallBase> _resolver;

        /// <summary>设置自定义解析委托，传入 null 恢复默认</summary>
        public static void SetResolver(Func<IncomingCallBase> resolver) => _resolver = resolver;

        /// <summary>获取当前来电UI实例</summary>
        public static IncomingCallBase Current => _resolver?.Invoke() ?? DraedonIncomingCall.Instance;

        /// <summary>切换来电UI风格</summary>
        public static void SwitchStyle(IncomingCallBase newStyle) {
            if (newStyle == null) return;

            var old = Current;
            if (old == newStyle) return;

            //强制结束旧来电
            old?.ForceEnd();

            SetResolver(() => newStyle);
        }

        /// <summary>快捷发起来电</summary>
        public static void StartCall(string caller, string portraitKey = null) {
            Current.StartCall(caller, portraitKey);
        }

        /// <summary>快捷入队台词</summary>
        public static void EnqueueLine(string speaker, string content, Action onFinish = null, Action onStart = null, int autoAdvanceDelay = 120) {
            Current.EnqueueLine(speaker, content, onFinish, onStart, autoAdvanceDelay);
        }

        /// <summary>重置</summary>
        public static void Reset() {
            Current?.ForceEnd();
            _resolver = null;
        }
    }
}
