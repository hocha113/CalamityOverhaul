using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 沈幽初见：鬼雨叙事皮肤，立绘走黑雨汇聚入场（<see cref="ShenyoPortraitRainRenderer"/>）。
    /// 不带自动触发策略，由外部接线调用 <c>NarrativeRouter.Begin&lt;FirstMetShenyo&gt;()</c>
    /// </summary>
    internal sealed class FirstMetShenyo : StoryScenario, ILocalizedModType
    {
        private const string DeadLabel = "dead";
        private const string RainLabel = "rain";

        public string LocalizationCategory => "ADV";

        public static LocalizedText ShenyoName { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText OptDead { get; private set; }
        public static LocalizedText OptRain { get; private set; }
        public static LocalizedText A1 { get; private set; }
        public static LocalizedText A2 { get; private set; }
        public static LocalizedText B1 { get; private set; }
        public static LocalizedText B2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            ShenyoName = this.GetLocalization(nameof(ShenyoName), () => "沈幽");
            L1 = this.GetLocalization(nameof(L1), () => "这地方很久没看到有活人来了");
            L2 = this.GetLocalization(nameof(L2), () => "你是怎么到这里来的？");
            OptDead = this.GetLocalization(nameof(OptDead), () => "死过一次，醒了就在这了");
            OptRain = this.GetLocalization(nameof(OptRain), () => "外面下雨，打伞走进来的");
            A1 = this.GetLocalization(nameof(A1), () => "……死而复生？");
            A2 = this.GetLocalization(nameof(A2), () => "稀罕事，不过看你的反应，倒像是不在意");
            B1 = this.GetLocalization(nameof(B1), () => "那你运气真不错");
            B2 = this.GetLocalization(nameof(B2), () => "现在想想该怎么出去吧");
        }

        protected override void Build(NarrativeComposer n) {
            n.AllowSkipThrough()
             //黑雨成形期间闭目开口，睁眼（眯眼打量）落在提问那一拍
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[1],
                    onExit: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Choice(NarrativeIds.Shenyo, L2.Value, c => c
                 .Voice(Voice[2])
                 .Option(DeadLabel, OptDead.Value, NarrativeTarget.Goto(DeadLabel))
                 .Option(RainLabel, OptRain.Value, NarrativeTarget.Goto(RainLabel)))
             .Label(DeadLabel)
             .Say(NarrativeIds.Shenyo, A1.Value, Voice[3],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Say(NarrativeIds.Shenyo, A2.Value, Voice[4],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .End()
             .Label(RainLabel)
             .Say(NarrativeIds.Shenyo, B1.Value, Voice[5],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Say(NarrativeIds.Shenyo, B2.Value, Voice[6],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .End();
        }

        protected override void OnStarted() => ShenyoNarrativePortrait.ShowRainAssembly();

        protected override void OnCompleted() => ShenyoNarrativePortrait.Hide();
    }
}
