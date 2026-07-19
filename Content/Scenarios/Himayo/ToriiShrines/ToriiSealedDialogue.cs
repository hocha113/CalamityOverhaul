using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 拔刀条件未满足时的风凉话：无名的低语暗示刀在等待什么，
    /// 由 <see cref="ToriiShrine"/> 的右键交互手动触发，可反复播放
    /// </summary>
    internal sealed class ToriiSealedDialogue : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Onikiri;

        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "（你握住刀柄，指节都攥白了——刀身却纹丝不动，像是和整片大地铸在了一起）");
            L2 = this.GetLocalization(nameof(L2), () => "（刀镡下传来一声几不可闻的叹息，仿佛浅眠的人翻了个身）");
            L3 = this.GetLocalization(nameof(L3), () => "（……现在还不行。这把刀似乎在等一场大火烧到尽头的那天）");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.System, L1.Value)
             .Say(NarrativeIds.System, L2.Value)
             .Say(NarrativeIds.System, L3.Value)
             .End();
        }

        //纯手动触发、永不判定完成：每次去拔没资格的刀都能听一遍
        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => false,
            CanTrigger = (_, _) => false,
        };
    }
}
