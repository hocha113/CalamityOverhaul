using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Industrials.Generator.Hydroelectrics;
using CalamityOverhaul.Content.Industrials.Generator.Thermal;
using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class IndustrialStartQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "工业起步");
            Description = this.GetLocalization(nameof(Description), () => "制作一台风力发电机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<WindGrivenGenerator>();
            Position = new Vector2(150, -150);
            AddParent<MiningQuestIII>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得风力发电机"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.IronBar,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "10个铁锭")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<WindGrivenGenerator>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ThermalPowerQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "火力发电");
            Description = this.GetLocalization(nameof(Description), () => "制作一台火力发电机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<ThermalGenerator>();
            Position = new Vector2(150, 0);
            AddParent<IndustrialStartQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得火力发电机"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Coal,
                Amount = 50,
                Description = this.GetLocalization("QuestReward.Description", () => "50个煤炭")
            });
            Rewards[0].ItemType = ItemID.Coal;
            Rewards[0].Description = this.GetLocalization("QuestReward.Description", () => "50个煤炭");
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.HasItem(ModContent.ItemType<ThermalGenerator>());
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PipelineQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "能源传输");
            Description = this.GetLocalization(nameof(Description), () => "制作通用能源管道");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<UEPipeline>();
            Position = new Vector2(0, -150);
            AddParent<IndustrialStartQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得通用能源管道"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<UEPipeline>(),
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个通用能源管道")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<UEPipeline>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class BatteryQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "能源储存");
            Description = this.GetLocalization(nameof(Description), () => "制作热能电池");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<ThermalBattery>();
            Position = new Vector2(0, -150);
            AddParent<PipelineQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得热能电池"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Wire,
                Amount = 50,
                Description = this.GetLocalization("QuestReward.Description", () => "50个电线")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<ThermalBattery>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class MiningMachineQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "自动化挖掘");
            Description = this.GetLocalization(nameof(Description), () => "制作一台采矿机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<MiningMachine>();
            Position = new Vector2(150, 0);
            AddParent<ThermalPowerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得采矿机"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.SpelunkerPotion,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "5个洞穴探险药水")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<MiningMachine>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class MiningMachineIIQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "自动化挖掘II");
            Description = this.GetLocalization(nameof(Description), () => "制作一台采矿机MK2");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<MiningMachineMk2>();
            Position = new Vector2(0, 150);
            AddParent<MiningMachineQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得采矿机MK2"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.SpelunkerPotion,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "5个洞穴探险药水")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<MiningMachineMk2>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ItemFilterQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "物品筛选");
            Description = this.GetLocalization(nameof(Description), () => "制作一个物品过滤器");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<ItemFilter>();
            Position = new Vector2(150, 0);
            AddParent<MiningMachineQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得物品过滤器"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Chest,
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "2个箱子")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<ItemFilter>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class HydroelectricQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "水力发电");
            Description = this.GetLocalization(nameof(Description), () => "制作一台水力发电机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<Hydroelectric>();
            Position = new Vector2(0, -150);
            AddParent<ThermalPowerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得水力发电机"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.BottomlessBucket,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "水桶")
            });
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<Hydroelectric>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class TeslaTowerQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "特斯拉塔");
            Description = this.GetLocalization(nameof(Description), () => "制作一座特斯拉电磁塔");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<TeslaElectromagneticTower>();
            Position = new Vector2(150, 0);
            AddParent<HydroelectricQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得特斯拉电磁塔"),
                RequiredProgress = 1
            });

            if (CWRRef.Has) {
                if (ModContent.TryFind("CalamityMod", "DubiousPlating", out ModItem dubiousPlatingItem)) {
                    AddReward(dubiousPlatingItem.Type, 20, this.GetLocalization("QuestReward.Description", () => "20个可疑镀层"));
                }
            }
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<TeslaElectromagneticTower>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
