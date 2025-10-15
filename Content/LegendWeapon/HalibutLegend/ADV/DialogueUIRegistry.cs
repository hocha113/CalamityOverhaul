using System;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    /// <summary>
    /// 管理当前使用的对话框类型，允许更换不同风格实现
    /// 默认返回深海风格 <see cref="SeaDialogueBox"/>
    /// </summary>
    internal static class DialogueUIRegistry
    {
        private static Func<DialogueBoxBase> _resolver;

        /// <summary>
        /// 设置一个解析委托，用于自定义返回哪种对话框实例 (需已在 UIHandleLoader 中注册)
        /// 传入 null 将恢复默认
        /// </summary>
        public static void SetResolver(Func<DialogueBoxBase> resolver) => _resolver = resolver;

        /// <summary>
        /// 获取当前应当使用的对话框实例
        /// </summary>
        public static DialogueBoxBase Current => _resolver?.Invoke() ?? SeaDialogueBox.Instance;
    }
}
