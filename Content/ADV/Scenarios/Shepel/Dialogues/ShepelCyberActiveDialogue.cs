using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues
{
    /// <summary>
    /// 赛博领域激活时的专属对话，优先级高于空闲问候
    /// Shepel会针对当前领域层级给出状态简报
    /// </summary>
    internal class ShepelCyberActiveDialogue : SHPCDialogueScenarioBase, ILocalizedModType
    {
        public new string LocalizationCategory => "ADV.Shepel";
        public override int DialoguePriority => 10;

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText Line_Intro { get; private set; }
        public static LocalizedText Line_LayerReport { get; private set; }
        public static LocalizedText Line_Warning { get; private set; }
        public static LocalizedText Line_MaxLayer { get; private set; }

        protected override Func<DialogueBoxBase> DefaultDialogueStyle
            => () => ADV.DialogueBoxs.Styles.SHPCDialogueBox.Instance;

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            Line_Intro = this.GetLocalization(nameof(Line_Intro),
                () => "主人，赛博空间已展开，当前层级 {0}。外部信号已被完全隔绝，这里现在是只属于我们的安全领域。");
            Line_LayerReport = this.GetLocalization(nameof(Line_LayerReport),
                () => "领域越深，我能为您清除的阻碍就越多。但请留意RAM的消耗。");
            Line_Warning = this.GetLocalization(nameof(Line_Warning),
                () => "主人请警戒，领域边缘出现异常波动，请保持在我的掩护范围内。");
            Line_MaxLayer = this.GetLocalization(nameof(Line_MaxLayer),
                () => "主人，我们已抵达最深处。前方即是黑墙边界，危险性极高……请握紧我的手，千万不要松开。");
        }

        protected override bool CheckConditions(Player player, ADVSave save) => Cyberspace.Active;

        protected override void Build() {
            ADV.DialogueBoxs.DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            ADV.DialogueBoxs.DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);

            int layer = Cyberspace.CurrentLayer;
            bool isMaxLayer = layer >= Cyberspace.MaxLayerCount;

            string introText = string.Format(Line_Intro.Value, layer);
            Add(RoleName.Value, introText, onStart: () => {
                ADV.DialogueBoxs.Styles.SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
                if (ADV.DialogueBoxs.Styles.SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                    portrait.SkipFadeIn();
                    portrait.currentFace = ShepelFullBodyPortrait.Face.Serious;
                }
            });

            if (isMaxLayer) {
                Add(RoleName.Value, Line_MaxLayer.Value, onStart: () => {
                    if (ADV.DialogueBoxs.Styles.SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                        portrait.currentFace = ShepelFullBodyPortrait.Face.Shocked;
                    }
                }, onComplete: Complete);
            }
            else {
                Add(RoleName.Value, Line_LayerReport.Value);
                Add(RoleName.Value, Line_Warning.Value, onComplete: Complete);
            }
        }

        protected override void OnScenarioStart() {
            ADV.DialogueBoxs.Styles.SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (ADV.DialogueBoxs.Styles.SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.SkipFadeIn();
                portrait.currentFace = ShepelFullBodyPortrait.Face.None;
            }
        }
    }
}
