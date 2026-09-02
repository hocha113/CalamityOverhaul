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
    //=========================================================================
    // 终末篇布局:主轴 x=-100 竖着走 终末石(-300)→古恒石(-450)→终末之战(-600)→征战轮回(-750),
    // 两翼各五件武器 y 步进 100、距枢纽 x±150 成扇:
    //   右翼 终焉武器 挂征战轮回,落 x=50
    //   左翼 中子武器 挂中子星锭(古恒石左上 -200,-300 → 绝对 -300,-750),落 x=-450
    // 无灾厄时终末石/古恒石/终末之战/征战轮回全部缺席,中子星锭改挂起点 (-100,-450) 并兼作右翼枢纽,
    // 两翼仍对称落 x=50 / x=-250,y 从 -650 到 -250。
    // 双场景簇外最近距离 ≥206px(左邻冒险家东缘 x=-500,右邻工业西缘 x=300);
    // 旧布局右翼落 x=350(有灾厄)/150(无灾厄),与电容矩阵(300,-600)相距 71/158px,连线还穿过电容矩阵
    //=========================================================================
    internal static class OmigaQuestParents
    {
        /// <summary>
        /// 终焉武器的枢纽:有古恒石挂征战轮回,否则挂中子星锭。<br/>
        /// 无灾厄时中子星锭与终焉武器本就都没有配方,门控换挂只改记号样式,不改可达性
        /// </summary>
        public static void AddOmigaHubParent(QuestNode quest) {
            quest.ParentIDs.Add(CWRID.Item_Rock > 0 ? nameof(RockQuestII) : nameof(NeutronStarIngotQuest));
        }
    }

    internal class AnnihilatingUniverseQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<AnnihilatingUniverse>();
            Position = new Vector2(150, -200);
            OmigaQuestParents.AddOmigaHubParent(this);
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
            OmigaQuestParents.AddOmigaHubParent(this);
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
            OmigaQuestParents.AddOmigaHubParent(this);
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
            OmigaQuestParents.AddOmigaHubParent(this);
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
            OmigaQuestParents.AddOmigaHubParent(this);
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
            //接在终末之战正上方:五次终末之战本就以第一次为前提,二者同受 Item_Rock 门控,不会缺父。
            //旧位挂古恒石 (200,-300),连线恰好正穿终末之战 (100,-600),读起来本就是一条链
            Position = new Vector2(0, -150);
            AddParent<BossRushQuest>();
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
            if (CWRID.Item_Rock > 0) {
                //古恒石左上,与征战轮回同高,左翼中子武器落 x=-450
                Position = new Vector2(-200, -300);
                AddParent<RockQuest>();
            }
            else {
                //无门槛链:悬在起点左上,兼作两翼共同的枢纽,右翼落 x=50、左翼落 x=-250
                Position = new Vector2(-100, -450);
                AddParent<FirstQuest>();
            }
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
