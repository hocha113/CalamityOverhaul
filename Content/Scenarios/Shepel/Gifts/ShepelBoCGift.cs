using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelBoCGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.BrainofCthulhu;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "和那种喜欢在脑子里制造幻觉的怪物交手，一定让您头晕脑胀了吧？真是个不讲礼貌的家伙。");
            L1 = this.GetLocalization(nameof(L1), () => "您的指尖还在轻微地发抖呢，已经为您调整了链接处的微电流，请稍微放松一下吧。");
            L2 = this.GetLocalization(nameof(L2), () => "至于那颗讨厌的大脑，我借用了它遗留下来的神经结晶，做了一点小小的材质改良。");
            L3 = this.GetLocalization(nameof(L3), () => "这是一个全新的防滑握把。有了它，哪怕是在精神紧绷的战斗中，武器也能安安稳稳地贴合您的掌心。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<CrystalGripModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitSmirk);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.BrainOfCthulhuGift, d => d.BrainOfCthulhuGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.BrainOfCthulhuGift = true, d => d.BrainOfCthulhuGift = true);
    }
}
