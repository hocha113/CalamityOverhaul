using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 鬼雨叙事皮肤 Demo：伞灵「深夜」初见。走完对话/选择/奖励三种 UI，
    /// 无持久化标志，可反复触发；调试入口见 <see cref="ShenyoStyleDemoItem"/>
    /// </summary>
    internal sealed class FirstMetShenyo : NarrativeScenario, ILocalizedModType
    {
        private const string TakeLabel = "take";
        private const string ReturnLabel = "return";
        private const string NameLabel = "name";

        public string LocalizationCategory => "ADV";

        public static LocalizedText ShenyoName { get; private set; }

        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText C0 { get; private set; }
        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }
        public static LocalizedText C3 { get; private set; }
        public static LocalizedText C1Response { get; private set; }
        public static LocalizedText C2Response { get; private set; }
        public static LocalizedText C3Response { get; private set; }

        public override StyleId DefaultStyle => "Kikasa";

        public override void SetStaticDefaults() {
            ShenyoName = this.GetLocalization(nameof(ShenyoName), () => "深夜");
            L1 = this.GetLocalization(nameof(L1), () => "……你听。雨把脚步声都收走了");
            L2 = this.GetLocalization(nameof(L2), () => "我在伞底下住了很久，伞外的东西，湿了就沉");
            L3 = this.GetLocalization(nameof(L3), () => "你是顺着雨声找来的？还是雨顺着你来的");
            L4 = this.GetLocalization(nameof(L4), () => "别站在檐口，那里的雨认得人");
            L5 = this.GetLocalization(nameof(L5), () => "这把旧的给你，伞面有个洞，正好看月亮");
            C0 = this.GetLocalization(nameof(C0), () => "雨还没停");
            C1 = this.GetLocalization(nameof(C1), () => "谢过她");
            C2 = this.GetLocalization(nameof(C2), () => "把伞还回去");
            C3 = this.GetLocalization(nameof(C3), () => "问她的名字");
            C1Response = this.GetLocalization(nameof(C1Response), () => "……不用谢，它想跟你走");
            C2Response = this.GetLocalization(nameof(C2Response), () => "拿着吧，雨淋不到我");
            C3Response = this.GetLocalization(nameof(C3Response), () => "深夜。雨停之前，都叫这个名字");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Shenyo", L1.Value)
             .Say("Shenyo", L2.Value)
             .Say("Shenyo", L3.Value)
             .Say("Shenyo", L4.Value)
             .Say("Shenyo", L5.Value)
             .Reward(ItemID.Umbrella, 1, string.Empty)
             .Choice("Shenyo", C0.Value, c => c
                 .Option(TakeLabel, C1.Value, NarrativeTarget.Goto(TakeLabel))
                 .Option(ReturnLabel, C2.Value, NarrativeTarget.Goto(ReturnLabel))
                 .Option(NameLabel, C3.Value, NarrativeTarget.Goto(NameLabel)))
             .Label(TakeLabel)
             .Say("Shenyo", C1Response.Value)
             .End()
             .Label(ReturnLabel)
             .Say("Shenyo", C2Response.Value)
             .End()
             .Label(NameLabel)
             .Say("Shenyo", C3Response.Value)
             .End();
        }
    }
}
