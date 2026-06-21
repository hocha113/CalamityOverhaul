using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Situational
{
    internal sealed class ShepelDungeonDialogue : ShepelSituationalNarrative
    {
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VNight_Line1 { get; private set; }
        public static LocalizedText VNight_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "地牢内部，残留能量扰乱了扫描信号，一直在处理噪声。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "总有一种被注视的感觉。也许是数据干扰，也许不是。主人多留意。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "地牢的设计者对采光需求明显没什么兴趣。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "走廊宽度、叉路密度，防守设计挺严密的。旧主人还是有想法的。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "书架……这里有书架，非常多。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "扫描了一部分，内容大多已经腐蚀。遗憾。");
            VNight_Line1 = this.GetLocalization(nameof(VNight_Line1),
                () => "地牢加上夜晚，主人选的时机真是……挺有品位的。");
            VNight_Line2 = this.GetLocalization(nameof(VNight_Line2),
                () => "没关系，我的视野不依赖光照。我会看着。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => player.ZoneDungeon;

        protected override void Build(NarrativeComposer n) {
            int total = !Main.dayTime ? 4 : 3;
            int v = ShepelStorySync.TakeVariantSeed(
                d => d.DungeonVariantSeed,
                (d, seed) => d.DungeonVariantSeed = seed,
                d => d.DungeonVariantSeed,
                (d, seed) => d.DungeonVariantSeed = seed,
                total);

            switch (v) {
                case 0: n.Say("SHPC", V0_Line1.Value).Say("SHPC", V0_Line2.Value); break;
                case 1: n.Say("SHPC", V1_Line1.Value).Say("SHPC", V1_Line2.Value); break;
                case 2: n.Say("SHPC", V2_Line1.Value).Say("SHPC", V2_Line2.Value); break;
                default: n.Say("SHPC", VNight_Line1.Value).Say("SHPC", VNight_Line2.Value); break;
            }
        }
    }
}
