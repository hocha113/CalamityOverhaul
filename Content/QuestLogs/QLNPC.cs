using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;

namespace CalamityOverhaul.Content.QuestLogs
{
    internal class QLNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {//死亡钩子，多人各端调用
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.OnKillByNPC(npc);
                }
            }
        }
    }
}
