using CalamityOverhaul.Content.Scenarios.Draedon.PQCDs;
using CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Services;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    internal sealed class QuestCompleteScenario : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Draedon";
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1), () => "所有量子节点已上线，网络运行正常");
            Line2 = this.GetLocalization(nameof(Line2), () => "数据传输延迟与理论值一致，没有偏差");
            Line3 = this.GetLocalization(nameof(Line3), () => "你的表现比我预期更快，值得肯定");
            Line4 = this.GetLocalization(nameof(Line4), () => "作为报酬，我将开放部分设备与权限，你可以自行选择使用");
            Line5 = this.GetLocalization(nameof(Line5), () => "如果需要协助，可以通过通讯网络直接联系我");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Draedon", "Red", Line1.Value)
             .Say("Draedon", Line2.Value)
             .Say("Draedon", Line3.Value)
             .Say("Draedon", Line4.Value, onEnter: GiveReward)
             .Say("Draedon", Line5.Value);
        }

        protected override void OnStarted() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnCompleted() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            DraedonStorySync.WriteDraedon(
                d => d.DeploySignaltowerQuestCompleted = true,
                d => d.DeploySignaltowerQuestCompleted = true);
            DSTPlayer.HasDeploySignaltowerQuestByWorld = false;
            FirstMetTzeentch.Open();
        }

        private static void GiveReward() {
            NarrativeServices.RewardGrant?.Grant(new RewardPayload {
                ItemType = ModContent.ItemType<PQCD>(),
                Stack = 1
            }, Main.LocalPlayer);
        }
    }
}
