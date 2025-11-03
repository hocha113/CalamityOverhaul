using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts
{
    internal class HellGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(HellGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText R1 { get; private set; }
        public static LocalizedText L0;
        public static LocalizedText L1;
        public static LocalizedText L2;
        public static LocalizedText L3;
        public static LocalizedText L4;
        public static LocalizedText L5;
        private const string slightAnnoyed = " ";
        private const string enjoy = " " + " ";
        private const string enjoy2 = " " + " " + " ";
        public override void SetStaticDefaults() {
            R1 = this.GetLocalization(nameof(R1), () => "比目鱼");
            L0 = this.GetLocalization(nameof(L0), () => "地狱这个鬼地方是越来越热了");
            L1 = this.GetLocalization(nameof(L1), () => "我有个提议，把海底挖穿，让这里也变成海洋的一部分");
            L2 = this.GetLocalization(nameof(L2), () => "跨越整块大陆的地热温泉......");
            L3 = this.GetLocalization(nameof(L3), () => "想想就很舒服");
            L4 = this.GetLocalization(nameof(L4), () => "哦，对了，我逮到了一条鱼");
            L5 = this.GetLocalization(nameof(L5), () => "最开始我以为它是来地狱泡温泉的向导，结果它只是长得像");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(R1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + slightAnnoyed, ADVAsset.Helen_slightAnnoyedADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + slightAnnoyed, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + enjoy, ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + enjoy, silhouette: false);
            DialogueBoxBase.RegisterPortrait(R1.Value + enjoy2, ADVAsset.Helen_enjoy2ADV);
            DialogueBoxBase.SetPortraitStyle(R1.Value + enjoy2, silhouette: false);
            Add(R1.Value + slightAnnoyed, L0.Value);
            Add(R1.Value + slightAnnoyed, L1.Value);
            Add(R1.Value + enjoy, L2.Value);
            Add(R1.Value + enjoy2, L3.Value);
            Add(R1.Value, L4.Value, onComplete: Give);
            Add(R1.Value, L5.Value);
        }
        private void Give() {
            ADVRewardPopup.ShowReward(ItemID.GuideVoodooFish, 1, null, appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
        }
        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!NPC.downedMoonlord) {
                return;//月球领主后才触发
            }
            if (CWRWorld.HasBoss) {
                return;//避免在不合适的时候触发
            }
            if (!halibutPlayer.HasHalubut) {
                return;//必须有比目鱼
            }
            if (!halibutPlayer.Player.ZoneUnderworldHeight) {
                return;//必须在地狱区域
            }
            if (save.HellGift) {
                return;//已经获得过奖励
            }
            if (StartScenario()) {
                save.HellGift = true;//标记已经获得奖励
            }
        }
    }
}
