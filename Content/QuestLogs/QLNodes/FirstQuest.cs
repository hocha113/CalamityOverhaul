using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class FirstQuest : QuestNode
    {
        public override void OnLoad() {
            Position = new Vector2(0, 0);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "点击领取"),
                RequiredProgress = 10
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Torch,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "点击领取")
            });
            
            ChildIDs.Add(nameof(MiningQuest));
        }

        public override void OnUpdate() {
            Player player = Main.LocalPlayer;
            int currentWood = player.CountItem(ItemID.Wood);
            
            // 更新进度
            Objectives[0].CurrentProgress = currentWood;
            
            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
                
                // 解锁后续任务
                QuestNode.GetQuest<MiningQuest>().IsUnlocked = true;
                
                // 播放完成音效或提示
                CombatText.NewText(player.getRect(), Color.Green, "任务完成: " + DisplayName.Value);
            }
        }
    }
}
