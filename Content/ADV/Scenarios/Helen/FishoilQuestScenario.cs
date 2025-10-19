using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen
{
    /// <summary>
    /// 比目鱼鱼油获取提示与任务创建场景
    /// </summary>
    internal class FishoilQuestScenario : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FishoilQuestScenario);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static bool Spwand;
        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");
            Line0 = this.GetLocalization(nameof(Line0), () => "你最近好像捕到了不少普通的鱼");
            Line1 = this.GetLocalization(nameof(Line1), () => "给我一批做实验，我可以提炼一瓶新鲜的鱼油");
            Line2 = this.GetLocalization(nameof(Line2), () => "过程不难但很枯燥");
            Line3 = this.GetLocalization(nameof(Line3), () => "鱼油很有潜力,比你想的更有用");
            Line4 = this.GetLocalization(nameof(Line4), () => "愿意吗?");
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "可以");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "没兴趣");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

            Add(Rolename.Value, Line0.Value);
            Add(Rolename.Value, Line1.Value);
            Add(Rolename.Value, Line2.Value);
            Add(Rolename.Value, Line3.Value);
            AddWithChoices(Rolename.Value, Line4.Value, new System.Collections.Generic.List<Choice>{
                new Choice(ChoiceAccept.Value, OnAccept, enabled: true),
                new Choice(ChoiceDecline.Value, OnDecline, enabled: true)
            });
        }

        private void OnAccept() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.FishoilQuestAccepted = true;
                FishoilQuestUI.Instance.OpenPersistent();
            }
            Complete();
        }

        private void OnDecline() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.FishoilQuestDeclined = true;
            }
            Complete();
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!NPC.downedQueenBee) {
                Spwand = false;
                return;//必须击败蜂后
            }

            if (!Spwand) {
                return;
            }
            if (!save.FirstMet) {
                return;
            }
            if (save.FishoilQuestAccepted || save.FishoilQuestDeclined) {
                return;
            }

            bool hasFish = false;
            for (int i = 0; i < Main.LocalPlayer.inventory.Length; i++) {
                if (Main.LocalPlayer.inventory[i].type == Terraria.ID.ItemID.Bass) {
                    hasFish = true;
                    break; 
                }
            }

            if (!hasFish) {
                return;
            }

            if (ScenarioManager.Start<FishoilQuestScenario>()) {
                if (!HalibutUIHead.Instance.Open) {
                    HalibutUIHead.Instance.Open = true;
                    Spwand = false;
                }
            }
        }
    }
}
