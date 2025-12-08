using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class MiningQuest : QuestNode
    {
        public override void OnLoad() {
            Position = new Vector2(150, 0);
            ParentIDs.Add(nameof(FirstQuest));
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                RequiredProgress = 10
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.CopperPickaxe,
                Amount = 1
            });
        }
    }
}
