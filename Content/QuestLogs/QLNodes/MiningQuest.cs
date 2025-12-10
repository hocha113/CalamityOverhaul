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
            Position = new Vector2(300, 0);
            AddParent<FirstQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集十块铜矿"),
                RequiredProgress = 10
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.CopperBar,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五个铜锭")
            });
            Rewards.Add(new QuestReward {
                ItemType = ItemID.IronPickaxe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description2", () => "一把铁镐")
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
            }
        }
    }

    public class MiningQuestII : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "矿工时间 II");
            Description = this.GetLocalization(nameof(Description), () => "收集五十块铁矿");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.IronPickaxe;
            Position = new Vector2(150, 0);
            AddParent<MiningQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集五十块铁矿"),
                RequiredProgress = 50
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.IronBar,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五十个铁锭")
            });
            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldPickaxe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description2", () => "一把金镐")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(ItemID.IronOre);

            // 更新进度
            Objectives[0].CurrentProgress = currentOre;

            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    public class MiningQuestIII : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "矿工时间 III");
            Description = this.GetLocalization(nameof(Description), () => "收集一百块金矿");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GoldPickaxe;
            Position = new Vector2(150, 0);
            AddParent<MiningQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集一百块金矿"),
                RequiredProgress = 100
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldBar,
                Amount = 100,
                Description = this.GetLocalization("QuestReward.Description", () => "一百个金锭")
            });
            Rewards.Add(new QuestReward {
                ItemType = ItemID.PlatinumPickaxe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description2", () => "一把铂金镐")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(ItemID.GoldOre);

            // 更新进度
            Objectives[0].CurrentProgress = currentOre;

            // 检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
