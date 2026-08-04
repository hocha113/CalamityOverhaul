using CalamityOverhaul.Content.TimeFreezes;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>GlobalNPC 时缓来源登记</summary>
    internal class SandevistanNPC : GlobalNPC
    {
        public override void OnSpawn(NPC npc, IEntitySource source) {
            SandevistanTimeSlow.ReconcileNPC(npc);
        }

        public override bool PreAI(NPC npc) {
            SandevistanTimeSlow.ReconcileNPC(npc);
            return true;
        }

        public override void PostAI(NPC npc) {
            SandevistanTimeSlow.ReconcileNPC(npc);
        }

        public override void OnKill(NPC npc) {
            TimeFreezeSystem.ClearNPCTimeScale<SandevistanTimeSlow>(npc);
        }
    }
}
