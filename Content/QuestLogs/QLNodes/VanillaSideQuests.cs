using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //讨伐篇原版侧翼：主线之外的可选强敌，挂在 y=450 侧翼带
    //布局约定见 layout-audit：主线 y=300，材料带 y=150，侧翼 y=450，事件带 y=600，猎杀带 y=750

    internal class DeerclopsQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "巡游怪");
            Description = this.GetLocalization(nameof(Description), () => "击败巡游怪");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "暴风雪的深处站着一只独眼巨鹿。它不主动找你，是你非要去找它。");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.Deerclops;
            Position = new Vector2(0, 150);
            AddParent<EyeofCthulhuQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            AddDefeatObjective();

            AddReward(ItemID.WarmthPotion, 5);
            AddReward(ItemID.GoldCoin, 3);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.VDownedV13.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class DukeFishronQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "猪龙鱼公爵");
            Description = this.GetLocalization(nameof(Description), () => "击败猪龙鱼公爵");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "用松露虫做饵，海里会咬钩一位公爵。猪、龙、鱼，三种凶相长在同一张脸上。");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.DukeFishron;
            Position = new Vector2(0, 150);
            AddParent<GolemQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddDefeatObjective();

            AddReward(ItemID.MasterBait, 5);
            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedFishron;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class EmpressOfLightQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "光之女皇");
            Description = this.GetLocalization(nameof(Description), () => "击败光之女皇");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "踩死神圣之地的七彩蝶，愤怒的女皇会亲自来讨说法。若在白昼激怒她，任何攻击都是一击致命。");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.HallowBoss;
            Position = new Vector2(0, 150);
            AddParent<QueenSlimeQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddDefeatObjective();

            AddReward(ItemID.SoulofFlight, 20);
            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedEmpressOfLight;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ZenithQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "天顶剑");
            Description = this.GetLocalization(nameof(Description), () => "锻造天顶剑");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "从铜短剑到弑星者，你挥过的每一把剑都记得你。把它们熔在一起，这段旅途就有了形状。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Zenith;
            Position = new Vector2(0, -150);
            AddParent<MoonLordQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            AddReward(ItemID.PlatinumCoin, 1);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ItemID.Zenith) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
