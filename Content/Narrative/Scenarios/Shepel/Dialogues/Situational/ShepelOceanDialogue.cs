using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Situational
{
    internal sealed class ShepelOceanDialogue : ShepelSituationalNarrative
    {
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VRain_Line1 { get; private set; }
        public static LocalizedText VRain_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "海洋区域。水下的声学环境与地表截然不同。我的某些模块挺喜欢这个频率的。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "如果有机会，我想完整扫描一次海底地形。不着急，以后再说。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "海水的温度分层很明显，表层和下方的深渊相差几十度。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "深渊方向的信号比上次又弱了一些，不知道是什么在干扰。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "这片海洋其实挺大的，主人有没有想过往最深处探索一下。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "我不是在催，只是提一嘴。");
            VRain_Line1 = this.GetLocalization(nameof(VRain_Line1),
                () => "下雨加海洋，海面现在相当嘈杂。");
            VRain_Line2 = this.GetLocalization(nameof(VRain_Line2),
                () => "往下潜进去反而安静了，挺有意思的对比。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => player.ZoneBeach;

        protected override void Build(NarrativeComposer n) {
            int total = Main.raining ? 4 : 3;
            int v = ShepelStorySync.TakeVariantSeed(
                d => d.OceanVariantSeed,
                (d, seed) => d.OceanVariantSeed = seed,
                d => d.OceanVariantSeed,
                (d, seed) => d.OceanVariantSeed = seed,
                total);

            switch (v) {
                case 0: n.Say("SHPC", V0_Line1.Value).Say("SHPC", V0_Line2.Value); break;
                case 1: n.Say("SHPC", V1_Line1.Value).Say("SHPC", V1_Line2.Value); break;
                case 2: n.Say("SHPC", V2_Line1.Value).Say("SHPC", V2_Line2.Value); break;
                default: n.Say("SHPC", VRain_Line1.Value).Say("SHPC", VRain_Line2.Value); break;
            }
        }
    }
}
