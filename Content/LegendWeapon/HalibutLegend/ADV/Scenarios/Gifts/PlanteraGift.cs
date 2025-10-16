﻿using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.Gifts
{
    internal class PlanteraGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(PlanteraGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "一朵花……如果'花'这个词可以用来形容那种会吃人的藤蔓聚合体");
            L1 = this.GetLocalization(nameof(L1), () => "丛林的愤怒以植物的形式生长。大自然的报复从来不讲道理");
            L2 = this.GetLocalization(nameof(L2), () => "双鳕鱼，它有两个头。丛林就像是陆地上的海洋，两者的东西不需要理由就能长出多余的部位");
            L3 = this.GetLocalization(nameof(L3), () => "据说两个头意味着双倍的智慧，但看起来它们只是在互相争论该往哪游");
            L4 = this.GetLocalization(nameof(L4), () => "就像减肥和食欲，永远在吵架，但谁也说服不了谁");
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
                ADVRewardPopup.ShowReward(ItemID.DoubleCod, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            if (save.PlanteraGift) {
                return;
            }
            if (!InWorldBossPhase.VDownedV7.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<PlanteraGift>()) {
                save.PlanteraGift = true;
            }
        }
    }
}