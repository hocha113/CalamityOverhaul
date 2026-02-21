using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Common
{
    internal class DeathTrackingNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private bool _deathHandled = false;
        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (_deathHandled) {
                return;
            }
            if (npc.life <= 0 || !npc.active) {//有些情况下是手动设置的死亡，这时life可能不为0，但active会被设为false
                _deathHandled = true;
                OnNPCDeath(npc);
            }
        }
        /// <summary>
        /// 当NPC被击杀时调用，在客户端或者服务端上均会运行
        /// </summary>
        /// <param name="npc"></param>
        public virtual void OnNPCDeath(NPC npc) { }
    }
}
