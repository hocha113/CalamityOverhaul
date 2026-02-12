using CalamityOverhaul.Content.ADV.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 用于在服务端拦截NPC击杀事件并通过网络同步到所有客户端，
    /// 保证DeathTrackingNPC的OnKill在全端都能被正确调用
    /// </summary>
    internal class ADVHook : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (!VaultLoad.LoadenContent) {
                return;
            }
            //单人模式下直接派发，不需要网络同步
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            //服务端处理：发送网络包通知所有客户端，服务端自身的OnKill由tModLoader自动调用
            if (VaultUtils.isServer) {
                DeathTrackingNPC.SendKillSync(npc.whoAmI, npc.type);
            }
        }
    }
}
