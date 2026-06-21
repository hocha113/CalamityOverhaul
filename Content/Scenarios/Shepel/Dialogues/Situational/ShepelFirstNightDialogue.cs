using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Situational
{
    internal sealed class ShepelFirstNightDialogue : ShepelSituationalNarrative
    {
        public override int DialoguePriority => 40;

        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText V3_Line1 { get; private set; }
        public static LocalizedText V3_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "夜幕落下了，夜间的威胁密度远超白天，请注意周围。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "我的视野不依赖光照，我看得清。主人呢，看得清吗？");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "今晚的星象数据比较完整，我正在运行例行分析。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "没什么要紧的，只是习惯了有事做。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "夜晚安静了许多，相对的。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "越安静的时候越需要警惕，主人。");
            V3_Line1 = this.GetLocalization(nameof(V3_Line1),
                () => "都这么晚了，主人有计划今晚去哪吗。");
            V3_Line2 = this.GetLocalization(nameof(V3_Line2),
                () => "只是问问，不是催。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => !Main.dayTime;

        protected override void Build(NarrativeComposer n) {
            int v = ShepelStorySync.TakeVariantSeed(
                d => d.NightVariantSeed,
                (d, seed) => d.NightVariantSeed = seed,
                d => d.NightVariantSeed,
                (d, seed) => d.NightVariantSeed = seed,
                4);

            switch (v) {
                case 0: n.Say("SHPC", V0_Line1.Value).Say("SHPC", V0_Line2.Value); break;
                case 1: n.Say("SHPC", V1_Line1.Value).Say("SHPC", V1_Line2.Value); break;
                case 2: n.Say("SHPC", V2_Line1.Value).Say("SHPC", V2_Line2.Value); break;
                default: n.Say("SHPC", V3_Line1.Value).Say("SHPC", V3_Line2.Value); break;
            }
        }
    }
}
