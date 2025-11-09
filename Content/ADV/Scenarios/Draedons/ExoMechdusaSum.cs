using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ExoMechdusaSum : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(ExoMechdusaSum);

        //角色名称本地化
        public static LocalizedText DraedonName { get; private set; }

        //介绍台词
        public static LocalizedText IntroLine1 { get; private set; }
        public static LocalizedText IntroLine2 { get; private set; }
        public static LocalizedText IntroLine3 { get; private set; }
        public static LocalizedText IntroLine4 { get; private set; }
        public static LocalizedText IntroLine5 { get; private set; }

        //选择提示
        public static LocalizedText SelectionPrompt { get; private set; }

        //机械选项文本
        public static LocalizedText ChoiceAres { get; private set; }
        public static LocalizedText ChoiceThanatos { get; private set; }
        public static LocalizedText ChoiceTwins { get; private set; }

        //Boss Rush模式文本
        public static LocalizedText BossRushLine { get; private set; }

        /// <summary>
        /// 是否启用简洁模式，如果是，跳过介绍直接选择机甲
        /// </summary>
        public static bool SimpleMode;
        public static bool CountDown;
        public static int CountDownTimer;

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        private const string red = " ";
        private const string alt = " " + " ";

        void IWorldInfo.OnWorldLoad() {
            SimpleMode = false;
            CountDown = false;
            CountDownTimer = 0;
        }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //介绍台词(对应原游戏的 DraedonIntroductionText 系列)
            IntroLine1 = this.GetLocalization(nameof(IntroLine1), () => "你知道吗？这一刻已经等了太久了");
            IntroLine2 = this.GetLocalization(nameof(IntroLine2), () => "我对一切未知感到着迷，但最让我着迷的莫过于你的本质");
            IntroLine3 = this.GetLocalization(nameof(IntroLine3), () => "我将会向你展示，我那些超越神明的造物");
            IntroLine4 = this.GetLocalization(nameof(IntroLine4), () => "而你，则将在战斗中向我展示你的本质");
            IntroLine5 = this.GetLocalization(nameof(IntroLine5), () => "现在，选择吧");

            SelectionPrompt = this.GetLocalization(nameof(SelectionPrompt), () => "做出你的选择");
            BossRushLine = this.GetLocalization(nameof(BossRushLine), () => "做出你的选择。你有20秒的时间");

            //机械选项
            ChoiceAres = this.GetLocalization(nameof(ChoiceAres), () => "战神阿瑞斯");
            ChoiceThanatos = this.GetLocalization(nameof(ChoiceThanatos), () => "死神塔纳托斯");
            ChoiceTwins = this.GetLocalization(nameof(ChoiceTwins), () => "双子神阿尔忒弥斯");
        }

        protected override void OnScenarioStart() {
            if (SimpleMode) {
                CountDown = true;
                CountDownTimer = 60 * 20;
            }
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            ExoMechdusaSumRender.RegisterHoverEffects();//注册机甲选择悬停特效
        }

        protected override void OnScenarioComplete() {
            //标记已观看机甲嘉登的第一次对话场景
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.FristExoMechdusaSum = true;//标记已触发机甲嘉登场景
            }
            SimpleMode = false;
            CountDown = false;
            CountDownTimer = 0;
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            ExoMechdusaSumRender.Cleanup();//清理机甲选择悬停特效
        }

        protected override void Build() {
            //注册嘉登立绘(使用科技风格的剪影效果)
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2RedADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.DraedonADV, silhouette: false);

            if (Main.LocalPlayer.TryGetADVSave(out var save) && save.FristExoMechdusaSum) {
                SimpleMode = true;//如果是非第一次触发机甲嘉登场景，则启用简洁模式
            }
            //检查是否为简洁模式
            bool simpleMode = CWRRef.GetBossRushActive() || SimpleMode;

            if (simpleMode) {
                //简洁模式，直接显示选择界面，时间紧迫，不等你嗷
                AddWithChoices(DraedonName.Value + red, BossRushLine.Value, [
                    new Choice(ChoiceAres.Value, () => SummonMech(ExoMechType.Prime)),
                    new Choice(ChoiceThanatos.Value, () => SummonMech(ExoMechType.Destroyer)),
                    new Choice(ChoiceTwins.Value, () => SummonMech(ExoMechType.Twins))
                ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
            }
            else {
                //普通模式，就播放老逼登的完整介绍对话
                Add(DraedonName.Value + alt, IntroLine1.Value);
                Add(DraedonName.Value, IntroLine2.Value);
                Add(DraedonName.Value, IntroLine3.Value);
                Add(DraedonName.Value + red, IntroLine4.Value);

                //添加选择界面
                AddWithChoices(DraedonName.Value + red, IntroLine5.Value, [
                    new Choice(ChoiceAres.Value, () => SummonMech(ExoMechType.Prime)),
                    new Choice(ChoiceThanatos.Value, () => SummonMech(ExoMechType.Destroyer)),
                    new Choice(ChoiceTwins.Value, () => SummonMech(ExoMechType.Twins))
                ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (CountDown) {
                if (CountDownTimer > 0) {
                    CountDownTimer--;
                }
                else {
                    //时间到，随机选择一个机甲进行召唤
                    ExoMechType[] mechOptions = [ExoMechType.Destroyer, ExoMechType.Prime, ExoMechType.Twins];
                    ExoMechType selectedMech = mechOptions[Main.rand.Next(mechOptions.Length)];
                    SummonMech(selectedMech);
                    ADVChoiceBox.Hide();//手动清理选项框
                    CountDown = false;
                    CountDownTimer = 0;
                }
            }
        }

        private void SummonMech(ExoMechType mechType) {
            //召唤设置的机械类型
            CWRRef.SummonExo((int)mechType, Main.LocalPlayer);
            //完成当前场景
            Complete();
            DefaultDialogueStyle().BeginClose();
        }

        private enum ExoMechType
        {
            None = 0,
            Destroyer = 1,
            Prime = 2,
            Twins = 3
        }
    }
}
