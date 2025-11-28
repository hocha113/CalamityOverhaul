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
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老核弹");

            L0 = this.GetLocalization(nameof(L0), () => "......");
            L1 = this.GetLocalization(nameof(L1), () => "你身上的气息......很特别");
            L2 = this.GetLocalization(nameof(L2), () => "在这片被遗忘的深渊中,我已经沉睡了太久");
            L3 = this.GetLocalization(nameof(L3), () => "但我能感受到,你与那些贪婪的入侵者不同");
            L4 = this.GetLocalization(nameof(L4), () => "硫磺的毒雾也无法遮蔽你内心的纯净");
            L5 = this.GetLocalization(nameof(L5), () => "去吧,年轻的冒险者。证明你的勇气");
            L6 = this.GetLocalization(nameof(L6), () => "如果你真的拥有挑战深渊的资格,那就让我见识一下吧");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, texture: null);

            //构建对话流程
            Add(OldDukeName.Value, L0.Value);
            Add(OldDukeName.Value, L1.Value);
            Add(OldDukeName.Value, L2.Value);
            Add(OldDukeName.Value, L3.Value);
            Add(OldDukeName.Value, L4.Value);
            Add(OldDukeName.Value, L5.Value);
            Add(OldDukeName.Value, L6.Value);
        }

        protected override void OnScenarioComplete() {
            OldDukeEffect.IsActive = false;
        }
    }
}
