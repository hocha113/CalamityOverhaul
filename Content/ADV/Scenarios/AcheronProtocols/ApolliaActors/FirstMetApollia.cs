using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 阿波利娅靠近玩家后的对话场景
    /// </summary>
    internal class FirstMetApollia : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMetApollia);
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => StarStreamDialogueBox.Instance;

        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }

        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }
        public static LocalizedText C2_R1 { get; private set; }
        public static LocalizedText C2_R2 { get; private set; }

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "阿波利娅");
            Line1 = this.GetLocalization(nameof(Line1), () => "......人类?");
            Line2 = this.GetLocalization(nameof(Line2), () => "大气层的空降轨迹太显眼了，我预判了落点过来碰碰运气");
            Line3 = this.GetLocalization(nameof(Line3), () => "我是阿波利娅。你最好是星流舰队的先遣引导员，告诉我大部队就在后面");
            C1 = this.GetLocalization(nameof(C1), () => "(递出嘉登的授权芯片)");
            C2 = this.GetLocalization(nameof(C2), () => "我怎么确认你不是虫族的拟态陷阱？");
            C2_R1 = this.GetLocalization(nameof(C2_R1), () => "......拟态虫不会主动暴露位置，也不会对着空降仓跑过来");
            C2_R2 = this.GetLocalization(nameof(C2_R2), () => "芯片，给我看你的授权芯片");
            Line4 = this.GetLocalization(nameof(Line4), () => "......(读取芯片数据)");
            Line5 = this.GetLocalization(nameof(Line5), () => "就你一个？！搞什么！");
            Line6 = this.GetLocalization(nameof(Line6), () => "这里需要的是泰坦军团，还有灭绝令！而不是再派一个人来送死！");
            Line7 = this.GetLocalization(nameof(Line7), () => "(深呼吸)......抱歉，源头的安排我都已知晓");
            Line8 = this.GetLocalization(nameof(Line8), () => "阿波利娅，原隶属第九军团，现在将协助你在这颗星球上的所有行动");
        }

        protected override void OnScenarioStart() {
            StarStreamDialogueBox.Instance?.ShowFullBodyPortrait<ApolliaFullBodyPortrait>();
        }

        protected static void SetPortraitFace(ApolliaFullBodyPortrait.Face face) {
            if (StarStreamDialogueBox.Instance?.GetActiveFullBodyPortrait() is ApolliaFullBodyPortrait fullbody) {
                fullbody.currentFace = face;
            }
        }

        protected override void Build() {
            string speaker = Rolename.Value;
            DialogueBoxBase.RegisterPortrait(speaker, texture: null);
            DialogueBoxBase.SetPortraitStyle(speaker, silhouette: true);

            Add(speaker, Line1.Value, onStart: () => SetPortraitFace(ApolliaFullBodyPortrait.Face.Calmnessl));
            Add(speaker, Line2.Value);

            AddWithChoices(speaker, Line3.Value, [
                new Choice(C1.Value, () => {
                    ScenarioManager.Reset<ChipPath>();
                    ScenarioManager.Start<ChipPath>();
                    Complete();
                }),
                new Choice(C2.Value, () => {
                    ScenarioManager.Reset<SuspicionPath>();
                    ScenarioManager.Start<SuspicionPath>();
                    Complete();
                }),
            ], null, null, choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.StarStream);
        }

        /// <summary>

        /// C1分支：直接递出芯片

        /// </summary>
        internal class ChipPath : ADVScenarioBase
        {
            public override string Key => nameof(ChipPath);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => StarStreamDialogueBox.Instance;
            protected override void OnScenarioStart() {
                StarStreamDialogueBox.Instance?.ShowFullBodyPortrait<ApolliaFullBodyPortrait>();
                StarStreamDialogueBox.Instance?.GetActiveFullBodyPortrait()?.SkipFadeIn();
            }
            protected override void Build() {
                string speaker = Rolename.Value;
                Add(speaker, Line4.Value, onStart: ChipReadRender.Activate);
                Add(speaker, Line5.Value, onStart: () => {
                    SetPortraitFace(ApolliaFullBodyPortrait.Face.Rage);
                    ChipReadRender.Deactivate();
                    ShakeScreen();
                });
                Add(speaker, Line6.Value);
                Add(speaker, Line7.Value, onStart: () => SetPortraitFace(ApolliaFullBodyPortrait.Face.Calmnessl));
                Add(speaker, Line8.Value);
            }
            protected override void OnScenarioComplete() => ActivateHeroPanel();
        }

        private static void ShakeScreen() {
            //质问发怒时的震屏：叠加到当前 InnoVault 登场运镜上
            if (CutsceneDirector.CurrentClip is ApolliaCutscene) {
                CutsceneDirector.Shake(Vector2.Zero, 30f, 0.88f, 20);
            }
        }

        /// <summary>

        /// C2分支：质疑后再递芯片，汇入相同结局

        /// </summary>
        internal class SuspicionPath : ADVScenarioBase
        {
            public override string Key => nameof(SuspicionPath);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => StarStreamDialogueBox.Instance;
            protected override void OnScenarioStart() {
                StarStreamDialogueBox.Instance?.ShowFullBodyPortrait<ApolliaFullBodyPortrait>();
                StarStreamDialogueBox.Instance?.GetActiveFullBodyPortrait()?.SkipFadeIn();
            }
            protected override void Build() {
                string speaker = Rolename.Value;
                Add(speaker, C2_R1.Value, onStart: () => SetPortraitFace(ApolliaFullBodyPortrait.Face.Worry));
                Add(speaker, C2_R2.Value);
                Add(speaker, Line4.Value, onStart: ChipReadRender.Activate);
                Add(speaker, Line5.Value, onStart: () => {
                    SetPortraitFace(ApolliaFullBodyPortrait.Face.Rage);
                    ChipReadRender.Deactivate();
                    ShakeScreen();
                });
                Add(speaker, Line6.Value);
                Add(speaker, Line7.Value, onStart: () => SetPortraitFace(ApolliaFullBodyPortrait.Face.Calmnessl));
                Add(speaker, Line8.Value);
            }
            protected override void OnScenarioComplete() => ActivateHeroPanel();
        }

        private static void ActivateHeroPanel() {
            if (Main.LocalPlayer?.TryGetModPlayer(out ApolliaPlayer ap) == true) {
                ap.ActivateHeroPanel();
            }
        }
    }
}
