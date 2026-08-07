using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 图鉴解锁辅助；城镇 NPC 条目只认 <see cref="Main.BestiaryTracker"/> 的交谈记录，
    /// 而 Victor 禁用了原版聊天（<see cref="Victor.CanChat"/>），须在自定义交互里手动登记
    /// </summary>
    internal static class VictorBestiary
    {
        /// <summary>
        /// 右键打开对话时登记「已交谈」；本地即时点亮，多人再请求服务端持久化并广播
        /// </summary>
        internal static void RegisterMet(NPC victor) {
            if (victor?.active != true || Main.dedServ) {
                return;
            }
            Main.BestiaryTracker.Chats.RegisterChatStartWith(victor);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                CyberwareNet.SendBestiaryChat(victor);
            }
        }

        /// <summary>本端是否已有交谈记录（供首次见面台词判定）</summary>
        internal static bool HasMet(NPC victor)
            => victor?.active == true
                && Main.BestiaryTracker.Chats.GetWasChatWith(victor);
    }
}
