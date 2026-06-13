using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.CybCourses
{
    /// <summary>
    /// 超梦教程通关祝贺对话
    /// 由 <see cref="HackTimeTutorialLead"/> 在 Phase.Done 时拉起；OnScenarioComplete 拉起 <see cref="CybCourseCompletePanel"/>
    /// </summary>
    internal class CybCourseOutroDialogue : ADVScenarioBase, ILocalizedModType
    {
        public new string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        protected override Func<DialogueBoxBase> DefaultDialogueStyle
            => () => SHPCDialogueBox.Instance;

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "训练完成。所有接口均已完成校准。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "您已掌握神经直连协议，超梦节点正在脱钩。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "随时可重置训练，或退出超梦。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(SpeakerName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(SpeakerName.Value, silhouette: false);

            AddTimed(SpeakerName.Value, Line1.Value, 4f, onStart: OnStart);
            AddTimed(SpeakerName.Value, Line2.Value, 4.5f);
            Add(SpeakerName.Value, Line3.Value);
        }

        private void OnStart() {
            SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.SkipFadeIn();
                portrait.currentFace = ShepelFullBodyPortrait.Face.Happy;
            }
        }

        protected override void OnScenarioComplete() {
            CybCourseCompletePanel.Show();
        }
    }
}
