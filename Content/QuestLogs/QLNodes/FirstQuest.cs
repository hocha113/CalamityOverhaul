using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class FirstQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconTexturePath = "CalamityOverhaul/icon_small";
            Position = new Vector2(0, 0);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "点击领取"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Torch,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "点击领取")
            });

            AddChild<MiningQuest>();
        }

        public override void UpdateByPlayer() {
            // 更新进度
            Objectives[0].CurrentProgress = 1;

            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
