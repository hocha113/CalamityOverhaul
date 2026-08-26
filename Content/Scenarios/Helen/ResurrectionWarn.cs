using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen
{
    internal sealed class ResurrectionWarn : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";
        public static LocalizedText Line0 { get; private set; }
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

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            Line0 = this.GetLocalization(nameof(Line0), () => "等等，你感觉到了吗？");
            Line1 = this.GetLocalization(nameof(Line1), () => "复苏状态正在接近危险临界点");
            Line2 = this.GetLocalization(nameof(Line2), () => "这不是闹着玩的");
            Line3 = this.GetLocalization(nameof(Line3), () => "当完全复苏时......后果会很严重");
            Line4 = this.GetLocalization(nameof(Line4), () => "那将会是被深渊吞噬的结局");
            Line5 = this.GetLocalization(nameof(Line5), () => "我体内那些眼睛，每睁开一个，复苏速度就会加快");
            Line6 = this.GetLocalization(nameof(Line6), () => "睁开的越多，积累越快，危险越大");
            Line7 = this.GetLocalization(nameof(Line7), () => "这也是驱使深渊力量的代价，没开启眼睛的我，实力会大打折扣");
            Line8 = this.GetLocalization(nameof(Line8), () => "我们需要学会权衡，战斗时开启多少眼睛才是安全的");
            Line9 = this.GetLocalization(nameof(Line9), () => "如果想要无代价使用这些力量......");
            Line10 = this.GetLocalization(nameof(Line10), () => "就必须想办法让那些眼睛死机");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Solemn", Line0.Value)
             .Say("Helen", "Solemn", Line1.Value)
             .Say("Helen", "Solemn", Line2.Value)
             .Say("Helen", "Solemn", Line3.Value)
             .Say("Helen", "Solemn", Line4.Value)
             .Say("Helen", "Solemn", Line5.Value)
             .Say("Helen", "Solemn", Line6.Value)
             .Say("Helen", "Solemn", Line7.Value)
             .Say("Helen", "Solemn", Line8.Value)
             .Say("Helen", "Solemn", Line9.Value)
             .Say("Helen", "Solemn", Line10.Value);
        }

        protected override void OnStarted() {
            HalibutAtlas.Instance?.Open();
            if (Main.LocalPlayer.TryGetOverride(out HalibutPlayer halibutPlayer)) {
                halibutPlayer.CloseEyes();
            }
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => HalibutStorySync.ReadHalibut(d => d.FirstResurrectionWarning, d => d.FirstResurrectionWarning),
            CanTrigger = (_, player) => {
                if (!HalibutStorySync.ReadHalibut(d => d.FirstMet, d => d.FirstMet)) {
                    return false;
                }

                var resurrectionSystem = player.GetOverride<HalibutPlayer>().ResurrectionSystem;
                return resurrectionSystem != null && resurrectionSystem.Ratio >= 0.7f;
            },
            OnCompleted = _ => HalibutStorySync.WriteHalibut(d => d.FirstResurrectionWarning = true, d => d.FirstResurrectionWarning = true),
        };
    }
}
