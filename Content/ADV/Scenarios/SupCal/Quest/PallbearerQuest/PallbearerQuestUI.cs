using CalamityOverhaul.Content.ADV.ADVQuestTracker;
using CalamityOverhaul.Content.Items.Ranged;
using InnoVault.UIHandles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.PallbearerQuest
{
    /// <summary>
    /// 扶柩者任务UI
    /// </summary>
    internal class PallbearerQuestUI : BaseQuestAcceptUI
    {
        public override string LocalizationCategory => "ADV";
        public static PallbearerQuestUI Instance => UIHandleLoader.GetUIHandleOfType<PallbearerQuestUI>();

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：猎杀亵渎天神");
            QuestDesc = this.GetLocalization(nameof(QuestDesc), () => "使用扶柩者击杀亵渎天神");
            AcceptText = this.GetLocalization(nameof(AcceptText), () => "接受");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
        }

        protected override bool ShouldShowQuest() {
            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return false;
            }

            //如果玩家已经接受了任务（通过存档标记），就不再显示UI
            if (save.Get<SupCalADVData>().SupCalQuestAccepted) {
                return false;
            }

            Item heldItem = Main.LocalPlayer.GetItem();
            return heldItem.type == ModContent.ItemType<Pallbearer>()
                && save.Get<SupCalADVData>().SupCalMoonLordReward
                && !save.Get<SupCalADVData>().SupCalQuestReward
                && !save.Get<SupCalADVData>().SupCalQuestDeclined;
        }

        protected override void OnQuestAccepted() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<SupCalADVData>().SupCalQuestAccepted = true;
            }
        }

        protected override void OnQuestDeclined() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<SupCalADVData>().SupCalQuestDeclined = true;
            }
        }
    }
}
