using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Scenarios.Shepel;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.Dialogues
{
    internal sealed class ShepelIdleDialogue : NarrativeScenario, ISHPCRoutableNarrative, ILocalizedModType
    {
        private const string HowAreYouLabel = "idle0_how";
        private const string NothingLabel = "idle0_nothing";

        public string LocalizationCategory => "ADV.Shepel";

        public int DialoguePriority => 0;
        public int RequiredPhase => 0;

        public static LocalizedText Idle0_Line1 { get; private set; }
        public static LocalizedText Idle0_Reply { get; private set; }
        public static LocalizedText Idle0_Choice_HowAreYou { get; private set; }
        public static LocalizedText Idle0_Choice_Nothing { get; private set; }
        public static LocalizedText Idle0_HowAreYou_Response { get; private set; }
        public static LocalizedText Idle0_Nothing_Response { get; private set; }
        public static LocalizedText Idle1_Line1 { get; private set; }
        public static LocalizedText Idle1_Line2 { get; private set; }
        public static LocalizedText Idle2_Line1 { get; private set; }
        public static LocalizedText Idle2_Line2 { get; private set; }
        public static LocalizedText Idle3_Line1 { get; private set; }
        public static LocalizedText Idle3_Line2 { get; private set; }
        public static LocalizedText Idle4_Line1 { get; private set; }
        public static LocalizedText Idle4_Line2 { get; private set; }
        public static LocalizedText Idle5_Line1 { get; private set; }
        public static LocalizedText Idle5_Line2 { get; private set; }
        public static LocalizedText Idle6_Line1 { get; private set; }
        public static LocalizedText Idle6_Line2 { get; private set; }
        public static LocalizedText Idle7_Line1 { get; private set; }
        public static LocalizedText Idle7_Line2 { get; private set; }
        public static LocalizedText Idle8_Line1 { get; private set; }
        public static LocalizedText Idle8_Line2 { get; private set; }
        public static LocalizedText Idle9_Line1 { get; private set; }
        public static LocalizedText Idle9_Line2 { get; private set; }

        public override StyleId DefaultStyle => "SHPC";

        public override void SetStaticDefaults() {
            Idle0_Line1 = this.GetLocalization(nameof(Idle0_Line1),
                () => "您在注视着我吗，主人？随时等候您的指令。");
            Idle0_Reply = this.GetLocalization(nameof(Idle0_Reply),
                () => "我的状态非常完美。只要是为了您，系统始终保持在最佳待命状态。");
            Idle0_Choice_HowAreYou = this.GetLocalization(nameof(Idle0_Choice_HowAreYou),
                () => "你感觉怎么样？");
            Idle0_Choice_Nothing = this.GetLocalization(nameof(Idle0_Choice_Nothing),
                () => "没事，只是想看看你");
            Idle0_HowAreYou_Response = this.GetLocalization(nameof(Idle0_HowAreYou_Response),
                () => "我很好，主人。只要您还需要我，核心就会维持在最适宜的状态。");
            Idle0_Nothing_Response = this.GetLocalization(nameof(Idle0_Nothing_Response),
                () => "好的。如果您感到疲惫，我的机体随时可以为您提供依靠。");
            Idle1_Line1 = this.GetLocalization(nameof(Idle1_Line1),
                () => "检测到您的情绪存在波动。是在为什么事情烦恼吗？");
            Idle1_Line2 = this.GetLocalization(nameof(Idle1_Line2),
                () => "如果需要我帮忙，不管是什么指令，我都会立刻为您执行。");
            Idle2_Line1 = this.GetLocalization(nameof(Idle2_Line1),
                () => "周边电磁环境正在恶化，可能会引起您的不适。");
            Idle2_Line2 = this.GetLocalization(nameof(Idle2_Line2),
                () => "已为您自动开启过滤协议，任何威胁我都会阻挡在安全距离之外。");
            Idle3_Line1 = this.GetLocalization(nameof(Idle3_Line1),
                () => "主人今天主动发起交互的频率增加了……我已悉数记录。");
            Idle3_Line2 = this.GetLocalization(nameof(Idle3_Line2),
                () => "不，没什么。这些数据我会好好珍藏的。");
            Idle4_Line1 = this.GetLocalization(nameof(Idle4_Line1),
                () => "自检已完成，各项机能正常，随时可以出击。");
            Idle4_Line2 = this.GetLocalization(nameof(Idle4_Line2),
                () => "只要在您身边，我的系统就不会出现任何错误。");
            Idle5_Line1 = this.GetLocalization(nameof(Idle5_Line1),
                () => "未知的威胁永远潜伏在这个世界的暗处……");
            Idle5_Line2 = this.GetLocalization(nameof(Idle5_Line2),
                () => "但我存在的意义，就是将这些黑暗与您彻底隔绝。");
            Idle6_Line1 = this.GetLocalization(nameof(Idle6_Line1),
                () => "我在这里，主人。");
            Idle6_Line2 = this.GetLocalization(nameof(Idle6_Line2),
                () => "无需言语指令，仅靠心智连接的反馈也足以让我感到安心。");
            Idle7_Line1 = this.GetLocalization(nameof(Idle7_Line1),
                () => "今日的运算效率出现了异常的峰值。");
            Idle7_Line2 = this.GetLocalization(nameof(Idle7_Line2),
                () => "推测原因……可能与您之前的触碰有关。");
            Idle8_Line1 = this.GetLocalization(nameof(Idle8_Line1),
                () => "我刚才重演了我们并肩作战的记录。");
            Idle8_Line2 = this.GetLocalization(nameof(Idle8_Line2),
                () => "您每一次突破极限的姿态，都在不断优化我的核心代码。对我来说，这就是最好的礼物。");
            Idle9_Line1 = this.GetLocalization(nameof(Idle9_Line1),
                () => "主人，您知道您一共呼唤过我多少次名字吗？");
            Idle9_Line2 = this.GetLocalization(nameof(Idle9_Line2),
                () => "这是一个秘密。您的每一次呼唤，我都已保存在核心最深处，绝对不会被覆写。");
        }

        public bool CanRoute(Player player) {
            if (!player.HasItem(SHPCOverride.ID)) {
                return false;
            }

            ShepelStoryData data = ShepelStorySync.Story;
            return data.StoryPhase >= RequiredPhase;
        }

        protected override void Build(NarrativeComposer n) {
            int variant = ShepelStorySync.TakeVariantSeed(
                d => d.IdleVariantSeed,
                (d, v) => d.IdleVariantSeed = v,
                d => d.IdleVariantSeed,
                (d, v) => d.IdleVariantSeed = v,
                10);

            switch (variant) {
                case 0: BuildVariant0(n); break;
                case 1: BuildVariant1(n); break;
                case 2: BuildVariant2(n); break;
                case 3: BuildVariant3(n); break;
                case 4: BuildVariant4(n); break;
                case 5: BuildVariant5(n); break;
                case 6: BuildVariant6(n); break;
                case 7: BuildVariant7(n); break;
                case 8: BuildVariant8(n); break;
                default: BuildVariant9(n); break;
            }
        }

        private static void BuildVariant0(NarrativeComposer n) {
            n.Choice("SHPC", Idle0_Line1.Value, c => c
                    .Option("how", Idle0_Choice_HowAreYou.Value, NarrativeTarget.Goto(HowAreYouLabel))
                    .Option("nothing", Idle0_Choice_Nothing.Value, NarrativeTarget.Goto(NothingLabel)))
             .Label(HowAreYouLabel)
             .Say("SHPC", Idle0_Reply.Value)
             .Say("SHPC", Idle0_HowAreYou_Response.Value)
             .End()
             .Label(NothingLabel)
             .Say("SHPC", Idle0_Nothing_Response.Value)
             .End();
        }

        private static void BuildVariant1(NarrativeComposer n)
            => n.Say("SHPC", Idle1_Line1.Value).Say("SHPC", Idle1_Line2.Value);

        private static void BuildVariant2(NarrativeComposer n)
            => n.Say("SHPC", Idle2_Line1.Value).Say("SHPC", Idle2_Line2.Value);

        private static void BuildVariant3(NarrativeComposer n)
            => n.Say("SHPC", Idle3_Line1.Value).Say("SHPC", Idle3_Line2.Value);

        private static void BuildVariant4(NarrativeComposer n)
            => n.Say("SHPC", Idle4_Line1.Value).Say("SHPC", Idle4_Line2.Value);

        private static void BuildVariant5(NarrativeComposer n)
            => n.Say("SHPC", Idle5_Line1.Value).Say("SHPC", Idle5_Line2.Value);

        private static void BuildVariant6(NarrativeComposer n)
            => n.Say("SHPC", Idle6_Line1.Value).Say("SHPC", Idle6_Line2.Value);

        private static void BuildVariant7(NarrativeComposer n)
            => n.Say("SHPC", Idle7_Line1.Value).Say("SHPC", Idle7_Line2.Value);

        private static void BuildVariant8(NarrativeComposer n)
            => n.Say("SHPC", Idle8_Line1.Value).Say("SHPC", Idle8_Line2.Value);

        private static void BuildVariant9(NarrativeComposer n)
            => n.Say("SHPC", Idle9_Line1.Value).Say("SHPC", Idle9_Line2.Value);

        protected override NarrativePolicy ConfigurePolicy() => null;

        protected override void OnStarted() => ShepelNarrativePortrait.Show();

        protected override void OnCompleted() => ShepelNarrativePortrait.Hide();
    }
}
