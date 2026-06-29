using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Composition;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    internal sealed class ShepelTwinsDialogue : ShepelReactiveNarrative
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1),
                            () => "机械双子眼已双双坠毁。它们的火力覆盖网已被完全撕裂。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "无论它们如何进行视觉共享和战术协同，也无法逃脱我对您的绝对聚焦。");
        }

        protected override bool CheckExtraConditions(Player player, ShepelStoryData data)
            => data.LastDefeatedBossNpcType == NPCID.Retinazer
                || data.LastDefeatedBossNpcType == NPCID.Spazmatism;

        protected override void Build(NarrativeComposer n) {
            ConsumeEvent();
            n
            .Say("SHPC", Line1.Value, onEnter: PortraitSerious)
            .Say("SHPC", Line2.Value, onEnter: PortraitSmirk);
        }
    }
}
