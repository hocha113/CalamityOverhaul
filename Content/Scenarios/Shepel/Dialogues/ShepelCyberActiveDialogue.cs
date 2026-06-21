using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Composition;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues
{
    internal sealed class ShepelCyberActiveDialogue : ShepelSituationalNarrative
    {
        public override int DialoguePriority => 10;

        public static LocalizedText Line_Intro { get; private set; }
        public static LocalizedText Line_LayerReport { get; private set; }
        public static LocalizedText Line_Warning { get; private set; }
        public static LocalizedText Line_MaxLayer { get; private set; }

        public override void SetStaticDefaults() {
            Line_Intro = this.GetLocalization(nameof(Line_Intro),
                () => "主人，赛博空间已展开，当前层级 {0}。外部信号已被完全隔绝，这里现在是只属于我们的安全领域。");
            Line_LayerReport = this.GetLocalization(nameof(Line_LayerReport),
                () => "领域越深，我能为您清除的阻碍就越多。但请留意RAM的消耗。");
            Line_Warning = this.GetLocalization(nameof(Line_Warning),
                () => "主人请警戒，领域边缘出现异常波动，请保持在我的掩护范围内。");
            Line_MaxLayer = this.GetLocalization(nameof(Line_MaxLayer),
                () => "主人，我们已抵达最深处。前方即是黑墙边界，危险性极高……请握紧我的手，千万不要松开。");
        }

        protected override bool CheckConditions(Player player, ShepelStoryData data)
            => Cyberspace.Active;

        protected override void Build(NarrativeComposer n) {
            int layer = Cyberspace.CurrentLayer;
            bool isMaxLayer = layer >= Cyberspace.MaxLayerCount;
            string introText = string.Format(Line_Intro.Value, layer);

            n.Say("SHPC", introText);

            if (isMaxLayer) {
                n.Say("SHPC", Line_MaxLayer.Value);
            }
            else {
                n.Say("SHPC", Line_LayerReport.Value)
                 .Say("SHPC", Line_Warning.Value);
            }
        }
    }
}
