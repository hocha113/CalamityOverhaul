using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //灾厄猎杀任务：小 Boss 与可选强敌
    //异闻篇一侧挂酸雨链与巨像蛤，讨伐篇一侧挂 y=750 猎杀带，终末之战挂终末篇

    internal class GiantClamQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.GiantClam;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "巨像蛤");
            Description = this.GetLocalization(nameof(Description), () => "击败巨像蛤");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "沉沦之海最大的一枚贝壳。平时安静得像块石头，撬开它的人都后悔了。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_GiantClam;
            Position = new Vector2(0, 150);
            AddParent<ExploreSunkenSea>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            AddDefeatObjective();

            AddReward(ItemID.GoldCoin, 5);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed1.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CragmawMireQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.CragmawMire;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "泥沼颚吻");
            Description = this.GetLocalization(nameof(Description), () => "在酸雨中击败泥沼颚吻");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "世界变硬后，酸雨里会浮上来一坨会咬人的淤泥。它长了太多牙，而且都朝着你。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_CragmawMire;
            Position = new Vector2(0, 150);
            AddParent<AcidRainQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddDefeatObjective();

            AddReward(ItemID.GoldCoin, 5);
            AddReward(ItemID.FishingPotion, 5);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed9.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class MaulerQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Mauler;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "血咬鲨");
            Description = this.GetLocalization(nameof(Description), () => "在酸雨中击败血咬鲨");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "酸水泡大的鲨鱼，脾气和酸度成正比。它跃出水面的时候，记得已经太晚了。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Mauler;
            Position = new Vector2(0, 150);
            if (BossQuestAvailability.CragmawMire) {
                AddParent<CragmawMireQuest>();
            }
            else {
                Position = new Vector2(0, 300);
                AddParent<AcidRainQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddDefeatObjective();

            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed24.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class NuclearTerrorQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.NuclearTerror;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "核之恐惧");
            Description = this.GetLocalization(nameof(Description), () => "在酸雨中击败核之恐惧");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "酸雨的最深处孵着一只发光的庞然大物。它走过的地方连酸水都会退开。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_NuclearTerror;
            Position = new Vector2(0, 150);
            if (BossQuestAvailability.Mauler) {
                AddParent<MaulerQuest>();
            }
            else if (BossQuestAvailability.CragmawMire) {
                Position = new Vector2(0, 300);
                AddParent<CragmawMireQuest>();
            }
            else {
                Position = new Vector2(0, 450);
                AddParent<AcidRainQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddDefeatObjective();

            AddReward(ItemID.PlatinumCoin, 1);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed25.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class GreatSandSharkQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.GreatSandShark;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "巨型沙鲨");
            Description = this.GetLocalization(nameof(Description), () => "击败巨型沙鲨");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "沙漠里最快的猎手，沙丘是它的海面。杀够它的同类，族群的王者会亲自出面。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_GreatSandShark;
            Position = new Vector2(0, 450);
            AddParent<MechanicalBossesQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddDefeatObjective();

            AddReward(ItemID.GoldCoin, 8);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed11.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class LeviathanQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Leviathan;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "阿娜希塔与利维坦");
            Description = this.GetLocalization(nameof(Description), () => "击败阿娜希塔与利维坦");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "歌声先来，巨影后到。海妖的乐曲是给深海巨兽引路的，别停下来听。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Leviathan;
            Position = new Vector2(150, 450);
            AddParent<MechanicalBossesQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddDefeatObjective();

            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed12.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class AstrumAureusQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.AstrumAureus;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "白金星舰");
            Description = this.GetLocalization(nameof(Description), () => "击败白金星舰");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "星染之地深处趴着一台被感染的巨型机械。夜里它会醒，履带碾过的地方寸草不生。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_AstrumAureus;
            Position = new Vector2(300, 450);
            AddParent<MechanicalBossesQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddDefeatObjective();

            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed13.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class RavagerQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Ravager;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "毁灭魔像");
            Description = this.GetLocalization(nameof(Description), () => "击败毁灭魔像");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "血肉与砖石拼出来的巨人，每个部件都会单独攻击。拆完它的四肢，本体才会认真起来。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_RavagerBody;
            Position = new Vector2(150, 450);
            AddParent<GolemQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddDefeatObjective();

            AddReward(ItemID.LifeFruit, 5);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed15.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class AstrumDeusQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.AstrumDeus;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星神游龙");
            Description = this.GetLocalization(nameof(Description), () => "击败星神游龙");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "在星染祭坛献上供品，星空里会游下来一条被感染的神明。杀死它，星染的矿脉才会向你敞开。");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_AstrumDeusHead;
            Position = new Vector2(300, 450);
            AddParent<GolemQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddDefeatObjective();

            AddReward(CWRID.Item_AstralBar, 5);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed16.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class BossRushQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_Rock > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "终末之战");
            Description = this.GetLocalization(nameof(Description), () => "通过一次终末之战");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "捏碎终末石，你杀过的所有东西会排着队回来找你。全部再杀一遍，才算给这段旅途盖章。");

            IconTexturePath = "CalamityMod/UI/MiscTextures/BossRushIcon";
            //终末篇主轴上、古恒石正上方一格;征战轮回(五次终末之战)接在它下游
            Position = new Vector2(0, -150);
            AddParent<RockQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "完成终末之战"),
                RequiredProgress = 1
            });

            AddReward(CWRID.Item_Rock, 1);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed32.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
