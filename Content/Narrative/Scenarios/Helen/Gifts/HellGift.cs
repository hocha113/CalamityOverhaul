using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen.Gifts
{
    internal sealed class HellGift : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "地狱这个鬼地方是越来越热了");
            L1 = this.GetLocalization(nameof(L1), () => "我有个提议，把海底挖穿，让这里也变成海洋的一部分");
            L2 = this.GetLocalization(nameof(L2), () => "跨越整块大陆的地热温泉......");
            L3 = this.GetLocalization(nameof(L3), () => "想想就很舒服");
            L4 = this.GetLocalization(nameof(L4), () => "哦，对了，我逮到了一条鱼");
            L5 = this.GetLocalization(nameof(L5), () => "最开始我以为它是来地狱泡温泉的向导，结果它只是长得像");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "SlightAnnoyed", L0.Value)
             .Say("Helen", "SlightAnnoyed", L1.Value)
             .Say("Helen", "Enjoy", L2.Value)
             .Say("Helen", "Enjoy2", L3.Value)
             .Say("Helen", L4.Value)
             .SayReward("Helen", L5.Value, ItemID.GuideVoodooFish, title: string.Empty);
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => HalibutStorySync.ReadGift(d => d.HellGift, d => d.HellGift),
            CanTrigger = (_, player) => {
                if (!NPC.downedMoonlord) {
                    return false;
                }

                return player.TryGetOverride<HalibutPlayer>(out HalibutPlayer halibutPlayer)
                    && halibutPlayer.HasHalubut
                    && player.ZoneUnderworldHeight;
            },
            OnCompleted = _ => HalibutStorySync.WriteGift(d => d.HellGift = true, d => d.HellGift = true),
        };
    }
}
