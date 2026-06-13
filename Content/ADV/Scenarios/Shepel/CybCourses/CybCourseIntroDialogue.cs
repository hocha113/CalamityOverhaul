using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.CybCourses
{
    //Shepel在超梦教程关卡中的开场介绍场景
    //由CybTutorialLead在世界加载后自动触发一次，完成后启动SHPC教学步骤
    internal class CybCourseIntroDialogue : ADVScenarioBase, ILocalizedModType
    {
        public new string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }

        protected override Func<DialogueBoxBase> DefaultDialogueStyle
            => () => SHPCDialogueBox.Instance;

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "主人，欢迎进入神经训练节点。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "这里是为您准备的沉浸式教学空间。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "SHPC是一种强大的智能武器，您将学习如何操作它的HUD界面。");
            Line4 = this.GetLocalization(nameof(Line4),
                () => "准备好将意识与我连接了吗？请握紧我的手，我们开始吧。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(SpeakerName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(SpeakerName.Value, silhouette: false);

            AddTimed(SpeakerName.Value, Line1.Value, 4.5f, onStart: OnStart);
            AddTimed(SpeakerName.Value, Line2.Value, 4.5f);
            AddTimed(SpeakerName.Value, Line3.Value, 6f);
            Add(SpeakerName.Value, Line4.Value);
        }

        private void OnStart() {
            SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.SkipFadeIn();
                portrait.currentFace = ShepelFullBodyPortrait.Face.Happy;
            }
        }

        protected override void OnScenarioComplete() {
            CybTutorialLead.BeginSHPCTutorial();
        }
    }
}
