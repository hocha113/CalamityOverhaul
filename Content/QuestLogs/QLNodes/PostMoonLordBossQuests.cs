using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class ProfanedGuardiansQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.ProfanedGuardians;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "亵渎使徒");
            Description = this.GetLocalization(nameof(Description), () => "击败亵渎使徒");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_ProfanedGuardianCommander;
            Position = new Vector2(150, 0);
            AddParent<MoonLordQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败亵渎使徒"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "UnholyEssence", out ModItem unholyEssenceItem)) {
                AddReward(unholyEssenceItem.Type, 30);
            }
        }

        public override void UpdateByPlayer() {
            //亵渎使徒击杀
            bool isDowned = InWorldBossPhase.Downed17.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class DragonfollyQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Dragonfolly;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "痴愚金龙");
            Description = this.GetLocalization(nameof(Description), () => "击败痴愚金龙");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Dragonfolly;
            Position = new Vector2(0, 150);
            AddParent<MoonLordQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败痴愚金龙"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "EffulgentFeather", out ModItem effulgentFeatherItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = effulgentFeatherItem.Type,
                    Amount = 10
                });
            }
        }

        public override void UpdateByPlayer() {
            //痴愚金龙击杀
            bool isDowned = InWorldBossPhase.Downed18.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ProvidenceQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Providence;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "亵渎天神");
            Description = this.GetLocalization(nameof(Description), () => "击败亵渎天神");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Providence;
            Position = new Vector2(150, 0);
            if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败亵渎天神"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_DivineGeode,
                Amount = 20
            });
        }

        public override void UpdateByPlayer() {
            //亵渎天神击杀
            bool isDowned = InWorldBossPhase.Downed19.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class StormWeaverQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.StormWeaver;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "风暴编织者");
            Description = this.GetLocalization(nameof(Description), () => "击败风暴编织者");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_StormWeaverHead;
            Position = new Vector2(0, -150);
            if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败风暴编织者"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "ArmoredShell", out ModItem armoredShellItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = armoredShellItem.Type,
                    Amount = 5
                });
            }
        }

        public override void UpdateByPlayer() {
            //风暴编织者击杀
            bool isDowned = InWorldBossPhase.Downed21.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CeaselessVoidQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.CeaselessVoid;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "无尽虚空");
            Description = this.GetLocalization(nameof(Description), () => "击败无尽虚空");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_CeaselessVoid;
            Position = new Vector2(0, 150);
            if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败无尽虚空"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "DarkPlasma", out ModItem darkPlasmaItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = darkPlasmaItem.Type,
                    Amount = 5
                });
            }
        }

        public override void UpdateByPlayer() {
            //无尽虚空击杀
            bool isDowned = InWorldBossPhase.Downed20.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SignusQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Signus;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "西格纳斯");
            Description = this.GetLocalization(nameof(Description), () => "击败西格纳斯");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Signus;
            Position = new Vector2(75, 150);
            if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败西格纳斯"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "TwistingNether", out ModItem twistingNetherItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = twistingNetherItem.Type,
                    Amount = 5
                });
            }
        }

        public override void UpdateByPlayer() {
            //西格纳斯击杀
            bool isDowned = InWorldBossPhase.Downed22.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PolterghastQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Polterghast;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "噬魂幽花");
            Description = this.GetLocalization(nameof(Description), () => "击败噬魂幽花");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Polterghast;
            Position = new Vector2(150, 0);
            if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败噬魂幽花"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "RuinousSoul", out ModItem ruinousSoulItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = ruinousSoulItem.Type,
                    Amount = 10
                });
            }
        }

        public override void UpdateByPlayer() {
            //噬魂幽花击杀
            bool isDowned = InWorldBossPhase.Downed23.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class OldDukeQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.OldDuke;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "硫海遗爵");
            Description = this.GetLocalization(nameof(Description), () => "击败硫海遗爵");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_OldDuke;
            Position = new Vector2(0, 150);
            if (BossQuestAvailability.Polterghast) {
                AddParent<PolterghastQuest>();
            }
            else if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败硫海遗爵"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "CorrodedFossil", out ModItem corrodedFossilItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = corrodedFossilItem.Type,
                    Amount = 20
                });
            }
        }

        public override void UpdateByPlayer() {
            //硫海遗爵击杀
            bool isDowned = InWorldBossPhase.Downed26.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class DevourerofGodsQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.DevourerofGods;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "神明吞噬者");
            Description = this.GetLocalization(nameof(Description), () => "击败神明吞噬者");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_DevourerofGodsHead;
            Position = new Vector2(150, 0);
            if (BossQuestAvailability.Polterghast) {
                AddParent<PolterghastQuest>();
            }
            else if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败神明吞噬者"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_CosmiliteBar,
                Amount = 30
            });
        }

        public override void UpdateByPlayer() {
            //神明吞噬者击杀
            bool isDowned = InWorldBossPhase.Downed27.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class YharonQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.Yharon;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "丛林龙,犽戎");
            Description = this.GetLocalization(nameof(Description), () => "击败丛林龙,犽戎");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Yharon;
            Position = new Vector2(150, 0);
            if (BossQuestAvailability.DevourerofGods) {
                AddParent<DevourerofGodsQuest>();
            }
            else if (BossQuestAvailability.Polterghast) {
                AddParent<PolterghastQuest>();
            }
            else if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败丛林龙,犽戎"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_AuricBar,
                Amount = 20
            });
        }

        public override void UpdateByPlayer() {
            //丛林龙击杀
            bool isDowned = InWorldBossPhase.Downed28.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ExoMechsQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.ExoMechs;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星流巨械");
            Description = this.GetLocalization(nameof(Description), () => "击败星流巨械");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_ThanatosHead;
            Position = new Vector2(150, -100);
            if (BossQuestAvailability.Yharon) {
                AddParent<YharonQuest>();
            }
            else if (BossQuestAvailability.DevourerofGods) {
                AddParent<DevourerofGodsQuest>();
            }
            else if (BossQuestAvailability.Polterghast) {
                AddParent<PolterghastQuest>();
            }
            else if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败星流巨械"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_ExoPrism,
                Amount = 20
            });
        }

        public override void UpdateByPlayer() {
            //星流巨械击杀
            bool isDowned = InWorldBossPhase.Downed29.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SupremeCalamitasQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.SupremeCalamitas;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "至尊灾厄");
            Description = this.GetLocalization(nameof(Description), () => "击败至尊灾厄");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_SupremeCalamitas;
            Position = new Vector2(150, 100);
            if (BossQuestAvailability.Yharon) {
                AddParent<YharonQuest>();
            }
            else if (BossQuestAvailability.DevourerofGods) {
                AddParent<DevourerofGodsQuest>();
            }
            else if (BossQuestAvailability.Polterghast) {
                AddParent<PolterghastQuest>();
            }
            else if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败至尊灾厄"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_AshesofAnnihilation,
                Amount = 20
            });
        }

        public override void UpdateByPlayer() {
            //至尊灾厄击杀
            bool isDowned = InWorldBossPhase.Downed30.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PrimordialWyrmQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && BossQuestAvailability.PrimordialWyrm;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "始源妖龙");
            Description = this.GetLocalization(nameof(Description), () => "击败始源妖龙");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_PrimordialWyrmHead;
            Position = new Vector2(0, 200);
            if (BossQuestAvailability.Yharon) {
                AddParent<YharonQuest>();
            }
            else if (BossQuestAvailability.DevourerofGods) {
                AddParent<DevourerofGodsQuest>();
            }
            else if (BossQuestAvailability.Polterghast) {
                AddParent<PolterghastQuest>();
            }
            else if (BossQuestAvailability.Providence) {
                AddParent<ProvidenceQuest>();
            }
            else if (BossQuestAvailability.ProfanedGuardians) {
                AddParent<ProfanedGuardiansQuest>();
            }
            else {
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败始源妖龙"),
                RequiredProgress = 1
            });

            if (ModContent.TryFind("CalamityMod", "HalibutCannon", out ModItem halibutCannonItem)) {
                Rewards.Add(new QuestReward {
                    ItemType = halibutCannonItem.Type,
                    Amount = 1
                });
            }
        }

        public override void UpdateByPlayer() {
            //始源妖龙击杀
            bool isDowned = InWorldBossPhase.Downed31.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
