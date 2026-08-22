using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 身份伪装的位置伪装 pass：AI 运行前把"该 NPC 正在追的玩家"的位置
    /// 换成诱饵落点，AI 跑完无条件还原，敌怪的索敌、走位、弹幕出生点
    /// 全都读到假位置，仇恨便被掉落物接走。<br/>
    /// tML 的 PostAI 不受任何 PreAI 返回值影响、必定执行，换位/还原配对天然成立；
    /// PreAI 开头再做一次自愈还原，兜 AI 中途抛异常打断配对的账。<br/>
    /// 每个端都跑这条 pass（客户端也本地模拟 NPC AI），登记表由
    /// <see cref="EntityMasquerade"/> 的复制生命周期在各端维护
    /// </summary>
    internal sealed class EntityMasqueradeNPC : GlobalNPC
    {
        //NPC 逐个串行更新，一个换位槽够用；-1 = 空
        private static int swappedPlayer = -1;
        private static Vector2 savedPosition;

        public override bool PreAI(NPC npc) {
            //上一只 NPC 的 AI 若抛了异常，它的 PostAI 不会跑，先把泄漏还回去
            RestoreSwap();
            if (!EntityMasquerade.HasAnyDecoy) return true;
            //Boss 明确豁免（设计稿：防 Boss 挂机农场）；友方不吃仇恨
            if (npc.friendly || npc.boss) return true;
            int target = npc.target;
            if (target < 0 || target >= Main.maxPlayers) return true;
            Player player = Main.player[target];
            if (player?.active != true) return true;
            if (!EntityMasquerade.TryGetLureAnchor(target, npc.Center,
                out Vector2 anchor)) {
                return true;
            }
            swappedPlayer = target;
            savedPosition = player.position;
            player.Center = anchor;
            return true;
        }

        public override void PostAI(NPC npc) => RestoreSwap();

        private static void RestoreSwap() {
            if (swappedPlayer < 0) return;
            Main.player[swappedPlayer].position = savedPosition;
            swappedPlayer = -1;
        }

        public override void Unload() {
            swappedPlayer = -1;
            savedPosition = default;
        }
    }
}
