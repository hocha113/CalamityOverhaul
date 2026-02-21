using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Common
{
    internal class DeathTrackingNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (npc.life > 0) {
                return;
            }
            OnNPCDeath(npc);
        }

        /// <summary>
        /// 当NPC被击杀时调用，在客户端或者服务端上均会运行
        /// </summary>
        /// <param name="npc"></param>
        public virtual void OnNPCDeath(NPC npc) { }
    }
}
