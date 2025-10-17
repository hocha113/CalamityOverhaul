using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios
{
    internal class FirstMet : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMet);
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Line0 { get; private set; }
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
        public static LocalizedText Line16 { get; private set; }
        public static LocalizedText Line17 { get; private set; }
        public static LocalizedText Line18 { get; private set; }
        public static LocalizedText Line19 { get; private set; }
        public static LocalizedText Line20 { get; private set; }
        public static LocalizedText Line21 { get; private set; }
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        //设置场景默认使用海洋风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");
            Line0 = this.GetLocalization(nameof(Line0), () => "先生......你的钓鱼技法，还有一些进步空间");
            Line1 = this.GetLocalization(nameof(Line1), () => "你可以叫我比目鱼博士");
            Line2 = this.GetLocalization(nameof(Line2), () => "或者，直接叫我比目鱼也行");
            Line3 = this.GetLocalization(nameof(Line3), () => "多亏了你的鱼钩，我才从那片死寂的水域里被拽了出来");
            Line4 = this.GetLocalization(nameof(Line4), () => "要不是你，我大概会一直卡在死机状态，直到真的死去吧");
            Line5 = this.GetLocalization(nameof(Line5), () => "这条鱼本该是你的收获，现在它成了我们的见面礼");
            Line6 = this.GetLocalization(nameof(Line6), () => "......");
            Line7 = this.GetLocalization(nameof(Line7), () => "你是不是在想，我为什么会出现在这里？");
            Line8 = this.GetLocalization(nameof(Line8), () => "我原本是硫磺海大学深渊研究科的教授");
            Line9 = this.GetLocalization(nameof(Line9), () => "几年前，我们筹备了一场‘深渊生态采样计划’");
            Line10 = this.GetLocalization(nameof(Line10), () => "最后能回来的，只剩我一个");
            Line11 = this.GetLocalization(nameof(Line11), () => "我们在深渊底部，遇到了某种......无法被理解的存在");
            Line12 = this.GetLocalization(nameof(Line12), () => "它不可能是生物，也不是能被‘杀死’的东西......更像是，概念本身的恐怖");
            Line13 = this.GetLocalization(nameof(Line13), () => "我在逃亡时，从一棵珊瑚树下发现了一只眼睛");
            Line14 = this.GetLocalization(nameof(Line14), () => "出于求生本能，我吞下了它");
            Line15 = this.GetLocalization(nameof(Line15), () => "借助那只眼睛的力量，我开启了一个领域，从深渊中撕开了一条逃生的缝隙");
            Line16 = this.GetLocalization(nameof(Line16), () => "之后的记忆就模糊了......我陷入沉睡，直到被你钓起来");
            Line17 = this.GetLocalization(nameof(Line17), () => "你看起来是个泰拉人");
            Line18 = this.GetLocalization(nameof(Line18), () => "如果你愿意，带上我吧");
            Line19 = this.GetLocalization(nameof(Line19), () => "我会帮你征服这片陆地");
            Line20 = this.GetLocalization(nameof(Line20), () => "作为交换......帮我解决体内那些眼球的复苏问题");
            Line21 = this.GetLocalization(nameof(Line21), () => "没有问题的话，我们就从研究这条鱼开始吧");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            Add(Rolename1.Value, Line0.Value);
            Add(Rolename2.Value, Line1.Value);
            Add(Rolename2.Value, Line2.Value);
            Add(Rolename2.Value, Line3.Value);
            Add(Rolename2.Value, Line4.Value);
            Add(Rolename2.Value, Line5.Value);//5 触发奖励
            Add(Rolename2.Value, Line6.Value);
            Add(Rolename2.Value, Line7.Value);
            Add(Rolename2.Value, Line8.Value);
            Add(Rolename2.Value, Line9.Value);
            Add(Rolename2.Value, Line10.Value);
            Add(Rolename2.Value, Line11.Value);
            Add(Rolename2.Value, Line12.Value);
            Add(Rolename2.Value, Line13.Value);
            Add(Rolename2.Value, Line14.Value);
            Add(Rolename2.Value, Line15.Value);
            Add(Rolename2.Value, Line16.Value);
            Add(Rolename2.Value, Line17.Value);
            Add(Rolename2.Value, Line18.Value);
            Add(Rolename2.Value, Line19.Value);
            Add(Rolename2.Value, Line20.Value);
            Add(Rolename2.Value, Line21.Value);
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {//第一次手持大比目鱼时触发对话
            if (!halibutPlayer.HeldHalibut) {
                return;
            }

            if (save.FirstMet) {
                return;
            }

            if (ScenarioManager.Start<FirstMet>()) {
                save.FirstMet = true;
            }
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 5) {
                ADVRewardPopup.ShowReward(ItemID.Bass, 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
    }
}