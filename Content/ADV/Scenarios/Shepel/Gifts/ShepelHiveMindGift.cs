using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelHiveMindGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelHiveMindGift);
        public override int TargetBossID => CWRID.NPC_HiveMind;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "那个大脑散发的波动，带有强烈的精神干扰。主人，您的脑波频率刚才有些异常。");
            L1 = this.GetLocalization(nameof(L1), () => "请深呼吸，将注意力集中在我的系统提示音上，屏蔽掉那些亵渎的低语。");
            L2 = this.GetLocalization(nameof(L2), () => "我把那种干扰波动进行了反向编译。现在，您的攻击也能附带撕裂敌方护盾的高频震荡。");
            L3 = this.GetLocalization(nameof(L3), () => "危机已解除。回去之后，请允许我为您泡一杯安神的茶。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            Add(RoleName.Value, L0.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.None));
            Add(RoleName.Value, L1.Value);
            Add(RoleName.Value, L2.Value);
            Add(RoleName.Value, L3.Value, onComplete: Complete);
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            if (args.Index == 2) {
                ADVRewardPopup.ShowReward(ModContent.ItemType<OscillatorBarrelModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().HiveMindGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().HiveMindGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelHiveMindGift>();
    }
}
