using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.UIHandles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest.YharonQuest
{
    /// <summary>鬼面刀任务UI</summary>
    internal class YharonQuestUI : BaseQuestAcceptUI
    {
        public override string LocalizationCategory => "ADV.SupCal";
        public static YharonQuestUI Instance => UIHandleLoader.GetUIHandleOfType<YharonQuestUI>();

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：焚世龙");
            QuestDesc = this.GetLocalization(nameof(QuestDesc), () => "使用鬼面刀击杀焚世之龙");
            AcceptText = this.GetLocalization(nameof(AcceptText), () => "接受");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
        }

        protected override bool ShouldShowQuest() {
            if (HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestAccepted, d => d.SupCalYharonQuestAccepted)) {
                return false;
            }

            if (!HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward)) {
                return false;
            }

            Item heldItem = Main.LocalPlayer.GetItem();
            return heldItem.type == ModContent.ItemType<OniMachete>()
                && HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward)
                && !HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestReward, d => d.SupCalYharonQuestReward)
                && !HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestDeclined, d => d.SupCalYharonQuestDeclined);
        }

        protected override void OnQuestAccepted() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalYharonQuestAccepted = true,
                d => d.SupCalYharonQuestAccepted = true);
        }

        protected override void OnQuestDeclined() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalYharonQuestDeclined = true,
                d => d.SupCalYharonQuestDeclined = true);
        }
    }
}
