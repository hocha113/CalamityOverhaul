using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums
{
    internal sealed class ExoMechEndingDialogue : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Draedon";
        public static LocalizedText EndLine1 { get; private set; }
        public static LocalizedText EndLine2 { get; private set; }
        public static LocalizedText EndLine3 { get; private set; }
        public static LocalizedText EndLine4 { get; private set; }
        public static LocalizedText EndLine5 { get; private set; }
        public static LocalizedText EndLine6 { get; private set; }
        public static LocalizedText EndLine7 { get; private set; }
        public static LocalizedText EndLine8 { get; private set; }
        public static LocalizedText EndLine9 { get; private set; }
        public static LocalizedText KillAttemptLine { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {

            //对应原版 DraedonEndText
            EndLine1 = this.GetLocalization(nameof(EndLine1), () => "一个未知因素，你是一个特异点");
            EndLine2 = this.GetLocalization(nameof(EndLine2), () => "你对这片大地和它的历史而言，只是外来之人，就和我一样");
            EndLine3 = this.GetLocalization(nameof(EndLine3), () => "......很抱歉，但在看了这样一场\"展示\"之后，我必须得离开一小会儿去整理我的思绪");
            EndLine4 = this.GetLocalization(nameof(EndLine4), () => "迄今为止喷洒的血液已经让这片大陆变得陈腐无比，毫无生气");
            EndLine5 = this.GetLocalization(nameof(EndLine5), () => "你也挥洒了自己的鲜血，这或许足以终结这个绝望的时代......不管如何，这都是我期望看到的变化");
            EndLine6 = this.GetLocalization(nameof(EndLine6), () => "现在，你想要接触那位暴君。可惜我无法帮到你");
            EndLine7 = this.GetLocalization(nameof(EndLine7), () => "这并非出自怨恨，毕竟从一开始，我的目标就只有观察刚才的这一场战斗");
            EndLine8 = this.GetLocalization(nameof(EndLine8), () => "但你过去也成功过，所以你最后会找到办法的");
            EndLine9 = this.GetLocalization(nameof(EndLine9), () => "我必须尊重并承认你的胜利，但现在，我得把注意力放回到我的机械上了");

            KillAttemptLine = this.GetLocalization(nameof(KillAttemptLine), () => "......你的行为没什么必要");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Draedon", EndLine1.Value)
             .Say("Draedon", EndLine2.Value)
             .Say("Draedon", EndLine3.Value)
             .Say("Draedon", EndLine4.Value)
             .Say("Draedon", EndLine5.Value)
             .Say("Draedon", EndLine6.Value)
             .Say("Draedon", EndLine7.Value)
             .Say("Draedon", EndLine8.Value)
             .Say("Draedon", EndLine9.Value);
        }

        protected override void OnStarted() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnCompleted() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            //手动Begin不触发策略回调,完成标记写这里
            DraedonStorySync.WriteDraedon(d => d.ExoMechEndingDialogue = true, d => d.ExoMechEndingDialogue = true);
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => DraedonStorySync.ReadDraedon(d => d.ExoMechEndingDialogue, d => d.ExoMechEndingDialogue),
            CanTrigger = (_, _) => false,
        };

    }
}
