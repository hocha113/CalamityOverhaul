using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal class TerminusQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_Terminus > 0;

        public override bool IsChapterHub => true;

        public override int ChapterOrder => 50;

        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Terminus;
            //终末篇主轴钉在 x=-100:轴上依次是终末石→古恒石→终末之战→征战轮回,右翼终焉武器落 x=50,
            //与工业西缘(x=300 的太阳能/电容矩阵)拉开 250px;轴放 x=0 时右翼在 150,与电容矩阵仅 158px 起
            Position = new Vector2(-100, -300);
            AddParent<FirstQuest>();
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();

            AddReward(CWRID.Item_BloodOrb, 999);
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.InquireItem(CWRID.Item_Terminus);

            Objectives[0].CurrentProgress = currentOre;

            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class RockQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_Rock > 0;

        public override void SetStaticDefaults() {
            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Rock;
            Position = new Vector2(0, -150);
            if (CWRID.Item_Terminus > 0) {
                AddParent<TerminusQuest>();
            }
            else {
                AddParent<FirstQuest>();
            }
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Master;

            AddObtainObjective();
            //奖励已禁用
            //Rewards.Add(new QuestReward {
            //ItemType = CWRID.Item_,
            //Amount = 5,
            //Description = this.GetLocalization("QuestReward.Description", () => "五块古恒石")
            //});
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            int currentOre = player.InquireItem(CWRID.Item_Rock);
            Objectives[0].CurrentProgress = currentOre;

            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
