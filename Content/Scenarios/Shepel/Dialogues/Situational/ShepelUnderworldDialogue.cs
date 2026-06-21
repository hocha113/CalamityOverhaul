using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Situational
{
    internal sealed class ShepelUnderworldDialogue : ShepelSituationalNarrative
    {
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VBloodMoon_Line1 { get; private set; }
        public static LocalizedText VBloodMoon_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "地底深处，热流读数超出了所有预设参数范围。这里的一切比我预想的要极端。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "某些外部传感器在过热警告中，不过我会撑住的。主人也注意别硬撑。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "岩浆作为照明方案，从能源效率角度来说极其浪费。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "不过地狱的居民大概不关心这个。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "这里的建筑痕迹比地表的地牢还要古老，在岩浆里泡了这么久也没被侵蚀。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "用的是什么材料？存档一下，以后有机会研究。");
            VBloodMoon_Line1 = this.GetLocalization(nameof(VBloodMoon_Line1),
                () => "血月加地狱，两套警报系统同时运行，有点忙。");
            VBloodMoon_Line2 = this.GetLocalization(nameof(VBloodMoon_Line2),
                () => "不过数据倒是很丰富。主人请多注意安全。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => player.ZoneUnderworldHeight;

        protected override void Build(NarrativeComposer n) {
            int total = Main.bloodMoon ? 4 : 3;
            int v = ShepelStorySync.TakeVariantSeed(
                d => d.UnderworldVariantSeed,
                (d, seed) => d.UnderworldVariantSeed = seed,
                d => d.UnderworldVariantSeed,
                (d, seed) => d.UnderworldVariantSeed = seed,
                total);

            switch (v) {
                case 0: n.Say("SHPC", V0_Line1.Value).Say("SHPC", V0_Line2.Value); break;
                case 1: n.Say("SHPC", V1_Line1.Value).Say("SHPC", V1_Line2.Value); break;
                case 2: n.Say("SHPC", V2_Line1.Value).Say("SHPC", V2_Line2.Value); break;
                default: n.Say("SHPC", VBloodMoon_Line1.Value).Say("SHPC", VBloodMoon_Line2.Value); break;
            }
        }
    }
}
