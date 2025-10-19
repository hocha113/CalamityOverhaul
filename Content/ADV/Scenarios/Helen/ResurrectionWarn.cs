using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen
{
    internal class ResurrectionWarn : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(ResurrectionWarn);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");
            Line0 = this.GetLocalization(nameof(Line0), () => "等等，你感觉到了吗？");
            Line1 = this.GetLocalization(nameof(Line1), () => "复苏状态正在接近危险临界点");
            Line2 = this.GetLocalization(nameof(Line2), () => "我必须警告你，这不是闹着玩的");
            Line3 = this.GetLocalization(nameof(Line3), () => "当完全复苏时......后果会很严重");
            Line4 = this.GetLocalization(nameof(Line4), () => "那将会是被深渊吞噬的结局");
            Line5 = this.GetLocalization(nameof(Line5), () => "我体内那些眼睛，每睁开一个，复苏速度就会加快");
            Line6 = this.GetLocalization(nameof(Line6), () => "睁开的越多，积累越快，危险越大");
            Line7 = this.GetLocalization(nameof(Line7), () => "这也是驱使深渊力量的代价，没开启眼睛的我，实力会大打折扣");
            Line8 = this.GetLocalization(nameof(Line8), () => "我们需要学会权衡，战斗时开启多少眼睛才是安全的");
            Line9 = this.GetLocalization(nameof(Line9), () => "如果想要无代价使用这些力量......");
            Line10 = this.GetLocalization(nameof(Line10), () => "就必须想办法让那些眼睛死机");
        }
        
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);
            
            Add(Rolename.Value, Line0.Value);
            Add(Rolename.Value, Line1.Value);
            Add(Rolename.Value, Line2.Value);
            Add(Rolename.Value, Line3.Value);
            Add(Rolename.Value, Line4.Value);
            Add(Rolename.Value, Line5.Value);
            Add(Rolename.Value, Line6.Value);
            Add(Rolename.Value, Line7.Value);
            Add(Rolename.Value, Line8.Value);
            Add(Rolename.Value, Line9.Value);
            Add(Rolename.Value, Line10.Value);
        }
        
        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!save.FirstMet) {
                return;
            }
            
            if (save.FirstResurrectionWarning) {
                return;
            }
            
            var resurrectionSystem = halibutPlayer.ResurrectionSystem;
            if (resurrectionSystem == null) {
                return;
            }
            
            if (resurrectionSystem.Ratio >= 0.7f) {
                if (ScenarioManager.Start<ResurrectionWarn>()) {
                    if (!HalibutUIHead.Instance.Open) {
                        HalibutUIHead.Instance.Open = true;//打开比目鱼UI以便展示
                    }
                    if (halibutPlayer.Player.TryGetModPlayer<HalibutSave>(out var halibutSave)) {
                        foreach (var eye in halibutSave.activationSequence) {
                            eye.IsActive = false;//关掉所有眼球
                        }
                    }
                    save.FirstResurrectionWarning = true;
                }
            }
        }
    }
}