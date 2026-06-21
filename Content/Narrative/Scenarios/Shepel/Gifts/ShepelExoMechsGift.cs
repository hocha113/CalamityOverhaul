using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Gifts
{
    internal sealed class ShepelExoMechsGift : ShepelBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override int TargetBossId => CWRID.NPC_AresBody;

        public override void SetStaticDefaults() {
RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "结束了。那个被称为源头的存在，终于也倒在您的火力之下了。");
            L1 = this.GetLocalization(nameof(L1), () => "主人，您做到了。我的系统日志正在全速记录这一刻的数据，散热模块甚至有些超负荷了。");
            L2 = this.GetLocalization(nameof(L2), () => "我提取了星流泰坦的核心数据为您升级。现在，我是比它更高效的兵器，也是专属于您的造物。");
            L3 = this.GetLocalization(nameof(L3), () => "证明了这一点，我的存在才更有价值。今后也请继续使用我吧。");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("SHPC", L0.Value)
             .Say("SHPC", L1.Value)
             .SayReward("SHPC", L2.Value, ModContent.ItemType<HighVoltageCoreModule>(), title: string.Empty)
             .Say("SHPC", L3.Value, onEnter: PortraitBlank);
        }

        protected override bool IsGiftCompleted()
            => ShepelStorySync.ReadGift(d => d.ExoMechsGift, d => d.ExoMechsGift);

        protected override void MarkGiftCompleted()
            => ShepelStorySync.WriteGift(d => d.ExoMechsGift = true, d => d.ExoMechsGift = true);

        protected override bool AdditionalConditions(Player player)
            => !NPC.AnyNPCs(CWRID.NPC_Apollo)
            && !NPC.AnyNPCs(CWRID.NPC_Artemis)
            && !NPC.AnyNPCs(CWRID.NPC_ThanatosHead);
    }
}
