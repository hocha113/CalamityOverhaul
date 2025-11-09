using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 永恒燃烧的如今，坏结局场景
    /// </summary>
    internal class EternalBlazingNow : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(EternalBlazingNow);
        public string LocalizationCategory => "ADV";

        //角色名称
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }

        //对话台词
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

        //选项文本
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }

        //选择1后的对话
        public static LocalizedText Choice1Line1 { get; private set; }
        public static LocalizedText Choice1Line2 { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        //比目鱼表情常量
        internal const string helenShock = " ";
        internal const string helenSolemn = " " + " ";
        internal const string helenWrath = " " + " " + " ";

        //至尊灾厄表情常量
        private const string supCalDespise = " ";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "比目鱼");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "???硫火女巫???");

            Line1 = this.GetLocalization(nameof(Line1), () => "开什么玩笑......");
            Line2 = this.GetLocalization(nameof(Line2), () => "过去的身影都消失了......这些火......是在封锁过去？！");
            Line3 = this.GetLocalization(nameof(Line3), () => "这火从过去一直燃烧到现在......这个女巫......她的力量果然不是普通的魔法");
            Line4 = this.GetLocalization(nameof(Line4), () => "刚才的战斗一直在隐藏吗，不过就算是这种力量的对抗，我也有信心再让你死一遍");
            Line5 = this.GetLocalization(nameof(Line5), () => "我不会死......不过，也差不多了");
            Line6 = this.GetLocalization(nameof(Line6), () => "你们做得很好......或许，你们真的是他口中那个值得等待的“时代唯一”");
            Line7 = this.GetLocalization(nameof(Line7), () => "所以，我有最后一件事，想拜托你们");
            Line8 = this.GetLocalization(nameof(Line8), () => "只要这世间的过去与现在，还存有一缕硫磺火，“我”就不会消亡");
            Line9 = this.GetLocalization(nameof(Line9), () => "可我的意识，却会在这无尽的火海中被逐渐磨灭");
            Line10 = this.GetLocalization(nameof(Line10), () => "如果没有遇到你们，我最多还能撑三十年");
            Line11 = this.GetLocalization(nameof(Line11), () => "......即使是完美的异类，也逃不过意识被侵蚀殆尽的结局吗？所以，你想让他继承你的躯体？");
            Line12 = this.GetLocalization(nameof(Line12), () => "没错，这是唯一的办法");
            Line13 = this.GetLocalization(nameof(Line13), () => "当我的意识彻底消散，整个世界都会被焚尽");
            Line14 = this.GetLocalization(nameof(Line14), () => "况且，如果你们想终结这个时代，凡人的躯壳太过脆弱......就以我的躯体做你们迈向终点的踏板吧");
            Line15 = this.GetLocalization(nameof(Line15), () => "我绝对不允许！让他变成你这副鬼样子？！先从我的尸体上跨过去吧！");

            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "......");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(阻止比目鱼)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");

            Choice1Line1 = this.GetLocalization(nameof(Choice1Line1), () => "......这就是你的选择吗？");
            Choice1Line2 = this.GetLocalization(nameof(Choice1Line2), () => "我明白了......");
        }

        protected override void Build() {
            //注册比目鱼立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + helenShock, ADVAsset.Helen_amazeADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + helenShock, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + helenSolemn, ADVAsset.Helen2ADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + helenSolemn, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + helenWrath, ADVAsset.Helen_wrathADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + helenWrath, silhouette: false);

            //注册至尊灾厄立绘
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: true);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + supCalDespise, ADVAsset.SupCalADV[3]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + supCalDespise, silhouette: true);

            /*
            //检查是否拥有比目鱼
            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            if (hasHalibut) {
                //有比目鱼版本对话
                
            }
            else {
                //无比目鱼版本
                Add(Rolename2.Value, Line5.Value);
                Add(Rolename2.Value, Line6.Value);
                Add(Rolename2.Value, Line7.Value, onComplete: Complete);
            }
            */
            Add(Rolename1.Value + helenShock, Line1.Value);
            Add(Rolename1.Value + helenShock, Line2.Value);
            Add(Rolename1.Value + helenShock, Line3.Value);
            Add(Rolename1.Value + helenShock, Line4.Value);
            Add(Rolename2.Value, Line5.Value);
            Add(Rolename2.Value, Line6.Value);
            Add(Rolename2.Value, Line7.Value);
            Add(Rolename2.Value + supCalDespise, Line8.Value);
            Add(Rolename2.Value, Line9.Value);
            Add(Rolename2.Value, Line10.Value);
            Add(Rolename1.Value + helenShock, Line11.Value);
            Add(Rolename2.Value + supCalDespise, Line12.Value);
            Add(Rolename2.Value + supCalDespise, Line13.Value);
            Add(Rolename2.Value + supCalDespise, Line14.Value, Screenjittering);
            Add(Rolename1.Value + helenWrath, Line15.Value);

            //添加选项
            AddWithChoices(Rolename1.Value + helenWrath, QuestionLine.Value, [
                new Choice(Choice1Text.Value, Choice1),
                new Choice(Choice2Text.Value, Choice2, enabled: false, disabledHint: string.Empty),
            ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Brimstone);
        }

        protected override void OnScenarioStart() {
            //开始生成粒子效果
            EbnEffect.IsActive = true;
        }

        private void Screenjittering() {
            PunchCameraModifier modifier = new PunchCameraModifier(Main.LocalPlayer.Center
                , (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
            Main.instance.CameraModifiers.Add(modifier);
        }

        private void Choice1() {
            //选择1：阻止比目鱼拼命
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.EternalBlazingNowChoice1 = true;
            }
            ScenarioManager.Reset<EternalBlazingNow_Choice1>();
            ScenarioManager.Start<EternalBlazingNow_Choice1>();
            Complete();
        }

        internal class EternalBlazingNow_Choice1 : ADVScenarioBase
        {
            public override string Key => nameof(EternalBlazingNow_Choice1);
            //设置场景默认使用硫磺火风格
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
            protected override void OnScenarioStart() {
                //开始生成粒子效果
                EbnEffect.IsActive = true;
            }
            protected override void OnScenarioComplete() {
                EbnEffect.IsActive = false;
            }
            protected override void Build() {
                //选择阻止比目鱼
                Add(Rolename1.Value + helenSolemn, Choice1Line1.Value);
                Add(Rolename1.Value, Choice1Line2.Value);
            }
        }

        private void Choice2() {
            //选择2：保持沉默
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.EternalBlazingNowChoice2 = true;
            }
            Complete();
            //停止粒子生成
            EbnEffect.IsActive = false;
        }
    }
}
