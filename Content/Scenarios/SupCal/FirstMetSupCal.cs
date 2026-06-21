using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    internal sealed class FirstMetSupCal : NarrativeScenario, ILocalizedModType
    {
        private const string FightLabel = "fight";
        private const string SilentLabel = "silent";

        /// <summary>玩家选战且正进入战斗场景</summary>
        public static bool ThisIsToFight;

        public string LocalizationCategory => "ADV";

        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Rolename3 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }
        public static LocalizedText NoFishLine1 { get; private set; }
        public static LocalizedText NoFishLine2 { get; private set; }
        public static LocalizedText NoFishLine3 { get; private set; }
        public static LocalizedText NoFishLine4 { get; private set; }
        public static LocalizedText NoFishLine5 { get; private set; }
        public static LocalizedText NoFishLine6 { get; private set; }
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText Choice2Response { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "硫火女巫");
            Rolename3 = this.GetLocalization(nameof(Rolename3), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "没想到你这么快就杀掉了我的'妹妹'");
            Line2 = this.GetLocalization(nameof(Line2), () => "你的成长速度确实有些快了");
            Line3 = this.GetLocalization(nameof(Line3), () => "我对你有印象......你是...");
            Line4 = this.GetLocalization(nameof(Line4), () => "焚烧了一半海域的硫火女巫？！");
            Line5 = this.GetLocalization(nameof(Line5), () => "哈?!呵呵，竟然有人...或者鱼认得我，你们倒也算有趣");
            Line6 = this.GetLocalization(nameof(Line6), () => "......你为什么还活着?我明明记得硫火女巫在上世纪.....就已经死了");
            Line7 = this.GetLocalization(nameof(Line7), () => "真是一条有趣的鱼。我的意识早已熔铸进硫磺火中，这幅躯体......只不过是被火焰操控的尸体罢了" +
                "我的意识早已经熔铸进硫磺火中，这具躯体只不过是被火焰操纵的尸体");
            Line8 = this.GetLocalization(nameof(Line8), () => "......活人的意识，非人的躯体，依靠媒介行走世间，你成为了异类?!");
            Line9 = this.GetLocalization(nameof(Line9), () => "你的层次太低，理解不了我现在的状态");
            Line10 = this.GetLocalization(nameof(Line10), () => "况且我来这里也不是为了这事儿的......");

            NoFishLine1 = this.GetLocalization(nameof(NoFishLine1), () => "没想到你这么快就杀掉了我的'妹妹'，独自一人来的？");
            NoFishLine2 = this.GetLocalization(nameof(NoFishLine2), () => "你的成长速度比我预期的要快");
            NoFishLine3 = this.GetLocalization(nameof(NoFishLine3), () => "问我为什么还活着？看来你已经开始触碰那些不该知道的东西");
            NoFishLine4 = this.GetLocalization(nameof(NoFishLine4), () => "我的意识早已熔入硫磺之火，这幅躯体......只不过是被火焰操控的尸体罢了");
            NoFishLine5 = this.GetLocalization(nameof(NoFishLine5), () => "不过说了也无用，你的层次太低，无法理解我现在的状态");
            NoFishLine6 = this.GetLocalization(nameof(NoFishLine6), () => "当然，我现身也不是为了解释这些的");

            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "那么，你的选择是？");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(拔出武器)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "那么便让我来称量称量你吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "......真是杂鱼，那么给你一个见面礼，我们下次见");
        }

        protected override void Build(NarrativeComposer n) {
            if (HasHalibut()) {
                n.Say("SupCalUnknown", Line1.Value)
                 .Say("SupCalUnknown", Line2.Value)
                 .Say("Helen", "Solemn", Line3.Value)
                 .Say("Helen", "Amazed", Line4.Value)
                 .Say("SupCal", Line5.Value)
                 .Say("Helen", "Amazed", Line6.Value)
                 .Say("SupCal", "CloseEye", Line7.Value)
                 .Say("Helen", "Amazed", Line8.Value)
                 .Say("SupCal", "CloseEye", Line9.Value)
                 .Say("SupCal", "BeTo", Line10.Value);
            }
            else {
                n.Say("SupCalUnknown", NoFishLine1.Value)
                 .Say("SupCalUnknown", NoFishLine2.Value)
                 .Say("SupCal", "CloseEye", NoFishLine3.Value)
                 .Say("SupCal", "CloseEye", NoFishLine4.Value)
                 .Say("SupCal", "BeTo", NoFishLine5.Value)
                 .Say("SupCal", "BeTo", NoFishLine6.Value);
            }

            n.Choice("SupCal", "BeTo", QuestionLine.Value, c => c
                .Option("fight", Choice1Text.Value, NarrativeTarget.Goto(FightLabel))
                .Option("silent", Choice2Text.Value, NarrativeTarget.Goto(SilentLabel)))
             .Label(FightLabel)
             .Say("SupCal", Choice1Response.Value, onExit: OnChoseFight)
             .End()
             .Label(SilentLabel)
             .Say("SupCal", "Despise", Choice2Response.Value, onExit: OnChoseSilent)
             .Reward(CWRID.Item_AshesofCalamity, 999, string.Empty)
             .End();
        }

        protected override void OnStarted() => SupCalEffect.IsActive = true;

        private static bool HasHalibut() {
            try {
                return Main.LocalPlayer.TryGetOverride(out HalibutPlayer halibutPlayer) && halibutPlayer.HasHalubut;
            } catch {
                return false;
            }
        }

        private static void OnChoseFight() {
            if (!NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                CWRRef.SummonSupCal(Main.LocalPlayer.Center);
            }

            HalibutStorySync.WriteSupCal(
                d => d.SupCalChoseToFight = true,
                d => d.SupCalChoseToFight = true);
            ThisIsToFight = true;
            SupCalEffect.IsActive = false;
        }

        private static void OnChoseSilent() => SupCalEffect.IsActive = false;
    }
}
