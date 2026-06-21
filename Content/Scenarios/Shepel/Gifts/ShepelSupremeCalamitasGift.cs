using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using CalamityOverhaul.Content.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelSupremeCalamitasGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_SupremeCalamitas;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "焚海的魔女……终于落幕了。主人的各项生命体征一切正常，没有致命伤。");
            L1 = this.GetLocalization(nameof(L1), () => "确认全域威胁解除。呼……请允许我暂时挂起战斗协议，执行一次深度的系统自检。");
            L2 = this.GetLocalization(nameof(L2), () => "我收集了散落的混沌等离子，为您制作了这个最高效的能量模组。");
            L3 = this.GetLocalization(nameof(L3), () => "漫长的战役终于结束了。主人，接下来的和平时光，也请让我继续作为女仆服侍您。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<PlasmaInjectorModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitHappy);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.SupremeCalamitasGift, d => d.SupremeCalamitasGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.SupremeCalamitasGift = true, d => d.SupremeCalamitasGift = true);
    }
}
