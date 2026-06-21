using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    /// <summary>永恒燃烧的如今，比目鱼缺席的差分版本</summary>
    internal sealed class EternalBlazingNowNoHelen : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???硫火女巫???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "硫火女巫");

            Line1 = this.GetLocalization(nameof(Line1), () => "......能走到这里的人，已经许久没有出现了");
            Line2 = this.GetLocalization(nameof(Line2), () => "我不会死......不过，也差不多了");
            Line3 = this.GetLocalization(nameof(Line3), () => "你做得很好......或许，你真的是他口中那个值得等待的“时代唯一”");
            Line4 = this.GetLocalization(nameof(Line4), () => "所以，我有最后一件事，想拜托你");
            Line5 = this.GetLocalization(nameof(Line5), () => "只要这世间的过去与现在，还存有一缕硫磺火，“我”就不会消亡");
            Line6 = this.GetLocalization(nameof(Line6), () => "可我的意识，却会在这无尽的火海中被逐渐磨灭");
            Line7 = this.GetLocalization(nameof(Line7), () => "如果没有遇到你，我最多还能撑三十年");
            Line8 = this.GetLocalization(nameof(Line8), () => "这是唯一的办法");
            Line9 = this.GetLocalization(nameof(Line9), () => "当我的意识彻底消散，整个世界都会被焚尽");
            Line10 = this.GetLocalization(nameof(Line10), () => "况且，如果你想终结这个时代，凡人的躯壳太过脆弱");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCalShadow", Line1.Value)
             .Say("SupCalShadow", Line3.Value)
             .Say("SupCalShadow", Line4.Value)
             .Say("SupCalShadow", "BeTo", Line5.Value)
             .Say("SupCalShadow", Line6.Value)
             .Say("SupCalShadow", Line7.Value)
             .Say("SupCalShadow", "BeTo", Line8.Value)
             .Say("SupCalShadow", "BeTo", Line9.Value)
             .Say("SupCalShadow", "BeTo", Line10.Value);
        }

        protected override void OnStarted() {
            EbnEffect.IsActive = true;
            MusicToast.ShowMusic(
                title: "罪之楔",
                artist: "腐姬",
                albumCover: ADVAsset.FUJI,
                style: MusicToast.MusicStyle.RedNeon,
                displayDuration: 480);
        }

        protected override void OnCompleted() {
            HalibutStorySync.WriteSupCal(
                d => d.EternalBlazingNowChoice1 = true,
                d => d.EternalBlazingNowChoice1 = true);
            WitchFarewell.RequestSpawn();
        }
    }
}
