using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.UIHandles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.Quest.DoGQuest
{
    /// <summary>
    /// 神明吞噬者任务UI
    /// </summary>
    internal class DoGQuestUI : BaseQuestAcceptUI
    {
        public override string LocalizationCategory => "ADV";
        public static DoGQuestUI Instance => UIHandleLoader.GetUIHandleOfType<DoGQuestUI>();

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：神明吞噬者");
            QuestDesc = this.GetLocalization(nameof(QuestDesc), () => "使用刻心者击杀神明吞噬者");
            AcceptText = this.GetLocalization(nameof(AcceptText), () => "接受");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
        }

        protected override bool ShouldShowQuest() {
            if (HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward)
                || HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestAccepted, d => d.SupCalDoGQuestAccepted)
                || HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestDeclined, d => d.SupCalDoGQuestDeclined)) {
                return false;
            }

            if (!HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward)) {
                return false;
            }

            Item heldItem = Main.LocalPlayer.GetItem();
            return heldItem.type == ModContent.ItemType<Heartcarver>()
                && HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward)
                && !HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward)
                && !HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestDeclined, d => d.SupCalDoGQuestDeclined);
        }

        protected override void OnQuestAccepted() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalDoGQuestAccepted = true,
                d => d.SupCalDoGQuestAccepted = true);
        }

        protected override void OnQuestDeclined() {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalDoGQuestDeclined = true,
                d => d.SupCalDoGQuestDeclined = true);
        }
    }
}
