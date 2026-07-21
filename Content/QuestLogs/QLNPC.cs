using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;

namespace CalamityOverhaul.Content.QuestLogs
{
    internal class QLNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {//死亡钩子，MP各端
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.OnKillByNPC(npc);
                }
            }
        }
    }
}
