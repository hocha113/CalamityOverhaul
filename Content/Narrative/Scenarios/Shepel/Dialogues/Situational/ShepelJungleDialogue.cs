using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues.Situational
{
    internal sealed class ShepelJungleDialogue : ShepelSituationalNarrative
    {
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VHard_Line1 { get; private set; }
        public static LocalizedText VHard_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "丛林的生物信号密度是所有地表区域里最高的，我的扫描模块有些应付不过来。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "主人在这里打架，背景噪音对我来说真的很嘈杂。不是抱怨，只是说明情况。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "这片区域的菌丝网络走向挺特别的，某些连接模式和我的神经路由有点相似。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "纯技术性描述，不是在夸它。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "丛林湿度持续刷新记录，某几个外部传感器不太开心。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "功能正常，只是汇报一下状态。");
            VHard_Line1 = this.GetLocalization(nameof(VHard_Line1),
                () => "主人，进入困难模式之后丛林的生物信号混乱程度上升了不止一个量级。");
            VHard_Line2 = this.GetLocalization(nameof(VHard_Line2),
                () => "植物会主动攻击这件事，我依然觉得在设计上有些过激。请注意周围。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => player.ZoneJungle;

        protected override void Build(NarrativeComposer n) {
            int total = Main.hardMode ? 4 : 3;
            int v = ShepelStorySync.TakeVariantSeed(
                d => d.JungleVariantSeed,
                (d, seed) => d.JungleVariantSeed = seed,
                d => d.JungleVariantSeed,
                (d, seed) => d.JungleVariantSeed = seed,
                total);

            switch (v) {
                case 0: n.Say("SHPC", V0_Line1.Value).Say("SHPC", V0_Line2.Value); break;
                case 1: n.Say("SHPC", V1_Line1.Value).Say("SHPC", V1_Line2.Value); break;
                case 2: n.Say("SHPC", V2_Line1.Value).Say("SHPC", V2_Line2.Value); break;
                default: n.Say("SHPC", VHard_Line1.Value).Say("SHPC", VHard_Line2.Value); break;
            }
        }
    }
}
