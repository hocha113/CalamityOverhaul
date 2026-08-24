using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //讨伐篇事件带：入侵与月类事件，统一挂在 y=600 一排，x 随所属进度阶段的主线节点走

    internal class BloodMoonQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "血色之夜");
            Description = this.GetLocalization(nameof(Description), () => "经历一次血月");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "月亮变红的晚上，怪物不再敲门，它们直接开门。活过这一夜，天亮见。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.BloodMoonStarter;
            Position = new Vector2(0, 300);
            AddParent<KingSlimeQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "经历一次血月"),
                RequiredProgress = 1
            });

            AddReward(ItemID.BloodMoonStarter, 1);
            AddReward(ItemID.HealingPotion, 5);
        }

        public override void UpdateByPlayer() {
            if (Main.bloodMoon) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class TorchGodQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "火把神");
            Description = this.GetLocalization(nameof(Description), () => "通过火把神的试炼");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "在地下插满一百支火把，火焰的主人会亲自考验你。熄灭的火光里藏着它的恩宠。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.TorchGodsFavor;
            Position = new Vector2(-150, 300);
            AddParent<KingSlimeQuest>();
            HiddenUntilUnlocked = true;

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获得火把神的恩宠"),
                RequiredProgress = 1
            });

            AddReward(ItemID.Torch, 999);
        }

        //恩宠使用后消失，用玩家身上的持久标志做触发与判定
        protected override bool HiddenTriggerMet() => Main.LocalPlayer.unlockedBiomeTorches;

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.unlockedBiomeTorches) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class GoblinArmyQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "哥布林入侵");
            Description = this.GetLocalization(nameof(Description), () => "击退哥布林军队");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "打碎暗影珠会引来它们，人多了也会。矮小、成群、没完没了，但打赢了能捡到一位会修装备的俘虏。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GoblinBattleStandard;
            Position = new Vector2(0, 300);
            AddParent<EyeofCthulhuQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击退哥布林军队"),
                RequiredProgress = 1
            });

            AddReward(ItemID.SpikyBall, 99);
            AddReward(ItemID.GoldCoin, 2);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedGoblins;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class OldOnesArmyQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "旧日军团");
            Description = this.GetLocalization(nameof(Description), () => "守住埃特尼亚水晶，击退旧日军团三个阶段");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "酒馆老板带来了异世界的求援。摆好水晶，守住传送门，别让任何东西碰到它。三个梯队，一波比一波狠。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.DD2ElderCrystal;
            Position = new Vector2(0, 300);
            AddParent<EaterofWorldsQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.T1", () => "击退旧日军团 一阶"),
                RequiredProgress = 1
            });
            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.T2", () => "击退旧日军团 二阶"),
                RequiredProgress = 1
            });
            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.T3", () => "击退旧日军团 三阶"),
                RequiredProgress = 1
            });

            AddReward(ItemID.DefenderMedal, 25);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = DD2Event.DownedInvasionT1 ? 1 : 0;
            Objectives[1].CurrentProgress = DD2Event.DownedInvasionT2 ? 1 : 0;
            Objectives[2].CurrentProgress = DD2Event.DownedInvasionT3 ? 1 : 0;
            if (Objectives[0].IsCompleted && Objectives[1].IsCompleted && Objectives[2].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class PirateInvasionQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "海盗入侵");
            Description = this.GetLocalization(nameof(Description), () => "击退海盗入侵");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "世界变硬之后，海平面上开始出现帆影。他们抢钱，你抢回来，顺便连船长的家当一起。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.PirateMap;
            Position = new Vector2(0, 300);
            AddParent<WallofFleshQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击退海盗入侵"),
                RequiredProgress = 1
            });

            AddReward(ItemID.PirateMap, 1);
            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedPirates;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class SolarEclipseQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "日食讨伐");
            Description = this.GetLocalization(nameof(Description), () => "在日食中击杀蛾怪");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "月亮挡住太阳的日子，白天比夜晚更危险。恐怖电影里的东西全都上街了，其中飞得最快的那只叫蛾怪。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.SolarTablet;
            Position = new Vector2(0, 300);
            AddParent<PlanteraQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击杀蛾怪"),
                RequiredProgress = 1
            });

            AddReward(ItemID.SolarTablet, 1);
            AddReward(ItemID.GoldCoin, 5);
        }

        public override void OnKillByNPC(NPC npc) {
            if (npc.type == NPCID.Mothron) {
                Objectives[0].CurrentProgress = 1;
            }
        }

        public override void UpdateByPlayer() {
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class MartianMadnessQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "火星暴乱");
            Description = this.GetLocalization(nameof(Description), () => "击退火星人入侵");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "别盯着天上的探测器看，它会回家叫人。飞碟、死光、脑控头盔，全套外星入侵的排场。");

            IconType = QuestIconType.NPC;
            IconNPCType = NPCID.MartianSaucerCore;
            Position = new Vector2(0, 300);
            AddParent<GolemQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击退火星人入侵"),
                RequiredProgress = 1
            });

            AddReward(ItemID.GoldCoin, 15);
        }

        public override void UpdateByPlayer() {
            bool isDowned = NPC.downedMartians;
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class PumpkinMoonQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "南瓜月");
            Description = this.GetLocalization(nameof(Description), () => "在南瓜月中击败南瓜王");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "用南瓜月勋章唤醒万圣夜。稻草人排着队来收人头，压轴登场的是提着双镰的南瓜王。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.PumpkinMoonMedallion;
            Position = new Vector2(150, 300);
            AddParent<GolemQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败南瓜王"),
                RequiredProgress = 1
            });

            AddReward(ItemID.PumpkinMoonMedallion, 1);
            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.VDownedV8.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class FrostMoonQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "霜月");
            Description = this.GetLocalization(nameof(Description), () => "在霜月中击败冰雪女王");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "拆开那份不该拆的礼物，圣诞夜就变了味。坦克在雪地里巡逻，女王在暴雪里俯冲。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.NaughtyPresent;
            Position = new Vector2(150, 0);
            AddParent<PumpkinMoonQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "击败冰雪女王"),
                RequiredProgress = 1
            });

            AddReward(ItemID.NaughtyPresent, 1);
            AddReward(ItemID.GoldCoin, 10);
        }

        public override void UpdateByPlayer() {
            bool isDowned = InWorldBossPhase.VDownedV9.Invoke();
            Objectives[0].CurrentProgress = isDowned ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
