using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch
{
    internal sealed class FirstMetTzeentch : NarrativeScenario, ILocalizedModType
    {
        private const string MagicianLabel = "magician";
        private const string LiarLabel = "liar";
        private const string StrangerLabel = "stranger";

        public static bool Spawn { get; private set; }
        public static int RandTimer { get; private set; }

        public string LocalizationCategory => "ADV";

        public static LocalizedText Rolename { get; private set; }
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

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "?????????????????????");
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

        public static void ResetWorldState() {
            Spawn = false;
            RandTimer = 0;
        }

        public static void Open() {
            Spawn = true;
            RandTimer = Main.rand.Next(60 * 13, 60 * 20);
        }

        public static void Tick() {
            if (!Spawn) {
                return;
            }

            if (DraedonStorySync.ReadDraedon(d => d.FirstMetTzeentch, d => d.FirstMetTzeentch)) {
                Spawn = false;
                RandTimer = 0;
                return;
            }

            if (CWRWorld.HasBoss || CWRWorld.BossRush) {
                return;
            }

            if (--RandTimer > 0) {
                return;
            }

            if (NarrativeRouter.Begin<FirstMetTzeentch>()) {
                Spawn = false;
                RandTimer = 0;
            }
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Tzeentch", L1.Value)
             .Say("Tzeentch", L2.Value)
             .Say("Tzeentch", L3.Value)
             .Say("Tzeentch", L4.Value)
             .Say("Tzeentch", L5.Value)
             .Say("Tzeentch", L6.Value)
             .Say("Tzeentch", L7.Value)
             .Choice("Tzeentch", L8.Value, c => c
                 .Option("magician", Choice1Text.Value, NarrativeTarget.Goto(MagicianLabel))
                 .Option("liar", Choice2Text.Value, NarrativeTarget.Goto(LiarLabel))
                 .Option("stranger", Choice3Text.Value, NarrativeTarget.Goto(StrangerLabel)))
             .Label(MagicianLabel)
             .Say("Tzeentch", Choice1_R1.Value)
             .Say("Tzeentch", Choice1_R2.Value)
             .Say("Tzeentch", Choice1_R3.Value)
             .Say("Tzeentch", EndLine1.Value)
             .Say("Tzeentch", EndLine2.Value)
             .End()
             .Label(LiarLabel)
             .Say("Tzeentch", Choice2_R1.Value)
             .Say("Tzeentch", Choice2_R2.Value)
             .Say("Tzeentch", Choice2_R3.Value)
             .Say("Tzeentch", Choice2_R4.Value)
             .Say("Tzeentch", EndLine1.Value)
             .Say("Tzeentch", EndLine2.Value)
             .End()
             .Label(StrangerLabel)
             .Say("Tzeentch", Choice3_R1.Value)
             .Say("Tzeentch", Choice3_R2.Value)
             .Say("Tzeentch", Choice3_R3.Value)
             .Say("Tzeentch", EndLine1.Value)
             .Say("Tzeentch", EndLine2.Value)
             .End();
        }

        protected override void OnStarted() {
            TzeentchEffect.IsActive = true;
            TzeentchEffect.Send();
        }

        protected override void OnCompleted() {
            TzeentchEffect.IsActive = false;
            TzeentchEffect.Send();
            DraedonStorySync.WriteDraedon(
                d => d.FirstMetTzeentch = true,
                d => d.FirstMetTzeentch = true);
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => DraedonStorySync.ReadDraedon(d => d.FirstMetTzeentch, d => d.FirstMetTzeentch),
            CanTrigger = (_, _) => false,
        };
    }
}
