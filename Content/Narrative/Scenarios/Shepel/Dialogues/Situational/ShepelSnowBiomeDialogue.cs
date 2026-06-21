using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Situational
{
    internal sealed class ShepelSnowBiomeDialogue : ShepelSituationalNarrative
    {
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "低温区域。理论上低温对电子系统有益，但我并没有感受到性能提升。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "大概因为主人产生的战斗数据太多，省下的算力被瞬间占满了。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "冰晶的光折射数据非常漂亮，这不是评估报告，只是个人看法。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "若不是还有敌人要对付，这里其实挺适合待着的。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "雪地的环境噪音是所有地表区域里最低的。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "主人在这里的时候，我的传感器误报率也低了不少。不知道是不是因为这个原因。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => player.ZoneSnow;

        protected override void Build(NarrativeComposer n) {
            int v = ShepelStorySync.TakeVariantSeed(
                d => d.SnowVariantSeed,
                (d, seed) => d.SnowVariantSeed = seed,
                d => d.SnowVariantSeed,
                (d, seed) => d.SnowVariantSeed = seed,
                3);

            switch (v) {
                case 0: n.Say("SHPC", V0_Line1.Value).Say("SHPC", V0_Line2.Value); break;
                case 1: n.Say("SHPC", V1_Line1.Value).Say("SHPC", V1_Line2.Value); break;
                default: n.Say("SHPC", V2_Line1.Value).Say("SHPC", V2_Line2.Value); break;
            }
        }
    }
}
