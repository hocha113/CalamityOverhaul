using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class HiveMindGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(HiveMindGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一个由腐败构成的集体意识……真是让人不适的概念");
            L1 = this.GetLocalization(nameof(L1), () => "它的思维方式一定很特别，无数腐烂的碎片拼凑成一个扭曲的整体");
            L2 = this.GetLocalization(nameof(L2), () => "给，腐烂鱼。从那堆腐肉里捞出来的，别问我怎么做到的");
            L3 = this.GetLocalization(nameof(L3), () => "虽然闻起来像是被遗忘在阳光下三天的海鲜");
            L4 = this.GetLocalization(nameof(L4), () => "但据说它能让人产生一种……与腐败共鸣的感觉。听起来就不太对劲");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            Add(R1.Value, L0.Value);
            Add(R1.Value, L1.Value);
            Add(R1.Value, L2.Value); //奖励
            Add(R1.Value, L3.Value);
            Add(R1.Value, L4.Value);
        }
        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ItemID.EaterofPlankton, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!halibutPlayer.HeldHalibut) {
                return;
            }
            if (!save.FirstMet) {
                return;//必须先触发过初次见面
            }
            if (save.HiveMindGift) {
                return;
            }
            if (!InWorldBossPhase.Downed3.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<HiveMindGift>()) {
                save.HiveMindGift = true;
            }
        }
    }
}
