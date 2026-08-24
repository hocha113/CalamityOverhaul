using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //里程碑荣誉墙：起点西南的三枚汇总任务，进度每帧从任务表汇算
    //统计只认"当前世界实际加载"的任务，缺席内容不计入分母

    internal abstract class MilestoneQuest : QuestNode
    {
        //FirstQuest 同款金色光环，标记里程碑身份
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            bool isNig = QuestLog.Instance.NightMode;
            Texture2D value = CWRAsset.SoftGlow.Value;
            Color color = Color.Gold with { A = 0 } * alpha;
            if (isNig) {
                color *= 0.3f;
            }
            for (int i = 0; i < 6; i++) {
                spriteBatch.Draw(value, drawPos, null, color, 0, value.Size() / 2, scale * (2 + i * 0.2f), SpriteEffects.None, 0);
            }
            return true;
        }

        /// <summary>按 ID 清单汇算，缺席节点跳过；返回值填进目标进度</summary>
        protected void TallyByIDs(string[] ids) {
            int total = 0;
            int done = 0;
            foreach (var id in ids) {
                var quest = GetQuest(id);
                if (quest == null) {
                    continue;
                }
                total++;
                if (quest.IsCompleted) {
                    done++;
                }
            }
            Objectives[0].RequiredProgress = Math.Max(total, 1);
            Objectives[0].CurrentProgress = done;
        }
    }

    internal class ConquerorMilestone : MilestoneQuest
    {
        private static readonly string[] BossQuestIDs = [
            nameof(KingSlimeQuest), nameof(EyeofCthulhuQuest), nameof(EaterofWorldsQuest),
            nameof(QueenBeeQuest), nameof(SkeletronQuest), nameof(WallofFleshQuest),
            nameof(QueenSlimeQuest), nameof(MechanicalBossesQuest), nameof(PlanteraQuest),
            nameof(GolemQuest), nameof(LunaticCultistQuest), nameof(MoonLordQuest),
            nameof(DeerclopsQuest), nameof(DukeFishronQuest), nameof(EmpressOfLightQuest),
            nameof(DesertScourgeQuest), nameof(CrabulonQuest), nameof(HiveMindQuest),
            nameof(SlimeGodQuest), nameof(AquaticScourgeQuest), nameof(CryogenQuest),
            nameof(BrimstoneElementalQuest), nameof(CalamitasCloneQuest), nameof(PlaguebringerGoliathQuest),
            nameof(ProfanedGuardiansQuest), nameof(DragonfollyQuest), nameof(ProvidenceQuest),
            nameof(StormWeaverQuest), nameof(CeaselessVoidQuest), nameof(SignusQuest),
            nameof(PolterghastQuest), nameof(OldDukeQuest), nameof(DevourerofGodsQuest),
            nameof(YharonQuest), nameof(ExoMechsQuest), nameof(SupremeCalamitasQuest),
            nameof(PrimordialWyrmQuest),
            nameof(GiantClamQuest), nameof(CragmawMireQuest), nameof(GreatSandSharkQuest),
            nameof(LeviathanQuest), nameof(AstrumAureusQuest), nameof(AstrumDeusQuest),
            nameof(RavagerQuest), nameof(MaulerQuest), nameof(NuclearTerrorQuest)
        ];

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "讨伐编年史");
            Description = this.GetLocalization(nameof(Description), () => "击败这个世界的每一位强敌");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "从史莱姆王到世界尽头的存在，把每一场硬仗都打完。写满这一页的人不多。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GoldCrown;
            Position = new Vector2(-450, 900);
            AddParent<FirstQuest>();

            QuestType = QuestType.Achievement;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "完成所有讨伐任务"),
                RequiredProgress = 1
            });

            AddReward(ItemID.PlatinumCoin, 3);
        }

        public override void UpdateByPlayer() {
            TallyByIDs(BossQuestIDs);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class EventMilestone : MilestoneQuest
    {
        private static readonly string[] EventQuestIDs = [
            nameof(GoblinArmyQuest), nameof(BloodMoonQuest), nameof(PirateInvasionQuest),
            nameof(SolarEclipseQuest), nameof(OldOnesArmyQuest), nameof(MartianMadnessQuest),
            nameof(PumpkinMoonQuest), nameof(FrostMoonQuest)
        ];

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "事件平定者");
            Description = this.GetLocalization(nameof(Description), () => "平定每一场入侵与异象");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "军队、舰队、异界的月亮。凡是成群结队来的，都被你送了回去。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.PartyHat;
            Position = new Vector2(-300, 900);
            AddParent<FirstQuest>();

            QuestType = QuestType.Achievement;
            Difficulty = QuestDifficulty.Expert;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "完成所有事件任务"),
                RequiredProgress = 1
            });

            AddReward(ItemID.PlatinumCoin, 2);
        }

        public override void UpdateByPlayer() {
            TallyByIDs(EventQuestIDs);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CompletionistMilestone : MilestoneQuest
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "完典");
            Description = this.GetLocalization(nameof(Description), () => "完成任务书上的一切");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "这本书的最后一页留给读完全书的人。合上它的时候，这个世界已经没有你没见过的东西了。");

            IconTexturePath = "CalamityOverhaul/icon_small";
            Position = new Vector2(-150, 900);
            AddParent<FirstQuest>();

            QuestType = QuestType.Achievement;
            Difficulty = QuestDifficulty.Master;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "完成所有任务"),
                RequiredProgress = 1
            });

            AddReward(ItemID.PlatinumCoin, 5);
        }

        public override void UpdateByPlayer() {
            //隐藏彩蛋不计入分母，缺席内容自然不在表里
            int total = 0;
            int done = 0;
            foreach (var quest in AllQuests) {
                if (quest == this || quest.HiddenUntilUnlocked) {
                    continue;
                }
                total++;
                if (quest.IsCompleted) {
                    done++;
                }
            }
            Objectives[0].RequiredProgress = Math.Max(total, 1);
            Objectives[0].CurrentProgress = done;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
