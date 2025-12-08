using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class MiningQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ItemID.CopperPickaxe;
            Position = new Vector2(150, 0);
            ParentIDs.Add(nameof(FirstQuest));
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集十块铜矿"),
                RequiredProgress = 15
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.CopperBar,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五个铜锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(ItemID.CopperOre);

            // 更新进度
            Objectives[0].CurrentProgress = currentOre;

            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;

                // 播放完成音效或提示
                CombatText.NewText(player.getRect(), Color.Green, "任务完成: " + DisplayName.Value);
            }
        }
    }
}
