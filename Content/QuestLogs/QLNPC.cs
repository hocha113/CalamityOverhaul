using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;

namespace CalamityOverhaul.Content.QuestLogs
{
    internal class QLNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {//这个钩子会在NPC死亡时被调用，在多人模式中每个客户端都会调用
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.OnKillByNPC(npc);
                }
            }
        }
    }
}
