using CalamityOverhaul.Content.ADV.Scenarios.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.YharonQuest
{
    /// <summary>
    /// 鬼面刀任务UI
    /// </summary>
    internal class YharonQuestUI : BaseQuestAcceptUI
    {
        public override string LocalizationCategory => "ADV";
        public static YharonQuestUI Instance => UIHandleLoader.GetUIHandleOfType<YharonQuestUI>();

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：焚世龙");
            QuestDesc = this.GetLocalization(nameof(QuestDesc), () => "使用鬼面刀击杀焚世之龙");
            AcceptText = this.GetLocalization(nameof(AcceptText), () => "接受");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
        }

        protected override bool ShouldShowQuest() {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }

            //如果玩家已经接受/拒绝/完成了任务，就不再显示UI
            if (halibutPlayer.ADCSave.SupCalYharonQuestAccepted) {
                return false;
            }

            //前置任务必须完成
            if (!halibutPlayer.ADCSave.SupCalDoGQuestReward) {
                return false;
            }

            Item heldItem = Main.LocalPlayer.GetItem();
            return heldItem.type == ModContent.ItemType<OniMachete>()
                && halibutPlayer.ADCSave.SupCalDoGQuestReward
                && !halibutPlayer.ADCSave.SupCalYharonQuestReward
                && !halibutPlayer.ADCSave.SupCalYharonQuestDeclined;
        }

        protected override void OnQuestAccepted() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.SupCalYharonQuestAccepted = true;
            }
        }

        protected override void OnQuestDeclined() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.SupCalYharonQuestDeclined = true;
            }
        }
    }
}
