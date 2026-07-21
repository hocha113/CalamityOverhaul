using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Common
{
    internal class DeathTrackingNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private bool _deathHandled = false;
        public sealed override void OnKill(NPC npc) { }
        public sealed override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (_deathHandled) {
                return;
            }
            //手动死时 life 可能仍>0，靠 active==false
            if (npc.life <= 0 || !npc.active) {
                _deathHandled = true;
                OnNPCDeath(npc);
            }
        }
        /// <summary>击杀回调，客户端与服务端都会跑</summary>
        public virtual void OnNPCDeath(NPC npc) { }
    }
}
