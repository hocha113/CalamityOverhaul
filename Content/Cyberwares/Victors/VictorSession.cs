using CalamityOverhaul.Content.Cyberwares.Victors.UIs;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 本地客户端记录"当前正在与哪个 Victor 实例交互"的轻量会话。
    /// <br/>对话 UI、义体诊所、手术过场共享同一绑定，用于定身面向、镜头聚焦与收尾清理
    /// </summary>
    internal static class VictorSession
    {
        /// <summary>当前交互的 Victor 的 <see cref="Terraria.NPC.whoAmI"/>，-1 表示无交互</summary>
        public static int BoundWhoAmI { get; private set; } = -1;

        /// <summary>对话或诊所界面是否处于激活/淡出状态</summary>
        public static bool IsUIActive => VictorTalkUI.Instance.Active || VictorClinicUI.Instance.Active;

        /// <summary>界面或手术过场任意一者处于进行中</summary>
        public static bool InteractionActive => IsUIActive || VictorSurgery.Active;

        public static void Bind(int whoAmI) => BoundWhoAmI = whoAmI;

        public static void Clear() => BoundWhoAmI = -1;
    }
}
