using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses;
using CalamityOverhaul.Content.Items.Magic.NeutronWands;
using CalamityOverhaul.Content.Items.Magic.Pandemoniums;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Ranged.AnnihilatingUniverses;
using CalamityOverhaul.Content.Items.Ranged.NeutronBows;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal static class OmigaQuestParents
    {
        public static void AddRockGateParent(QuestNode quest) {
            if (CWRID.Item_Rock > 0) {
                quest.ParentIDs.Add(nameof(RockQuest));
            }
            else if (CWRID.Item_Terminus > 0) {
                quest.ParentIDs.Add(nameof(TerminusQuest));
            }
            else {
                quest.ParentIDs.Add(nameof(FirstQuest));
            }
        }

        public static void AddPostRockParent(QuestNode quest) {
            if (CWRID.Item_Rock > 0) {
                quest.ParentIDs.Add(nameof(RockQuestII));
            }
            else {
                //无RockII则挂门槛链+纵偏
                quest.Position += new Vector2(0, -300);
                AddRockGateParent(quest);
            }
        }
    }

    internal class AnnihilatingUniverseQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<AnnihilatingUniverse>();
            Position = new Vector2(150, -200);
            OmigaQuestParents.AddPostRockParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<AnnihilatingUniverse>());
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
            OmigaQuestParents.AddPostRockParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<AriaofTheCosmos>());
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
            OmigaQuestParents.AddPostRockParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<Pandemonium>());
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
            OmigaQuestParents.AddPostRockParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<SpearOfLonginus>());
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
            OmigaQuestParents.AddPostRockParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<DragonsWord>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class RockQuestII : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_Rock > 0;

        public override void SetStaticDefaults() {
            IconTexturePath = "CalamityMod/UI/MiscTextures/BossRushIcon";
            Position = new Vector2(200, -300);
            OmigaQuestParents.AddRockGateParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddCollectObjective(5, CWRID.Item_Rock);

            AddReward(CWRID.Item_Rock, 5);
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(CWRID.Item_Rock);
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
            Position = new Vector2(-200, -300);
            OmigaQuestParents.AddRockGateParent(this);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 5
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<NeutronStarIngot>());
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

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<NeutronBow>());
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

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<NeutronGlaive>());
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

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<NeutronScythe>());
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

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<NeutronWand>());
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

            AddObtainObjective();

            Rewards.Add(new QuestReward {
                ItemType = ModContent.ItemType<NeutronStarIngot>(),
                Amount = 2
            });
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int count = player.InquireItem(ModContent.ItemType<NeutronGun>());
            Objectives[0].CurrentProgress = count;
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
