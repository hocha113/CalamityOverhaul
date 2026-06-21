using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.UIHandles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.Quest.PallbearerQuest
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
            if (HalibutStorySync.ReadSupCal(d => d.SupCalQuestAccepted, d => d.SupCalQuestAccepted)) {
                return false;
            }

            Item heldItem = Main.LocalPlayer.GetItem();
            return heldItem.type == ModContent.ItemType<Pallbearer>()
                && HalibutStorySync.ReadSupCal(d => d.SupCalMoonLordReward, d => d.SupCalMoonLordReward)
                && !HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward)
                && !HalibutStorySync.ReadSupCal(d => d.SupCalQuestDeclined, d => d.SupCalQuestDeclined);
        }

        protected override void OnQuestAccepted() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalQuestAccepted = true,
                d => d.SupCalQuestAccepted = true);
        }

        protected override void OnQuestDeclined() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalQuestDeclined = true,
                d => d.SupCalQuestDeclined = true);
        }
    }
}
