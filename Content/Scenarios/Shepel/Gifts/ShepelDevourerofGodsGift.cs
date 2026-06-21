using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelDevourerofGodsGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_DevourerofGodsHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "能够吞噬神明的巨兽，最终也只是倒在了您的脚下。这片星空的动荡总算是平息了。");
            L1 = this.GetLocalization(nameof(L1), () => "这种高强度的跨维度追击战，一定让您感到疲惫了吧？请先收起武器，稍微放松一下肩膀。");
            L2 = this.GetLocalization(nameof(L2), () => "至于战利品，我已经从那些扭曲的空间残骸中，为您提取并适配好了最高效的穿透模组。");
            L3 = this.GetLocalization(nameof(L3), () => "繁杂的武器升级配置我会处理妥当的。现在，请允许我为您准备一杯茶，我们该休息了。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<QuantumFrameModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.DevourerofGodsGift, d => d.DevourerofGodsGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.DevourerofGodsGift = true, d => d.DevourerofGodsGift = true);
    }
}
