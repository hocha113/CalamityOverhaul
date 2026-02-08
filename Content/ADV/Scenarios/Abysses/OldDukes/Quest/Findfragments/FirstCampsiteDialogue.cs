using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindFragments
{
    /// <summary>
    /// 首次营地对话，触发寻找海洋碎片任务
    /// </summary>
    internal class FirstCampsiteDialogue : ADVScenarioBase, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

        //角色名称
        public static LocalizedText OldDukeName { get; private set; }

        //对话台词
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老公爵");

            L0 = this.GetLocalization(nameof(L0), () => "你来了");
            L1 = this.GetLocalization(nameof(L1), () => "我收集这些残片已经很久了");
            L2 = this.GetLocalization(nameof(L2), () => "这些残片很诡异，它们总像是...入侵进这个世界的产物");
            L3 = this.GetLocalization(nameof(L3), () => "我希望你能明白我的意思，我的直觉告诉我它们本应是无形之物，只是选择了某种可以被泰拉生物理解的形态");
            L4 = this.GetLocalization(nameof(L4), () => "总之...我想要解读它们，但数量还不够");
            L5 = this.GetLocalization(nameof(L5), () => "我希望你能帮我收集足够多的海洋残片");
            L6 = this.GetLocalization(nameof(L6), () => "我将给予你相应的报酬");
        }

        protected override void Build() {
            //注册老公爵立绘（使用剪影）
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, OldDukeCampsite.OldDuke, OldDukeCampsite.PortraitRec, null, true);

            Add(OldDukeName.Value, L0.Value);
            Add(OldDukeName.Value, L1.Value);
            Add(OldDukeName.Value, L2.Value);
            Add(OldDukeName.Value, L3.Value);
            Add(OldDukeName.Value, L4.Value);
            Add(OldDukeName.Value, L5.Value);
            Add(OldDukeName.Value, L6.Value);
        }

        protected override void OnScenarioStart() {
            //OldDukeEffect.IsActive由声明式计算自动管理
        }

        protected override void OnScenarioComplete() {
            //标记任务已触发
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.OldDukeFindFragmentsQuestTriggered = true;
            }
            //OldDukeEffect.IsActive由声明式计算自动管理
        }
    }
}
