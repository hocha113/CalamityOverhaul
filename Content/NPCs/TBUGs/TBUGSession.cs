using CalamityOverhaul.Content.NPCs.TBUGs.UIs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>本地 TBUG 交互会话，对话/商店共享 whoAmI</summary>
    internal static class TBUGSession
    {
        /// <summary>当前交互的 TBUG 的 <see cref="NPC.whoAmI"/>，-1 表示无交互</summary>
        public static int BoundWhoAmI { get; private set; } = -1;

        /// <summary>绑定的 TBUG 是否仍活着；UI 逐帧校验，人没了窗要跟着关</summary>
        public static bool IsBoundNPCAlive() {
            int who = BoundWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[who];
            return npc?.active == true && npc.type == ModContent.NPCType<TBUG>();
        }

        /// <summary>对话或商店界面激活/淡出中</summary>
        public static bool IsUIActive => TBUGTalkUI.Instance.Active || TBUGShopUI.Instance.Active;

        public static void Bind(int whoAmI) => BoundWhoAmI = whoAmI;

        public static void Clear() => BoundWhoAmI = -1;

        /// <summary>两个界面都关掉后收尾会话（切界面时由调用方先存 whoAmI 再重新 Bind）</summary>
        public static void MaybeEndSession() {
            if (TBUGTalkUI.Instance.IsOpen || TBUGShopUI.Instance.IsOpen) {
                return;
            }
            Clear();
            TBUGMood.Invalidate();
            //只清防重复，别动一次性台词标记（首单成交要留到下次开对话）
            TBUGDialogue.ResetLastLine();
        }
    }
}
