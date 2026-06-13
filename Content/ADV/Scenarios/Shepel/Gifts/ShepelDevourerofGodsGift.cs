using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    internal class ShepelDevourerofGodsGift : ShepelGiftScenarioBase
    {
        public override string Key => nameof(ShepelDevourerofGodsGift);
        public override int TargetBossID => CWRID.NPC_DevourerofGodsHead;
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            L0 = this.GetLocalization(nameof(L0), () => "能够吞噬神明的巨兽，最终也只是倒在了您的脚下。这片星空的动荡总算是平息了。");
            L1 = this.GetLocalization(nameof(L1), () => "这种高强度的跨维度追击战，一定让您感到疲惫了吧？请先收起武器，稍微放松一下肩膀。");
            L2 = this.GetLocalization(nameof(L2), () => "至于战利品，我已经从那些扭曲的空间残骸中，为您提取并适配好了最高效的穿透模组。");
            L3 = this.GetLocalization(nameof(L3), () => "繁杂的武器升级配置我会处理妥当的。现在，请允许我为您准备一杯茶，我们该休息了。");
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
                ADVRewardPopup.ShowReward(ModContent.ItemType<QuantumFrameModule>(), 1, null,
                    appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty)
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.SHPC);
            }
        }

        protected override bool IsGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DevourerofGodsGift;
        protected override void MarkGiftCompleted(ADVSave save) => save.Get<ShepelGiftData>().DevourerofGodsGift = true;
        protected override bool StartScenarioInternal() => ScenarioManager.Start<ShepelDevourerofGodsGift>();
    }
}
