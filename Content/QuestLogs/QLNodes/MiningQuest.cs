using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class MiningQuest : QuestNode
    {
        public int TargetOreID = ItemID.CopperOre;

        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ItemID.CopperPickaxe;
            Position = new Vector2(300, 0);
            AddParent<FirstQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集十块{0}"),
                RequiredProgress = 10
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.CopperBar,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五个{0}")
            });
            Rewards.Add(new QuestReward {
                ItemType = ItemID.IronPickaxe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description2", () => "一把{0}")
            });
        }

        public override void OnWorldEnter() {
            int targetBar = ItemID.CopperBar;
            int targetPickaxe = ItemID.CopperPickaxe;
            TargetOreID = ItemID.CopperOre;

            if (WorldGen.SavedOreTiers.Copper == TileID.Tin) {
                TargetOreID = ItemID.TinOre;
                targetBar = ItemID.TinBar;
                targetPickaxe = ItemID.TinPickaxe;
            }

            Objectives[0].TargetItemID = TargetOreID;
            Objectives[0].Description = this.GetLocalization("QuestObjective.Description").WithFormatArgs(Lang.GetItemNameValue(TargetOreID));

            Rewards[0].ItemType = targetBar;
            Rewards[0].Description = this.GetLocalization("QuestReward.Description").WithFormatArgs(Lang.GetItemNameValue(targetBar));

            int nextTierPickaxe = ItemID.IronPickaxe;
            if (WorldGen.SavedOreTiers.Iron == TileID.Lead) {
                nextTierPickaxe = ItemID.LeadPickaxe;
            }
            Rewards[1].ItemType = nextTierPickaxe;
            Rewards[1].Description = this.GetLocalization("QuestReward.Description2").WithFormatArgs(Lang.GetItemNameValue(nextTierPickaxe));

            IconItemType = targetPickaxe;
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(TargetOreID);

            //更新进度
            Objectives[0].CurrentProgress = currentOre;

            //检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    public class MiningQuestII : QuestNode
    {
        public int TargetOreID = ItemID.IronOre;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "矿工时间 II");
            Description = this.GetLocalization(nameof(Description), () => "收集五十块{0}");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.IronPickaxe;
            Position = new Vector2(150, 0);
            AddParent<MiningQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集五十块{0}"),
                RequiredProgress = 50
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.IronBar,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五十个{0}")
            });
            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldPickaxe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description2", () => "一把{0}")
            });
        }

        public override void OnWorldEnter() {
            int targetBar = ItemID.IronBar;
            int targetPickaxe = ItemID.IronPickaxe;
            TargetOreID = ItemID.IronOre;

            if (WorldGen.SavedOreTiers.Iron == TileID.Lead) {
                TargetOreID = ItemID.LeadOre;
                targetBar = ItemID.LeadBar;
                targetPickaxe = ItemID.LeadPickaxe;
            }

            Description = this.GetLocalization(nameof(Description)).WithFormatArgs(Lang.GetItemNameValue(TargetOreID));

            Objectives[0].TargetItemID = TargetOreID;
            Objectives[0].Description = this.GetLocalization("QuestObjective.Description").WithFormatArgs(Lang.GetItemNameValue(TargetOreID));

            Rewards[0].ItemType = targetBar;
            Rewards[0].Description = this.GetLocalization("QuestReward.Description").WithFormatArgs(Lang.GetItemNameValue(targetBar));

            int nextTierPickaxe = ItemID.GoldPickaxe;
            if (WorldGen.SavedOreTiers.Gold == TileID.Platinum) {
                nextTierPickaxe = ItemID.PlatinumPickaxe;
            }
            Rewards[1].ItemType = nextTierPickaxe;
            Rewards[1].Description = this.GetLocalization("QuestReward.Description2").WithFormatArgs(Lang.GetItemNameValue(nextTierPickaxe));

            IconItemType = targetPickaxe;
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(TargetOreID);

            //更新进度
            Objectives[0].CurrentProgress = currentOre;

            //检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    public class MiningQuestIII : QuestNode
    {
        public int TargetOreID = ItemID.GoldOre;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "矿工时间 III");
            Description = this.GetLocalization(nameof(Description), () => "收集一百块{0}");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GoldPickaxe;
            Position = new Vector2(150, 0);
            AddParent<MiningQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集一百块{0}"),
                RequiredProgress = 100
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldBar,
                Amount = 100,
                Description = this.GetLocalization("QuestReward.Description", () => "一百个{0}")
            });
            Rewards.Add(new QuestReward {
                ItemType = ItemID.ReaverShark,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description2", () => "一把{0}")
            });
        }

        public override void OnWorldEnter() {
            int targetBar = ItemID.GoldBar;
            int targetPickaxe = ItemID.GoldPickaxe;
            TargetOreID = ItemID.GoldOre;

            if (WorldGen.SavedOreTiers.Iron == TileID.Lead) {
                TargetOreID = ItemID.PlatinumOre;
                targetBar = ItemID.PlatinumBar;
                targetPickaxe = ItemID.ReaverShark;
            }

            Description = this.GetLocalization(nameof(Description)).WithFormatArgs(Lang.GetItemNameValue(TargetOreID));

            Objectives[0].TargetItemID = TargetOreID;
            Objectives[0].Description = this.GetLocalization("QuestObjective.Description").WithFormatArgs(Lang.GetItemNameValue(TargetOreID));

            Rewards[0].ItemType = targetBar;
            Rewards[0].Description = this.GetLocalization("QuestReward.Description").WithFormatArgs(Lang.GetItemNameValue(targetBar));

            int nextTierPickaxe = ItemID.ReaverShark;
            Rewards[1].ItemType = nextTierPickaxe;
            Rewards[1].Description = this.GetLocalization("QuestReward.Description2").WithFormatArgs(Lang.GetItemNameValue(nextTierPickaxe));

            IconItemType = targetPickaxe;
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.CountItem(TargetOreID);

            //更新进度
            Objectives[0].CurrentProgress = currentOre;

            //检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
