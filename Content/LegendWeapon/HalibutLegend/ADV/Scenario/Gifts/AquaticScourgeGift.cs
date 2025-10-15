﻿using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario.Gifts
{
    internal class AquaticScourgeGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(AquaticScourgeGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "它让我想起家乡那些不太友好的邻居，不过它倒是挺友善的，它在我曾经任职的硫磺海大学里担任过保安队长，经常和我打招呼");
            L1 = this.GetLocalization(nameof(L1), () => "硫酸海是个有趣的地方，那里的生物都在问同一个问题：'为什么我还活着？'");
            L2 = this.GetLocalization(nameof(L2), () => "棱镜鱼，它的鳞片能折射出你从未见过的颜色。主要是因为它们不该存在");
            L3 = this.GetLocalization(nameof(L3), () => "盯着它看会让你的视觉系统重启，有点像强制更新，但更痛苦");
            L4 = this.GetLocalization(nameof(L4), () => "最后在光线暗淡的地方使用，不然感觉会像嗑了似的");
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
                ADVRewardPopup.ShowReward(ItemID.Prismite, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
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
            if (save.AquaticScourgeGift) {
                return;
            }
            if (!InWorldBossPhase.Downed8.Invoke()) {
                return;
            }

            if (ScenarioManager.Start<AquaticScourgeGift>()) {
                save.AquaticScourgeGift = true;
            }
        }
    }
}
