using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// 图鉴解锁辅助；城镇 NPC 条目只认 <see cref="Main.BestiaryTracker"/> 的交谈记录，
    /// 而 TBUG 禁用了原版聊天（<see cref="TBUG.CanChat"/>），须在自定义交互里手动登记
    /// </summary>
    internal static class TBUGBestiary
    {
        /// <summary>
        /// 右键打开对话时登记「已交谈」；本地即时点亮，多人再请求服务端持久化并广播
        /// </summary>
        internal static void RegisterMet(NPC tbug) {
            if (tbug?.active != true || Main.dedServ) {
                return;
            }
            Main.BestiaryTracker.Chats.RegisterChatStartWith(tbug);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                TBUGShopNet.SendBestiaryChat(tbug);
            }
        }

        /// <summary>本端是否已有交谈记录（供首次见面台词判定）</summary>
        internal static bool HasMet(NPC tbug)
            => tbug?.active == true
                && Main.BestiaryTracker.Chats.GetWasChatWith(tbug);
    }
}
