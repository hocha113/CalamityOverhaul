using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Everyday
{
    internal sealed class DyeProtest : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "老实说，我并不喜欢那些鲜艳的颜色");
            L2 = this.GetLocalization(nameof(L2), () => "洗掉好吗？放染缸里，然后用那个水桶");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Solemn", L1.Value)
             .Say("Helen", "Solemn", L2.Value);
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => HalibutStorySync.ReadHalibut(d => d.DyeProtest, d => d.DyeProtest),
            CanTrigger = (_, player) => {
                if (!player.TryGetOverride(out HalibutPlayer halibutPlayer) || !halibutPlayer.HeldHalibut) {
                    return false;
                }

                Item item = player.GetItem();
                return item.type == HalibutOverride.ID && item.CWR().DyeItemID > ItemID.None;
            },
            OnCompleted = _ => HalibutStorySync.WriteHalibut(d => d.DyeProtest = true, d => d.DyeProtest = true),
        };
    }
}
