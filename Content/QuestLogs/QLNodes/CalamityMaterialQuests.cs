using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //灾厄阶段材料任务：挂讨伐篇 y=150 材料带，x 对齐所属阶段
    //目标文本走 CollectItem/ObtainItem 模板，物品名自动取灾厄本地化
    //父节点可能因个别内容缺失而不在，一律带常驻回退并保持绝对坐标一致

    internal class AerialiteQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_AerialiteBar > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "天蓝之辉");
            Description = this.GetLocalization(nameof(Description), () => "锻造天蓝锭");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "荒漠灾虫死后，天上会落下淡蓝色的矿尘。它轻得能浮起来，却比铁更结实。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_AerialiteBar;
            if (BossQuestAvailability.DesertScourge) {
                Position = new Vector2(0, -150);
                AddParent<DesertScourgeQuest>();
            }
            else {
                Position = new Vector2(-150, -150);
                AddParent<EyeofCthulhuQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            AddCollectObjective(15, CWRID.Item_AerialiteBar);

            AddReward(ItemID.SwiftnessPotion, 5);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_AerialiteBar);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CryonicQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_CryonicBar > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "极寒结晶");
            Description = this.GetLocalization(nameof(Description), () => "锻造极寒神锭");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "冰灵碎裂后，雪原深处的坚冰里开始析出蓝色的矿脉。握在手里不化，反而更冷。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_CryonicBar;
            if (BossQuestAvailability.Cryogen) {
                Position = new Vector2(-150, -300);
                AddParent<CryogenQuest>();
            }
            else {
                Position = new Vector2(-150, -150);
                AddParent<WallofFleshQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddCollectObjective(15, CWRID.Item_CryonicBar);

            AddReward(ItemID.WarmthPotion, 5);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_CryonicBar);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class PerennialQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_PerennialBar > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "常青之赐");
            Description = this.GetLocalization(nameof(Description), () => "锻造常青锭");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "世纪之花凋谢的地方，泥土里长出了会呼吸的绿色金属。丛林在用自己的方式回礼。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_PerennialBar;
            Position = new Vector2(0, -150);
            AddParent<PlanteraQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddCollectObjective(15, CWRID.Item_PerennialBar);

            AddReward(ItemID.LifeFruit, 3);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_PerennialBar);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class LifeAlloyQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_LifeAlloy > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "生命合金");
            Description = this.GetLocalization(nameof(Description), () => "熔铸生命合金");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "三种活着的金属熔在一起，锭子上还留着脉搏。高阶装备的骨架都用它打底。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_LifeAlloy;
            //(300,-150)落在(1800,150)，(150,-150)会与瘟疫使者重叠
            Position = new Vector2(300, -150);
            AddParent<PlanteraQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddCollectObjective(5, CWRID.Item_LifeAlloy);

            AddReward(ItemID.LifeforcePotion, 5);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_LifeAlloy);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class AstralBarQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_AstralBar > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星幻熔铸");
            Description = this.GetLocalization(nameof(Description), () => "锻造星幻锭");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "游龙死后，星染矿脉终于肯让镐子下嘴。熔出来的锭子在黑暗里自己发光。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_AstralBar;
            if (BossQuestAvailability.AstrumDeus) {
                Position = new Vector2(150, 0);
                AddParent<AstrumDeusQuest>();
            }
            else {
                Position = new Vector2(450, 450);
                AddParent<GolemQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddCollectObjective(15, CWRID.Item_AstralBar);

            AddReward(ItemID.WrathPotion, 5);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_AstralBar);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class UelibloomQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_UelibloomBar > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "悠兰绽放");
            Description = this.GetLocalization(nameof(Description), () => "锻造悠兰锭");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "女神的圣火烧过之后，丛林的泥土里开出了金属的花。花瓣熔成锭，锭里还带着光。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_UelibloomBar;
            if (BossQuestAvailability.Providence) {
                Position = new Vector2(150, -150);
                AddParent<ProvidenceQuest>();
            }
            else {
                Position = new Vector2(450, -150);
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddCollectObjective(15, CWRID.Item_UelibloomBar);

            AddReward(ItemID.ChlorophyteBar, 10);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_UelibloomBar);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class BloodstoneQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_BloodstoneCore > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "血石淬炼");
            Description = this.GetLocalization(nameof(Description), () => "凝聚血石核心");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "女神倒下后，硫磺火崖的岩浆里浮起暗红的石头。它们还在跳，像一颗颗离体的心脏。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_BloodstoneCore;
            if (BossQuestAvailability.Providence) {
                Position = new Vector2(300, -150);
                AddParent<ProvidenceQuest>();
            }
            else {
                Position = new Vector2(600, -150);
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddCollectObjective(20, CWRID.Item_BloodstoneCore);

            AddReward(ItemID.SuperHealingPotion, 10);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_BloodstoneCore);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class AscendantEssenceQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_AscendantSpiritEssence > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "升华之魄");
            Description = this.GetLocalization(nameof(Description), () => "凝聚升华精魄");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "幽魂、星尘、月光，再加一点不肯散去的执念。把它们压进一枚精魄里，足够点亮传说。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_AscendantSpiritEssence;
            if (BossQuestAvailability.DevourerofGods) {
                Position = new Vector2(150, -150);
                AddParent<DevourerofGodsQuest>();
            }
            else if (BossQuestAvailability.Polterghast) {
                Position = new Vector2(300, -150);
                AddParent<PolterghastQuest>();
            }
            else {
                Position = new Vector2(750, -150);
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddCollectObjective(5, CWRID.Item_AscendantSpiritEssence);

            AddReward(ItemID.SuperManaPotion, 10);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_AscendantSpiritEssence);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class YharonSoulQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_YharonSoulFragment > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "龙魂余烬");
            Description = this.GetLocalization(nameof(Description), () => "收集犽戎魂魄碎片");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "巨龙燃尽之后，火焰里剩下的不是灰，是一片片还在发烫的魂。捡起来的时候小心别被燎到心。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_YharonSoulFragment;
            if (BossQuestAvailability.Yharon) {
                Position = new Vector2(0, -300);
                AddParent<YharonQuest>();
            }
            else {
                Position = new Vector2(750, -300);
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddCollectObjective(15, CWRID.Item_YharonSoulFragment);

            AddReward(ItemID.PlatinumCoin, 2);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_YharonSoulFragment);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class FruitQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod)
            && CWRID.Item_SanguineTangerine > 0 && CWRID.Item_MiracleFruit > 0
            && CWRID.Item_TaintedCloudberry > 0 && CWRID.Item_SacredStrawberry > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "生命的馈赠");
            Description = this.GetLocalization(nameof(Description), () => "集齐四枚强化生命的果实");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "强敌的尸骸旁偶尔会结出奇异的果实。一共四种，每一种都能把你的生命推向新的上限。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_SacredStrawberry;
            if (BossQuestAvailability.Providence) {
                Position = new Vector2(0, -300);
                AddParent<ProvidenceQuest>();
            }
            else {
                Position = new Vector2(300, -300);
                AddParent<MoonLordQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective(CWRID.Item_SanguineTangerine);
            AddObtainObjective(CWRID.Item_MiracleFruit);
            AddObtainObjective(CWRID.Item_TaintedCloudberry);
            AddObtainObjective(CWRID.Item_SacredStrawberry);

            AddReward(ItemID.SuperHealingPotion, 15);
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            Objectives[0].CurrentProgress = player.InquireItem(CWRID.Item_SanguineTangerine);
            Objectives[1].CurrentProgress = player.InquireItem(CWRID.Item_MiracleFruit);
            Objectives[2].CurrentProgress = player.InquireItem(CWRID.Item_TaintedCloudberry);
            Objectives[3].CurrentProgress = player.InquireItem(CWRID.Item_SacredStrawberry);
            bool allDone = true;
            foreach (var objective in Objectives) {
                if (!objective.IsCompleted) {
                    allDone = false;
                    break;
                }
            }
            if (allDone && !IsCompleted) IsCompleted = true;
        }
    }

    internal class DraedonPowerCellQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_DraedonPowerCell > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "远古动力");
            Description = this.GetLocalization(nameof(Description), () => "收集动力电池");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "实验室的废墟里散落着还有余电的电池。这种能源密度远超你的发电机，值得研究。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_DraedonPowerCell;
            Position = new Vector2(150, 0);
            AddParent<TeslaTowerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddCollectObjective(10, CWRID.Item_DraedonPowerCell);

            AddReward(CWRID.Item_DubiousPlating, 10);
            AddReward(CWRID.Item_MysteriousCircuitry, 10);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_DraedonPowerCell);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class DraedonsForgeQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_DraedonsForge > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "巨匠熔炉");
            Description = this.GetLocalization(nameof(Description), () => "得到巨匠的熔炉");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "工业的尽头是一台什么都能熔的炉子。组装好它，你的工坊从此不再有做不出的东西。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_DraedonsForge;
            if (CWRID.Item_DraedonPowerCell > 0) {
                Position = new Vector2(150, 0);
                AddParent<DraedonPowerCellQuest>();
            }
            else {
                Position = new Vector2(300, 0);
                AddParent<TeslaTowerQuest>();
            }

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            AddReward(CWRID.Item_DraedonPowerCell, 50);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(CWRID.Item_DraedonsForge) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
