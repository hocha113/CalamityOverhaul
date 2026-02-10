using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class KingSlimeQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "史莱姆王");
            Description = this.GetLocalization(nameof(Description), () => "击败史莱姆王");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.KingSlime;
            Position = new Vector2(0, 300);
            AddParent<FirstQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败史莱姆王"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldCoin,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "5金币")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedSlimeKing;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class DesertScourgeQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "荒漠灾虫");
            Description = this.GetLocalization(nameof(Description), () => "击败荒漠灾虫");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_DesertScourgeHead;
            Position = new Vector2(150, 0);
            AddParent<KingSlimeQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败荒漠灾虫"),
                RequiredProgress = 1
            });

            if (CWRRef.Has) {
                if (ModContent.TryFind("CalamityMod", "VictoryShard", out ModItem victoryShardItem)) {
                    AddReward(victoryShardItem.Type, 10, this.GetLocalization("QuestReward.Description", () => "10个胜利碎片"));
                }
            }
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed0.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class EyeofCthulhuQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "克苏鲁之眼");
            Description = this.GetLocalization(nameof(Description), () => "击败克苏鲁之眼");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.EyeofCthulhu;
            Position = new Vector2(150, 0);
            AddParent<DesertScourgeQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败克苏鲁之眼"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.DemoniteOre,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个魔矿/猩红矿")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedBoss1;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CrabulonQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "菌生蟹");
            Description = this.GetLocalization(nameof(Description), () => "击败菌生蟹");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Crabulon;
            Position = new Vector2(150, 0);
            AddParent<EyeofCthulhuQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败菌生蟹"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GlowingMushroom,
                Amount = 50,
                Description = this.GetLocalization("QuestReward.Description", () => "50个发光蘑菇")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed2.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class EaterofWorldsQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "世界吞噬者/克苏鲁之脑");
            Description = this.GetLocalization(nameof(Description), () => "击败世界吞噬者或克苏鲁之脑");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.EaterofWorldsHead;
            Position = new Vector2(150, 0);
            AddParent<CrabulonQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败世界吞噬者或克苏鲁之脑"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.ShadowScale,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个暗影鳞片/组织样本")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedBoss2;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class QueenBeeQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "蜂后");
            Description = this.GetLocalization(nameof(Description), () => "击败蜂后");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.QueenBee;
            Position = new Vector2(0, 150);
            AddParent<EaterofWorldsQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败蜂后"),
                RequiredProgress = 1
            });

            AddReward(ItemID.BeeWax, 20);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedQueenBee;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class HiveMindQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "腐巢意志/血肉宿主");
            Description = this.GetLocalization(nameof(Description), () => "击败腐巢意志或血肉宿主");

            IconType = QuestIconType.Texture;
            IconTexturePath = "CalamityMod/NPCs/HiveMind/HiveMind_Head_Boss";
            Position = new Vector2(150, 0);
            AddParent<EaterofWorldsQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败腐巢意志或血肉宿主"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.DemoniteBar,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "10个魔矿锭/猩红矿锭")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed3.Invoke() || InWorldBossPhase.Downed4.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SkeletronQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "骷髅王");
            Description = this.GetLocalization(nameof(Description), () => "击败骷髅王");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.SkeletronHead;
            Position = new Vector2(150, 0);
            AddParent<HiveMindQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败骷髅王"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Bone,
                Amount = 50,
                Description = this.GetLocalization("QuestReward.Description", () => "50个骨头")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedBoss3;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SlimeGodQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "史莱姆之神");
            Description = this.GetLocalization(nameof(Description), () => "击败史莱姆之神");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_SlimeGodCore;
            Position = new Vector2(150, 0);
            AddParent<SkeletronQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败史莱姆之神"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Gel,
                Amount = 1000,
                Description = this.GetLocalization("QuestReward.Description", () => "1000个凝胶")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed5.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class WallofFleshQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "血肉墙");
            Description = this.GetLocalization(nameof(Description), () => "击败血肉墙");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.WallofFlesh;
            Position = new Vector2(150, 0);
            AddParent<SlimeGodQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败血肉墙"),
                RequiredProgress = 1
            });

            AddReward(ItemID.SoulofLight, 20);
            AddReward(ItemID.SoulofNight, 20);
        }

        public override void UpdateByPlayer() {
            bool isDowned = Main.hardMode;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    // --- 困难模式任务 ---

    public class QueenSlimeQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "史莱姆皇后");
            Description = this.GetLocalization(nameof(Description), () => "击败史莱姆皇后");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.QueenSlimeMask;
            Position = new Vector2(150, 150);
            AddParent<WallofFleshQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败史莱姆皇后"),
                RequiredProgress = 1
            });

            AddReward(ItemID.QueenSlimeMountSaddle);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedQueenSlime;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class AquaticScourgeQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "渊海灾虫");
            Description = this.GetLocalization(nameof(Description), () => "击败渊海灾虫");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_AquaticScourgeHead;
            Position = new Vector2(0, -150);
            AddParent<WallofFleshQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败渊海灾虫"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_CorrodedFossil,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "酸蚀化石")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed8.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CryogenQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "极地冰灵");
            Description = this.GetLocalization(nameof(Description), () => "击败极地冰灵");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_Cryogen;
            Position = new Vector2(0, 150);
            AddParent<WallofFleshQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败极地冰灵"),
                RequiredProgress = 1
            });

            if (CWRRef.Has) {
                if (ModContent.TryFind("CalamityMod", "EssenceofEleum", out ModItem essenceOfEleumItem)) {
                    AddReward(essenceOfEleumItem.Type, 20);
                }
            }
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed6.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class MechanicalBossesQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "机械三王");
            Description = this.GetLocalization(nameof(Description), () => "击败所有机械Boss");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.MechanicalSkull;
            Position = new Vector2(150, 0);
            AddParent<WallofFleshQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败所有机械Boss"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.HallowedBar,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个神圣锭")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.DownedAnyMechBoss;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class BrimstoneElementalQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "硫磺火元素");
            Description = this.GetLocalization(nameof(Description), () => "击败硫磺火元素");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_BrimstoneElemental;
            Position = new Vector2(150, -150);
            AddParent<WallofFleshQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败硫磺火元素"),
                RequiredProgress = 1
            });

            if (CWRRef.Has) {
                if (ModContent.TryFind("CalamityMod", "EssenceofHavoc", out ModItem essenceOfHavocItem)) {
                    AddReward(essenceOfHavocItem.Type, 20);
                }
            }
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed7.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PlanteraQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "世纪之花");
            Description = this.GetLocalization(nameof(Description), () => "击败世纪之花");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.Plantera;
            Position = new Vector2(150, 0);
            AddParent<MechanicalBossesQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败世纪之花"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.ChlorophyteOre,
                Amount = 50,
                Description = this.GetLocalization("QuestReward.Description", () => "50个叶绿矿")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.VDownedV7.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CalamitasCloneQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "灾厄之影");
            Description = this.GetLocalization(nameof(Description), () => "击败灾厄之影");

            IconType = QuestIconType.NPC;
            IconNPCType = CWRID.NPC_CalamitasClone;
            Position = new Vector2(0, 150);
            AddParent<PlanteraQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败灾厄之影"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_AshesofCalamity,
                Amount = 100,
                Description = this.GetLocalization("QuestReward.Description", () => "100个灾厄尘")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed10.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class GolemQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "石巨人");
            Description = this.GetLocalization(nameof(Description), () => "击败石巨人");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.Golem;
            Position = new Vector2(150, 0);
            AddParent<PlanteraQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败石巨人"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.BeetleHusk,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "10个甲虫外壳")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.DownedV7.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class PlaguebringerGoliathQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "瘟疫使者歌莉娅");
            Description = this.GetLocalization(nameof(Description), () => "击败瘟疫使者歌莉娅");

            IconTexturePath = "CalamityMod/NPCs/PlaguebringerGoliath/PlaguebringerGoliath_BossChecklist";//歌莉娅的纹理比较特殊，这里手动设置纹理路径
            Position = new Vector2(0, -150);
            AddParent<GolemQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败瘟疫使者歌莉娅"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = CWRID.Item_PlagueCellCanister,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "20个瘟疫细胞罐")
            });
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.Downed14.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class LunaticCultistQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "拜月教邪教徒");
            Description = this.GetLocalization(nameof(Description), () => "击败拜月教邪教徒");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.CultistBoss;
            Position = new Vector2(150, 0);
            AddParent<GolemQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败拜月教邪教徒"),
                RequiredProgress = 1
            });

            AddReward(ItemID.FragmentSolar, 5);
            AddReward(ItemID.FragmentVortex, 5);
            AddReward(ItemID.FragmentNebula, 5);
            AddReward(ItemID.FragmentStardust, 5);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.DownedV8.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class MoonLordQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "月球领主");
            Description = this.GetLocalization(nameof(Description), () => "击败月球领主");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.MoonLordHead;
            Position = new Vector2(150, 0);
            AddParent<LunaticCultistQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败月球领主"),
                RequiredProgress = 1
            });

            AddReward(ItemID.LunarOre, 100);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.VDownedV16.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
