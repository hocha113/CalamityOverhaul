using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class FirstQuest : QuestNode
    {
        public override void OnLoad() {
            Position = new Vector2(0, 0);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = DetailedDescription,
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Wood,
                Amount = 50,
                Description = Description
            });
        }
    }
}
