using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums
{
    internal class ExoMechdusaSum : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
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
        /// <summary>
        /// 启用兼容模式，默认使用原版选择UI而非覆盖使用选项框模式
        /// </summary>
        public static bool CompatibleMode {
            get {
                if (CWRMod.Instance.fargowiltasCrossmod != null) {
                    return true;//我爱你FargowiltasCrossmod
                }
                if (CWRMod.Instance.infernum != null && ModGanged.InfernumModeOpenState) {
                    return true;//我爱你InfernumMode
                }
                if (CWRMod.Instance.woTM != null) {
                    return true;//我爱你WoTM
                }
                return false;
            }
        }
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

            //如果不是兼容模式，才注册机甲选择悬停特效
            if (!CompatibleMode) {
                ExoMechdusaSumRender.RegisterHoverEffects();
            }
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

            //如果不是兼容模式，才清理机甲选择悬停特效
            if (!CompatibleMode) {
                ExoMechdusaSumRender.Cleanup();
            }
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
                if (CompatibleMode) {
                    //兼容模式：显示对话但不显示选项框，依赖原版选项UI
                    Add(DraedonName.Value + red, BossRushLine.Value, onComplete: () => {
                        //对话结束后，重新启用原版选择UI
                        CWRRef.SetAbleToSelectExoMech(Main.LocalPlayer, true);
                        //场景不自动完成，等待玩家选择
                    });
                }
                else {
                    //正常模式：显示自定义选项框
                    AddWithChoices(DraedonName.Value + red, BossRushLine.Value, [
                        new Choice(ChoiceAres.Value, () => SummonMech(ExoMechType.Prime)),
                        new Choice(ChoiceThanatos.Value, () => SummonMech(ExoMechType.Destroyer)),
                        new Choice(ChoiceTwins.Value, () => SummonMech(ExoMechType.Twins))
                    ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
                }
            }
            else {
                //普通模式，就播放老逼登的完整介绍对话
                Add(DraedonName.Value + alt, IntroLine1.Value);
                Add(DraedonName.Value, IntroLine2.Value);
                Add(DraedonName.Value, IntroLine3.Value);
                Add(DraedonName.Value + red, IntroLine4.Value);

                if (CompatibleMode) {
                    //兼容模式：显示最后一句对话后重新启用原版选择UI
                    Add(DraedonName.Value + red, IntroLine5.Value, onComplete: () => {
                        //对话结束后，重新启用原版选择UI
                        CWRRef.SetAbleToSelectExoMech(Main.LocalPlayer, true);
                        //场景不自动完成，等待玩家选择
                    });
                }
                else {
                    //正常模式：添加选择界面
                    AddWithChoices(DraedonName.Value + red, IntroLine5.Value, [
                        new Choice(ChoiceAres.Value, () => SummonMech(ExoMechType.Prime)),
                        new Choice(ChoiceThanatos.Value, () => SummonMech(ExoMechType.Destroyer)),
                        new Choice(ChoiceTwins.Value, () => SummonMech(ExoMechType.Twins))
                    ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
                }
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (CountDown) {
                if (CountDownTimer > 0) {
                    CountDownTimer--;
                }
                else {
                    ExoMechType[] mechOptions = [ExoMechType.Destroyer, ExoMechType.Prime, ExoMechType.Twins];
                    ExoMechType selectedMech = mechOptions[Main.rand.Next(mechOptions.Length)];
                    SummonMech(selectedMech);
                    SimpleMode = false;
                    CountDown = false;
                    CountDownTimer = 0;
                    //完成当前场景
                    Complete();
                    ADVChoiceBox.Hide();//手动清理选项框
                    DialogueUIRegistry.ForceCloseBox(DefaultDialogueStyle());
                }
            }
            if (CompatibleMode && DraedonEffect.IsActive && CWRRef.HasExo()) {
                //完成当前场景
                Complete();
                ADVChoiceBox.Hide();//手动清理选项框
                DialogueUIRegistry.ForceCloseBox(DefaultDialogueStyle());
            }
        }

        private void SummonMech(ExoMechType mechType) {
            //召唤设置的机械类型
            CWRRef.SummonExo((int)mechType, Main.LocalPlayer);
            //完成当前场景
            Complete();
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
