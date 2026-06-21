using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelWoFGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => NPCID.WallofFlesh;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "肉墙倒塌后，地壳结构和能量基线都发生了剧烈的震荡。");
            L1 = this.GetLocalization(nameof(L1), () => "世界正在进入一个更危险的阶段。但同时，新的稀有材料也开始大量涌现。");
            L2 = this.GetLocalization(nameof(L2), () => "我用那些新出现的合金为您升级了握持系统，这算是我们跨入新阶段的第一步。");
            L3 = this.GetLocalization(nameof(L3), () => "无论外部环境发生怎样的异变，我的护卫协议永远不会动摇。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<HarmonyGripModule>(), title: string.Empty)
             .Say("SHPC", L3.Value);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.WallOfFleshGift, d => d.WallOfFleshGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.WallOfFleshGift = true, d => d.WallOfFleshGift = true);
    }
}
