using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes
{
    internal class FirstMetOldDuke : ADVScenarioBase, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV.FirstMetOldDuke";

        //设置默认对话框样式为硫磺海风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

        //角色名称
        public static LocalizedText OldDukeName { get; private set; }
        public static LocalizedText HelenName { get; private set; }

        //对话台词
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老核弹");

            L0 = this.GetLocalization(nameof(L0), () => "......");
            L1 = this.GetLocalization(nameof(L1), () => "没必要一见面就拔刀相向");
            L2 = this.GetLocalization(nameof(L2), () => "我年纪大了，不想打架");
            L3 = this.GetLocalization(nameof(L3), () => "我们可以做个交易");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, texture: null);

            //构建对话流程
            Add(OldDukeName.Value, L0.Value);
            Add(OldDukeName.Value, L1.Value);
            Add(OldDukeName.Value, L2.Value);
            Add(OldDukeName.Value, L3.Value);
        }

        protected override void OnScenarioComplete() {
            OldDukeEffect.IsActive = false;
        }
    }
}
