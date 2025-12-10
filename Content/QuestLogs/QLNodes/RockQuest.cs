using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal class TerminusQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Terminus;
            Position = new Vector2(0, -150);
            AddParent<FirstQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得终末石"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_BloodOrb,
                Amount = 999,
                Description = this.GetLocalization("QuestReward.Description", () => "大量血珠")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(CWRID.Item_Terminus);

            // 更新进度
            Objectives[0].CurrentProgress = currentOre;

            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class RockQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_;
            Position = new Vector2(0, -150);
            AddParent<TerminusQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得古恒石"),
                RequiredProgress = 10
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块古恒石")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(CWRID.Item_);

            // 更新进度
            Objectives[0].CurrentProgress = currentOre;

            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
