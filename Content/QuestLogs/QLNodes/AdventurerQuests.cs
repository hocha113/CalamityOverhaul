using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal class AdventurerQuests : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "冒险家");
            Description = this.GetLocalization(nameof(Description), () => "世界任我走！");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.HermesBoots;
            Position = new Vector2(-650, 0);
            AddParent<FirstQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "点击领取"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.HermesBoots,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "跑靴")
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.LuckyHorseshoe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "马蹄铁")
            });
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = 1;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
