using CalamityOverhaul.Content.Narrative.Runtime;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.End.EternalBlazingNow
{
    /// <summary>永恒燃烧的如今，坏结局场景</summary>
    internal sealed class EternalBlazingNow : NarrativeScenario, ILocalizedModType
    {
        private const string Choice1Label = "choice1";

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
        public static LocalizedText Line11 { get; private set; }
        public static LocalizedText Line12 { get; private set; }
        public static LocalizedText Line13 { get; private set; }
        public static LocalizedText Line14 { get; private set; }
        public static LocalizedText Line15 { get; private set; }
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText AchievementTitle { get; private set; }
        public static LocalizedText AchievementTooltip { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "比目鱼");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "???硫火女巫???");
            Rolename3 = this.GetLocalization(nameof(Rolename3), () => "硫火女巫");

            Line1 = this.GetLocalization(nameof(Line1), () => "开什么玩笑......");
            Line2 = this.GetLocalization(nameof(Line2), () => "过去的身影都消失了......这些火......是在封锁过去？！");
            Line3 = this.GetLocalization(nameof(Line3), () => "这个女巫......她的力量果然不是普通的魔法");
            Line4 = this.GetLocalization(nameof(Line4), () => "不过就算是这种力量的对抗，我也有信心再让你死一遍");
            Line5 = this.GetLocalization(nameof(Line5), () => "我不会死......不过，也差不多了");
            Line6 = this.GetLocalization(nameof(Line6), () => "你们做得很好......或许，你们真的是他口中那个值得等待的“时代唯一”");
            Line7 = this.GetLocalization(nameof(Line7), () => "所以，我有最后一件事，想拜托你们");
            Line8 = this.GetLocalization(nameof(Line8), () => "只要这世间的过去与现在，还存有一缕硫磺火，“我”就不会消亡");
            Line9 = this.GetLocalization(nameof(Line9), () => "可我的意识，却会在这无尽的火海中被逐渐磨灭");
            Line10 = this.GetLocalization(nameof(Line10), () => "我最多还能撑三十年");
            Line11 = this.GetLocalization(nameof(Line11), () => "......所以，你想让他接替你？");
            Line12 = this.GetLocalization(nameof(Line12), () => "没错，这是唯一的办法");
            Line13 = this.GetLocalization(nameof(Line13), () => "当我的意识彻底消散，整个世界都会被焚尽");
            Line14 = this.GetLocalization(nameof(Line14), () => "况且，如果你们想终结这个时代，凡人的躯壳太过脆弱......");
            Line15 = this.GetLocalization(nameof(Line15), () => "我绝对不允许！让他变成你这副鬼样子？！先从我的尸体上跨过去吧！");

            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "......");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(阻止比目鱼)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");
            AchievementTitle = this.GetLocalization(nameof(AchievementTitle), () => "BE结局：永恒燃烧的现在");
            AchievementTooltip = this.GetLocalization(nameof(AchievementTooltip), () => "往日被烈火所吞噬，以异类之躯触及永恒");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Amazed", Line1.Value)
             .Say("Helen", "Amazed", Line2.Value)
             .Say("Helen", "Amazed", Line3.Value)
             .Say("Helen", "Serious2", Line4.Value)
             .Say("SupCalShadow", Line5.Value)
             .Say("SupCalShadow", Line6.Value)
             .Say("SupCalShadow", Line7.Value)
             .Say("SupCalShadow", "BeTo", Line8.Value)
             .Say("SupCalShadow", Line9.Value)
             .Say("SupCalShadow", Line10.Value)
             .Say("Helen", "Serious2", Line11.Value)
             .Say("SupCalShadow", "BeTo", Line12.Value)
             .Say("SupCalShadow", "BeTo", Line13.Value)
             .Say("SupCalShadow", "BeTo", Line14.Value, onEnter: ScreenJitter)
             .Choice("Helen", "Wrath", Line15.Value, c => c
                 .Option("stop", Choice1Text.Value, NarrativeTarget.Goto(Choice1Label), onSelect: OnChoice1))
             .Label(Choice1Label)
             .Command(BeginChoice1Scenario)
             .End();
        }

        protected override void OnStarted() {
            EbnEffect.IsActive = true;
            MusicToast.ShowMusic(
                title: "罪之楔",
                artist: "腐姬",
                albumCover: ADVAsset.FUJI,
                style: MusicToast.MusicStyle.RedNeon,
                displayDuration: 480);
        }

        private static void ScreenJitter() {
            PunchCameraModifier modifier = new PunchCameraModifier(
                Main.LocalPlayer.Center,
                (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(),
                30f, 6f, 20, 1000f,
                nameof(EternalBlazingNow));
            Main.instance.CameraModifiers.Add(modifier);
        }

        private static void OnChoice1() {
            HalibutStorySync.WriteSupCal(
                d => d.EternalBlazingNowChoice1 = true,
                d => d.EternalBlazingNowChoice1 = true);
        }

        private static void BeginChoice1Scenario() => NarrativeRouter.Begin<EternalBlazingNowChoice1>();
    }
}
