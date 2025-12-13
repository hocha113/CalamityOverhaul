using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses;
using CalamityOverhaul.Content.Items.Magic.Pandemoniums;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal class DarkMatterCompressorQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<DarkMatterCompressorItem>();
            Position = new Vector2(0, -150);
            AddParent<RockQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得暗物质压缩机"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<DarkMatterBall>(),
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "十个暗物质球")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<DarkMatterCompressorItem>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class InfinityCatalystQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<InfinityCatalyst>();
            Position = new Vector2(0, -150);
            AddParent<TransmutationOfMatterQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得无尽催化剂"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "一块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<InfinityCatalyst>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class InfiniteIngotQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<InfiniteIngot>();
            Position = new Vector2(0, -150);
            AddParent<InfinityCatalystQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得无尽锭"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "两块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<InfiniteIngot>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class InfinitePickQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<InfinitePick>();
            Position = new Vector2(150, -150);
            AddParent<InfiniteIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得无尽镐"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<InfinitePick>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class HeavenfallLongbowQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<HeavenfallLongbow>();
            Position = new Vector2(-150, -150);
            AddParent<InfiniteIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得无尽弓"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<HeavenfallLongbow>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class InfiniteToiletQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<InfiniteToiletItem>();
            Position = new Vector2(0, -150);
            AddParent<InfiniteIngotQuest>();
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得无尽马桶"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "一块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<InfiniteToiletItem>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class TransmutationOfMatterQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<TransmutationOfMatterItem>();
            Position = new Vector2(0, -150);
            AddParent<DarkMatterCompressorQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得物质转化台"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "十块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<TransmutationOfMatterItem>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class AnnihilatingUniverseQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<AnnihilatingUniverse>();
            Position = new Vector2(150, -200);
            AddParent<RockQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得寰宇湮灭"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<AnnihilatingUniverse>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class AriaofTheCosmosQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<AriaofTheCosmos>();
            Position = new Vector2(150, -100);
            AddParent<RockQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得寰宇咏叹调"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<AriaofTheCosmos>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class PandemoniumQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<Pandemonium>();
            Position = new Vector2(150, 0);
            AddParent<RockQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得万魔殿"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<Pandemonium>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class SpearOfLonginusQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<SpearOfLonginus>();
            Position = new Vector2(150, 100);
            AddParent<RockQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得朗基努斯"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<SpearOfLonginus>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class DragonsWordQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<DragonsWord>();
            Position = new Vector2(150, 200);
            AddParent<RockQuestII>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得龙言"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<InfiniteIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块无尽锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<DragonsWord>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class RockQuestII : QuestNode
    {
        public override void SetStaticDefaults() {
            IconTexturePath = "CalamityMod/UI/MiscTextures/BossRushIcon";
            Position = new Vector2(200, 0);
            AddParent<TransmutationOfMatterQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                TargetItemID = CWRID.Item_Rock,
                Description = this.GetLocalization("QuestObjective.Description", () => "获得古恒石"),
                RequiredProgress = 5
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_Rock,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块古恒石")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(CWRID.Item_Rock);
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class NeutronStarIngotQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<NeutronStarIngot>();
            Position = new Vector2(-200, 0);
            AddParent<TransmutationOfMatterQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得中子星锭"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "五块中子星锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<NeutronStarIngot>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class NeutronBowQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<NeutronBow>();
            Position = new Vector2(-150, -200);
            AddParent<NeutronStarIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得中子弓"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "两块中子星锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<NeutronBow>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class NeutronGlaiveQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<NeutronGlaive>();
            Position = new Vector2(-150, -100);
            AddParent<NeutronStarIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得中子剑"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "两块中子星锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<NeutronGlaive>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class NeutronScytheQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<NeutronScythe>();
            Position = new Vector2(-150, 0);
            AddParent<NeutronStarIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得中子镰刀"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "两块中子星锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<NeutronScythe>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class NeutronWandQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<NeutronWand>();
            Position = new Vector2(-150, 100);
            AddParent<NeutronStarIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得中子杖"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "两块中子星锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<NeutronWand>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class NeutronGunQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<NeutronGun>();
            Position = new Vector2(-150, 200);
            AddParent<NeutronStarIngotQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得中子枪"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "两块中子星锭")
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.CountItem(ModContent.ItemType<NeutronGun>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
