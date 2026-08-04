using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>GlobalNPC 时缓来源登记</summary>
    internal class SandevistanNPC : GlobalNPC
    {
        public override void OnSpawn(NPC npc, IEntitySource source) {
            if (SandevistanTimeSlow.IsActive
                && SandevistanTimeSlow.ShouldAffectNPC(npc)) {
                SandevistanTimeSlow.EnsureNPCSource(npc);
            }
        }

        public override bool PreAI(NPC npc) {
            if (SandevistanTimeSlow.IsActive
                && SandevistanTimeSlow.ShouldAffectNPC(npc)) {
                SandevistanTimeSlow.EnsureNPCSource(npc);
            }
            return true;
        }
    }
}
