using CalamityOverhaul.Content.Cyberwares.Victors.UIs;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>本地 Victor 交互会话，对话/诊所/手术共享 whoAmI</summary>
    internal static class VictorSession
    {
        /// <summary>当前交互的 Victor 的 <see cref="Terraria.NPC.whoAmI"/>，-1 表示无交互</summary>
        public static int BoundWhoAmI { get; private set; } = -1;

        /// <summary>对话或诊所界面激活/淡出中</summary>
        public static bool IsUIActive => VictorTalkUI.Instance.Active || VictorClinicUI.Instance.Active;

        /// <summary>界面或手术过场任一进行中</summary>
        public static bool InteractionActive => IsUIActive || VictorSurgery.Active;

        public static void Bind(int whoAmI) => BoundWhoAmI = whoAmI;

        public static void Clear() => BoundWhoAmI = -1;
    }
}
