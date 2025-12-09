using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class ProfanedGuardiansQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "亵渎使徒");
            Description = this.GetLocalization(nameof(Description), () => "击败亵渎使徒");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "ProfanedGuardianCommander").Type;
            Position = new Vector2(150, 0);
            AddParent<MoonLordQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败亵渎使徒"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "ProfanedShard").Type,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "3个亵渎碎片")
            });
        }

        public override void UpdateByPlayer() {
            //检查亵渎使徒是否被击败
            bool isDowned = InWorldBossPhase.Downed17.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class DragonfollyQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "痴愚金龙");
            Description = this.GetLocalization(nameof(Description), () => "击败痴愚金龙");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "Bumblefuck").Type;
            Position = new Vector2(0, 150);
            AddParent<MoonLordQuest>();
            
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败痴愚金龙"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "EffulgentFeather").Type,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "10个闪耀金羽")
            });
        }

        public override void UpdateByPlayer() {
            //检查痴愚金龙是否被击败
            bool isDowned = InWorldBossPhase.Downed18.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ProvidenceQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "亵渎天神");
            Description = this.GetLocalization(nameof(Description), () => "击败亵渎天神");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Providence;
            Position = new Vector2(150, 0);
            AddParent<ProfanedGuardiansQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败亵渎天神"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_DivineGeode,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个圣神晶石")
            });
        }

        public override void UpdateByPlayer() {
            //检查亵渎天神是否被击败
            bool isDowned = InWorldBossPhase.Downed19.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class StormWeaverQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "风暴编织者");
            Description = this.GetLocalization(nameof(Description), () => "击败风暴编织者");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "StormWeaverHead").Type;
            Position = new Vector2(0, -150);
            AddParent<ProvidenceQuest>();
            
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败风暴编织者"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "ArmoredShell").Type,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "5个装甲外壳")
            });
        }

        public override void UpdateByPlayer() {
            //检查风暴编织者是否被击败
            bool isDowned = InWorldBossPhase.Downed21.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CeaselessVoidQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "无尽虚空");
            Description = this.GetLocalization(nameof(Description), () => "击败无尽虚空");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "CeaselessVoid").Type;
            Position = new Vector2(0, 150);
            AddParent<ProvidenceQuest>();
            
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败无尽虚空"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "DarkPlasma").Type,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "5个暗物质")
            });
        }

        public override void UpdateByPlayer() {
            //检查无尽虚空是否被击败
            bool isDowned = InWorldBossPhase.Downed20.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SignusQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "西格纳斯");
            Description = this.GetLocalization(nameof(Description), () => "击败西格纳斯");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "Signus").Type;
            Position = new Vector2(75, 150);
            AddParent<ProvidenceQuest>();
            
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败西格纳斯"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "TwistingNether").Type,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "5个扭曲虚空")
            });
        }

        public override void UpdateByPlayer() {
            //检查西格纳斯是否被击败
            bool isDowned = InWorldBossPhase.Downed22.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PolterghastQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "噬魂幽花");
            Description = this.GetLocalization(nameof(Description), () => "击败噬魂幽花");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "Polterghast").Type;
            Position = new Vector2(150, 0);
            AddParent<ProvidenceQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败噬魂幽花"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "RuinousSoul").Type,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "10个幽花之魂")
            });
        }

        public override void UpdateByPlayer() {
            //检查噬魂幽花是否被击败
            bool isDowned = InWorldBossPhase.Downed23.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class OldDukeQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "硫海遗爵");
            Description = this.GetLocalization(nameof(Description), () => "击败硫海遗爵");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_OldDuke;
            Position = new Vector2(0, 150);
            AddParent<PolterghastQuest>();
            
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败硫海遗爵"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "CorrodedFossil").Type,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个腐蚀化石")
            });
        }

        public override void UpdateByPlayer() {
            //检查硫海遗爵是否被击败
            bool isDowned = InWorldBossPhase.Downed26.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class DevourerofGodsQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "神明吞噬者");
            Description = this.GetLocalization(nameof(Description), () => "击败神明吞噬者");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_DevourerofGodsHead;
            Position = new Vector2(150, 0);
            AddParent<PolterghastQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败神明吞噬者"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_CosmiliteBar,
                Amount = 30,
                Description = this.GetLocalization("QuestReward.Description", () => "30个神宇金锭")
            });
        }

        public override void UpdateByPlayer() {
            //检查神明吞噬者是否被击败
            bool isDowned = InWorldBossPhase.Downed27.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class YharonQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "丛林龙,犽戎");
            Description = this.GetLocalization(nameof(Description), () => "击败丛林龙,犽戎");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Yharon;
            Position = new Vector2(150, 0);
            AddParent<DevourerofGodsQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败丛林龙,犽戎"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_AuricBar,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个圣金源锭")
            });
        }

        public override void UpdateByPlayer() {
            //检查丛林龙是否被击败
            bool isDowned = InWorldBossPhase.Downed28.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ExoMechsQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星流巨械");
            Description = this.GetLocalization(nameof(Description), () => "击败星流巨械");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_ThanatosHead;
            Position = new Vector2(150, -100);
            AddParent<YharonQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败星流巨械"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_ExoPrism,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个星流棱晶")
            });
        }

        public override void UpdateByPlayer() {
            //检查星流巨械是否被击败
            bool isDowned = InWorldBossPhase.Downed29.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SupremeCalamitasQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "至尊灾厄");
            Description = this.GetLocalization(nameof(Description), () => "击败至尊灾厄");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_SupremeCalamitas;
            Position = new Vector2(150, 100);
            AddParent<YharonQuest>();
            
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败至尊灾厄"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_AshesofAnnihilation,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个灭世余灰")
            });
        }

        public override void UpdateByPlayer() {
            //检查至尊灾厄是否被击败
            bool isDowned = InWorldBossPhase.Downed30.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PrimordialWyrmQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "始源妖龙");
            Description = this.GetLocalization(nameof(Description), () => "击败始源妖龙");

            IconType = QuestIconType.NPC;
            //使用ModContent查找NPCID
            IconNPCType = ModContent.Find<ModNPC>("CalamityMod", "PrimordialWyrmHead").Type;
            Position = new Vector2(0, 200);
            AddParent<YharonQuest>();
            
            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败始源妖龙"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ModContent.Find<ModItem>("CalamityMod", "HalibutCannon").Type,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "大比目鱼炮")
            });
        }

        public override void UpdateByPlayer() {
            //检查始源妖龙是否被击败
            bool isDowned = InWorldBossPhase.Downed31.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
