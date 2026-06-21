using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    internal sealed class CryogenGift : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText R1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_Cryogen;

        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一团被冰冻的灵魂......");
            L1 = this.GetLocalization(nameof(L1), () => "在深渊的档案馆里，这种低温通常被用来给那些疯子保鲜");
            L2 = this.GetLocalization(nameof(L2), () => "霜鲦鱼，从冰晶碎片里解冻出来的。它还保持着被冻结时的姿态");
            L3 = this.GetLocalization(nameof(L3), () => "据说吃了它会让你的血液降温，思维变得异常清醒");
            L4 = this.GetLocalization(nameof(L4), () => "吃多了或许会成为一个冰雕");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", L0.Value)
             .Say("Helen", "Stern", L1.Value)
             .SayReward("Helen", L2.Value, ItemID.FrostMinnow, title: string.Empty)
             .Say("Helen", L3.Value)
             .Say("Helen", L4.Value);
        }

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.CryogenGift, d => d.CryogenGift);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.CryogenGift = true, d => d.CryogenGift = true);
    }
}
