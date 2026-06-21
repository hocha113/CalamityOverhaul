using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Presentation.Views;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    /// <summary>女巫告别场景</summary>
    internal sealed class WitchFarewell : NarrativeScenario, ILocalizedModType
    {
        public static bool SpawnPending;

        public string LocalizationCategory => "ADV.EternalBlazingNow";

        public static LocalizedText FarewellLine1 { get; private set; }
        public static LocalizedText FarewellLine2 { get; private set; }
        public static LocalizedText FarewellLine3 { get; private set; }
        public static LocalizedText FarewellLine4 { get; private set; }
        public static LocalizedText FarewellLine5 { get; private set; }
        public static LocalizedText FarewellLine6 { get; private set; }
        public static LocalizedText FarewellLine7 { get; private set; }
        public static LocalizedText FarewellLine8 { get; private set; }
        public static LocalizedText FarewellLine9 { get; private set; }
        public static LocalizedText FarewellLine10 { get; private set; }
        public static LocalizedText FarewellLine11 { get; private set; }

        private static bool removedHalibut;

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            FarewellLine1 = this.GetLocalization(nameof(FarewellLine1), () => "这漫长的一生里，我见过无数次黎明与终焉");
            FarewellLine2 = this.GetLocalization(nameof(FarewellLine2), () => "火焰吞噬时代，也照亮新的开始。我原以为，这次也不会例外");
            FarewellLine3 = this.GetLocalization(nameof(FarewellLine3), () => "没想到，在最后的路上，会有人同行");
            FarewellLine4 = this.GetLocalization(nameof(FarewellLine4), () => "对我来说，这样的结局……已经足够了");
            FarewellLine5 = this.GetLocalization(nameof(FarewellLine5), () => "你的存在，证明这片大地还没有真正枯竭");
            FarewellLine6 = this.GetLocalization(nameof(FarewellLine6), () => "我相信，你会走得比我更远");
            FarewellLine7 = this.GetLocalization(nameof(FarewellLine7), () => "而我，也终于可以停下来了");
            FarewellLine8 = this.GetLocalization(nameof(FarewellLine8), () => "不必回头看。前面还有更重要的事情等着你");
            FarewellLine9 = this.GetLocalization(nameof(FarewellLine9), () => "就当我在这场漫长的旅途中，终于抵达了属于自己的地方");
            FarewellLine10 = this.GetLocalization(nameof(FarewellLine10), () => "那么到这里，就足够了");
            FarewellLine11 = this.GetLocalization(nameof(FarewellLine11), () => "去吧，杂鱼");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCalFarewell", FarewellLine1.Value, onEnter: TriggerRedScreen)
             .Say("SupCalFarewell", FarewellLine2.Value)
             .Say("SupCalFarewell", FarewellLine3.Value)
             .Say("SupCalFarewell", FarewellLine4.Value)
             .Say("SupCalFarewell", FarewellLine5.Value)
             .Say("SupCalFarewell", FarewellLine6.Value)
             .Say("SupCalFarewell", FarewellLine7.Value)
             .Say("SupCalFarewell", FarewellLine8.Value)
             .Say("SupCalFarewell", FarewellLine9.Value)
             .Say("SupCalFarewell", FarewellLine10.Value)
             .Say("SupCalFarewell", FarewellLine11.Value, onEnter: ShowAchievement, onExit: FinalFade);
        }

        public static void RequestSpawn() => SpawnPending = true;

        public static void ResetWorldState() {
            SpawnPending = false;
            removedHalibut = false;
        }

        protected override void OnStarted() {
            EbnEffect.StartContraction();
            if (Main.LocalPlayer.HasHalibut()) {
                RemoveHalibutFromPlayer();
                removedHalibut = true;
            }

            DialoguePanelView.Instance?.ShowFullBodyPortrait<SupCalFullBodyPortrait>();
        }

        protected override void OnCompleted() {
            DialoguePanelView.Instance?.HideFullBodyPortrait();
            EbnEffect.IsActive = false;
            EbnEffect.ResetEffects();
            EbnEffect.StartEpilogueFadeIn();
            if (removedHalibut) {
                HelenEpilogue.RequestSpawn();
                removedHalibut = false;
            }
        }

        private static void RemoveHalibutFromPlayer() {
            Player player = Main.LocalPlayer;
            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == HalibutOverride.ID) {
                    player.inventory[i].TurnToAir();
                }
            }
        }

        private static void TriggerRedScreen() => EbnEffect.StartRedScreen();

        private static void FinalFade() => EbnEffect.FinalFadeOut = true;

        private static void ShowAchievement() {
            DialoguePanelView.GetPortraits<SupCalFullBodyPortrait>().StartBurning();
            CWRNpc.SetNPCLoot(CWRID.NPC_SupremeCalamitas);
            NotificationPopupSystem.Add(new EbnAchievementEntry(
                CWRAsset.icon_small.Value,
                EternalBlazingNow.AchievementTitle.Value,
                EternalBlazingNow.AchievementTooltip.Value));
            HalibutStorySync.WriteSupCal(
                d => d.EternalBlazingNow = true,
                d => d.EternalBlazingNow = true);
            EbnState.SendEbnSync(Main.LocalPlayer);
        }
    }
}
