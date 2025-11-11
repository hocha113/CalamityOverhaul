using CalamityOverhaul.Content.ADV.ADVChoiceBoxs;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch
{
    internal class FirstMetTzeentch : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        private const string Rolename1 = "?????????????????????";
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => TzeentchDialogueBox.Instance;

        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }
        public static LocalizedText L7 { get; private set; }
        public static LocalizedText L8 { get; private set; }

        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice3Text { get; private set; }

        public static LocalizedText Choice1_R1 { get; private set; }
        public static LocalizedText Choice1_R2 { get; private set; }
        public static LocalizedText Choice1_R3 { get; private set; }

        public static LocalizedText Choice2_R1 { get; private set; }
        public static LocalizedText Choice2_R2 { get; private set; }
        public static LocalizedText Choice2_R3 { get; private set; }
        public static LocalizedText Choice2_R4 { get; private set; }

        public static LocalizedText Choice3_R1 { get; private set; }
        public static LocalizedText Choice3_R2 { get; private set; }
        public static LocalizedText Choice3_R3 { get; private set; }

        public static LocalizedText EndLine1 { get; private set; }
        public static LocalizedText EndLine2 { get; private set; }

        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "嘘——别回头");
            L2 = this.GetLocalization(nameof(L2), () => "我一直在看你搭建这些小玩意儿");
            L3 = this.GetLocalization(nameof(L3), () => "十座量子节点，十条通路，十个可能的未来");
            L4 = this.GetLocalization(nameof(L4), () => "很可爱的小网络，你知道它会吸引多少东西过来吗？");
            L5 = this.GetLocalization(nameof(L5), () => "啊，对了该介绍一下自己");
            L6 = this.GetLocalization(nameof(L6), () => "有人叫我预言者，有人叫我命运之羽……");
            L7 = this.GetLocalization(nameof(L7), () => "但我更喜欢听到——你——现在怎么称呼我");
            L8 = this.GetLocalization(nameof(L8), () => "来吧，让我听听");

            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "魔术师……？");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "骗子");
            Choice3Text = this.GetLocalization(nameof(Choice3Text), () => "不，我不认识你");

            Choice1_R1 = this.GetLocalization(nameof(Choice1_R1), () => "哈！多么温和的叫法");
            Choice1_R2 = this.GetLocalization(nameof(Choice1_R2), () => "可惜，我不变魔术我只改变事实");
            Choice1_R3 = this.GetLocalization(nameof(Choice1_R3), () => "继续吧，'魔术师'就先放在账上");

            Choice2_R1 = this.GetLocalization(nameof(Choice2_R1), () => "啊，直接、粗暴、诚实，多可贵的品质");
            Choice2_R2 = this.GetLocalization(nameof(Choice2_R2), () => "但很遗憾，我从不骗人");
            Choice2_R3 = this.GetLocalization(nameof(Choice2_R3), () => "我只是提前告诉你，将要发生的事而已");
            Choice2_R4 = this.GetLocalization(nameof(Choice2_R4), () => "无论你信不信——都已经发生了");

            Choice3_R1 = this.GetLocalization(nameof(Choice3_R1), () => "不认识？很好");
            Choice3_R2 = this.GetLocalization(nameof(Choice3_R2), () => "所有的故事，都是从这句话开始的");
            Choice3_R3 = this.GetLocalization(nameof(Choice3_R3), () => "只是……大多数结局都不太愉快");

            EndLine1 = this.GetLocalization(nameof(EndLine1), () => "那么，我们会再见的");
            EndLine2 = this.GetLocalization(nameof(EndLine2), () => "很快");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1, ADVAsset.Tzeentch);
            DialogueBoxBase.SetPortraitStyle(Rolename1, silhouette: true);

            Add(Rolename1, L1.Value);
            Add(Rolename1, L2.Value);
            Add(Rolename1, L3.Value);
            Add(Rolename1, L4.Value);
            Add(Rolename1, L5.Value);
            Add(Rolename1, L6.Value);
            Add(Rolename1, L7.Value);

            AddWithChoices(Rolename1, L8.Value, [
                new Choice(Choice1Text.Value, Choice1_Magician),
                new Choice(Choice2Text.Value, Choice2_Liar),
                new Choice(Choice3Text.Value, Choice3_Stranger)
            ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Tzeentch);
        }

        protected override void OnScenarioStart() {
            TzeentchEffect.IsActive = true;
            TzeentchEffect.Send();
        }

        private void Choice1_Magician() {
            ScenarioManager.Reset<Choice1_MagicianPath>();
            ScenarioManager.Start<Choice1_MagicianPath>();
            Complete();
        }

        private void Choice2_Liar() {
            ScenarioManager.Reset<Choice2_LiarPath>();
            ScenarioManager.Start<Choice2_LiarPath>();
            Complete();
        }

        private void Choice3_Stranger() {
            ScenarioManager.Reset<Choice3_StrangerPath>();
            ScenarioManager.Start<Choice3_StrangerPath>();
            Complete();
        }

        internal class Choice1_MagicianPath : ADVScenarioBase
        {
            public override string Key => nameof(Choice1_MagicianPath);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => TzeentchDialogueBox.Instance;

            protected override void OnScenarioStart() {
                TzeentchEffect.IsActive = true;
            }

            protected override void OnScenarioComplete() {
                TzeentchEffect.IsActive = false;
                TzeentchEffect.Send();
            }

            protected override void Build() {
                Add(Rolename1, Choice1_R1.Value);
                Add(Rolename1, Choice1_R2.Value);
                Add(Rolename1, Choice1_R3.Value);
                Add(Rolename1, EndLine1.Value);
                Add(Rolename1, EndLine2.Value);
            }
        }

        internal class Choice2_LiarPath : ADVScenarioBase
        {
            public override string Key => nameof(Choice2_LiarPath);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => TzeentchDialogueBox.Instance;

            protected override void OnScenarioStart() {
                TzeentchEffect.IsActive = true;
            }

            protected override void OnScenarioComplete() {
                TzeentchEffect.IsActive = false;
                TzeentchEffect.Send();
            }

            protected override void Build() {
                Add(Rolename1, Choice2_R1.Value);
                Add(Rolename1, Choice2_R2.Value);
                Add(Rolename1, Choice2_R3.Value);
                Add(Rolename1, Choice2_R4.Value);
                Add(Rolename1, EndLine1.Value);
                Add(Rolename1, EndLine2.Value);
            }
        }

        internal class Choice3_StrangerPath : ADVScenarioBase
        {
            public override string Key => nameof(Choice3_StrangerPath);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => TzeentchDialogueBox.Instance;

            protected override void OnScenarioStart() {
                TzeentchEffect.IsActive = true;
            }

            protected override void OnScenarioComplete() {
                TzeentchEffect.IsActive = false;
                TzeentchEffect.Send();
            }

            protected override void Build() {
                Add(Rolename1, Choice3_R1.Value);
                Add(Rolename1, Choice3_R2.Value);
                Add(Rolename1, Choice3_R3.Value);
                Add(Rolename1, EndLine1.Value);
                Add(Rolename1, EndLine2.Value);
            }
        }
    }
}
