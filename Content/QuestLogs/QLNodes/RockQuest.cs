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
            Position = new Vector2(0, -300);
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
