using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //冒险家篇成长线：角色养成与生活目标，分布在冒险家群落的西缘与南缘
    //三色新矿与叶绿挂在工匠篇挖矿链尾（东区 y=0/150）

    internal class FishermanQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "渔夫的委托");
            Description = this.GetLocalization(nameof(Description), () => "完成五次渔夫委托");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "海边住着个使唤人的小鬼，但他给的报酬意外地实在。鱼在哪儿，宝贝就在哪儿。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.ReinforcedFishingPole;
            Position = new Vector2(150, 0);
            AddParent<ExploreOcean>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "完成渔夫委托"),
                RequiredProgress = 5
            });

            AddReward(ItemID.CratePotion, 5);
            AddReward(ItemID.FishingPotion, 5);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.anglerQuestsFinished;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class LifeMaxQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "生命之泉");
            Description = this.GetLocalization(nameof(Description), () => "将生命上限提升到400");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "红心水晶只能带你走到一半。剩下的路，要靠不断的战斗与成长。四百点生命是站稳硬模式的门票。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.LifeCrystal;
            Position = new Vector2(150, 0);
            AddParent<FindLifeCrystal>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "生命上限达到400"),
                RequiredProgress = 400
            });

            AddReward(ItemID.HealingPotion, 10);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.statLifeMax;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ManaMaxQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "魔力之心");
            Description = this.GetLocalization(nameof(Description), () => "将魔力上限提升到200");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "把坠落之星压成水晶，一颗一颗喂给自己。二十点一口，喝满九口，星空就在你血管里。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.ManaCrystal;
            Position = new Vector2(-150, 0);
            AddParent<CollectFallenStars>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "魔力上限达到200"),
                RequiredProgress = 200
            });

            AddReward(ItemID.ManaPotion, 10);
            AddReward(ItemID.CelestialMagnet, 1);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.statManaMax;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class WingsQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "展翅高飞");
            Description = this.GetLocalization(nameof(Description), () => "装备任意一对翅膀");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "云端的宝库、精灵的羽毛、恶魔的皮膜。不管哪种来路，背上长翅膀的那一刻，摔落伤害就成了历史。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.AngelWings;
            Position = new Vector2(-150, 0);
            AddParent<ExploreFloatingIsland>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "装备翅膀"),
                RequiredProgress = 1
            });

            AddReward(ItemID.SoulofFlight, 20);
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.wingsLogic > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class MoneyQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "第一桶金");
            Description = this.GetLocalization(nameof(Description), () => "持有一枚铂金币");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "一百个金币压成一枚铂金币。攒出它的路上，你大概已经死过很多次了，商人管这叫手续费。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.PlatinumCoin;
            Position = new Vector2(-150, 0);
            AddParent<NPCVillage>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "持有铂金币"),
                RequiredProgress = 1,
                TargetItemID = ItemID.PlatinumCoin
            });

            AddReward(ItemID.PiggyBank, 1);
            AddReward(ItemID.Safe, 1);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(ItemID.PlatinumCoin);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreAetherQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "微光之湖");
            Description = this.GetLocalization(nameof(Description), () => "找到微光湖");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "世界的角落里藏着一汪不属于这里的湖。东西丢进去，捞上来就变了样子。别自己跳进去试。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GalaxyPearl;
            Position = new Vector2(0, 150);
            AddParent<ExploreUnderground>();
            HiddenUntilUnlocked = true;

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达微光湖"),
                RequiredProgress = 1
            });

            AddReward(ItemID.ShimmerTorch, 99);
            AddReward(ItemID.GalaxyPearl, 1);
        }

        protected override bool HiddenTriggerMet() => Main.LocalPlayer.ZoneShimmer;

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneShimmer) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class HardmodeOresQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "三色新矿");
            Description = this.GetLocalization(nameof(Description), () => "收集硬模式的三阶矿石");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "血肉墙倒下后，用神锤砸碎祭坛，地底会长出三种新的金属。它们比先前的一切都更硬。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.AdamantiteOre;
            Position = new Vector2(150, 0);
            AddParent<MiningQuestIII>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.T1", () => "收集钴矿或钯金矿"),
                RequiredProgress = 30,
                TargetItemID = ItemID.CobaltOre
            });
            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.T2", () => "收集秘银矿或山铜矿"),
                RequiredProgress = 30,
                TargetItemID = ItemID.MythrilOre
            });
            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.T3", () => "收集精金矿或钛金矿"),
                RequiredProgress = 30,
                TargetItemID = ItemID.AdamantiteOre
            });

            AddReward(ItemID.MiningPotion, 5);
            AddReward(ItemID.SpelunkerPotion, 5);
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            Objectives[0].CurrentProgress = player.InquireItem(false, ItemID.CobaltOre, ItemID.PalladiumOre);
            Objectives[1].CurrentProgress = player.InquireItem(false, ItemID.MythrilOre, ItemID.OrichalcumOre);
            Objectives[2].CurrentProgress = player.InquireItem(false, ItemID.AdamantiteOre, ItemID.TitaniumOre);
            if (Objectives[0].IsCompleted && Objectives[1].IsCompleted && Objectives[2].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class ChlorophyteQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "叶绿之力");
            Description = this.GetLocalization(nameof(Description), () => "收集三十块叶绿矿");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "机械造物倒下后，丛林深处的泥土里开始生长会发光的绿色金属。它有自己的意志，还会自己繁殖。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.ChlorophyteOre;
            //挂父节点正下方：旧位 (150,150)→绝对 (900,150) 恰在骷髅王(有灾厄)/世纪之花(无灾厄)
            //讨伐节点正上一格，被读作"叶绿和骷髅王连在一起"（反馈 #2）；
            //(0,150)→绝对 (750,150) 两套配置均为空位，连线成最短竖线，脚本核算无新叠点
            Position = new Vector2(0, 150);
            AddParent<HardmodeOresQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Expert;

            AddCollectObjective(30, ItemID.ChlorophyteOre);

            AddReward(ItemID.ChlorophyteBar, 10);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(ItemID.ChlorophyteOre);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
