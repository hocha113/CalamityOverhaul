using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    internal class FirstMetOldDuke : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public override string LocalizationCategory => "ADV";

        //设置默认对话框样式为硫磺海风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

        /// <summary>
        /// 玩家选择结果：0=未选择，1=接受合作，2=拒绝合作，3=拒绝并战斗
        /// </summary>
        public static int PlayerChoice = 0;

        //角色名称
        public static LocalizedText OldDukeName { get; private set; }
        public static LocalizedText HelenName { get; private set; }

        //对话台词
        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }

        //比目鱼台词（如果有比目鱼）
        public static LocalizedText HL1 { get; private set; }
        public static LocalizedText HL2 { get; private set; }

        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }
        public static LocalizedText C3 { get; private set; }

        //选项回应
        public static LocalizedText C1Response { get; private set; }
        public static LocalizedText C2Response { get; private set; }
        public static LocalizedText C3Response { get; private set; }

        public override bool IsLoadingEnabled(Mod mod) {
            return false;//没做完，禁用
        }

        void IWorldInfo.OnWorldLoad() {
            //重置选择状态
            PlayerChoice = 0;
        }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老公爵");
            HelenName = this.GetLocalization(nameof(HelenName), () => "比目鱼");

            L0 = this.GetLocalization(nameof(L0), () => "......");
            L1 = this.GetLocalization(nameof(L1), () => "没必要一见面就拔刀相向");
            L2 = this.GetLocalization(nameof(L2), () => "我年纪大了，不想打架");
            L3 = this.GetLocalization(nameof(L3), () => "我们可以合作");
            L4 = this.GetLocalization(nameof(L4), () => "我一直在收集这种海洋残片");
            L5 = this.GetLocalization(nameof(L5), () => "如果你可以帮我找来更多，我会给予相应的报酬");
            L6 = this.GetLocalization(nameof(L6), () => "作为见面礼，这是一份样本");

            //比目鱼台词
            HL1 = this.GetLocalization(nameof(HL1), () => "老教授......?不过他看起来不认识我了");
            HL2 = this.GetLocalization(nameof(HL2), () => "他的话是可信的，我们硫磺海大学的同事，他是海洋考古学领域的泰斗");

            C1 = this.GetLocalization(nameof(C1), () => "接受合作");
            C2 = this.GetLocalization(nameof(C2), () => "拒绝合作");
            C3 = this.GetLocalization(nameof(C3), () => "拒绝合作并拔出武器");

            C1Response = this.GetLocalization(nameof(C1Response), () => "很好，希望我们能有一个愉快的合作");
            C2Response = this.GetLocalization(nameof(C2Response), () => "......那我就先离开了，如果你改变主意，随时可以再来找我");
            C3Response = this.GetLocalization(nameof(C3Response), () => "......看来你执意如此，那就让我来称量称量你吧");
        }

        protected override void OnScenarioStart() {
            //开启硫磺海效果
            OldDukeEffect.IsActive = true;
            //重置选择
            PlayerChoice = 0;
        }

        protected override void Build() {
            //注册立绘（老公爵暂时没有专属立绘，使用null）
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(OldDukeName.Value, silhouette: true);

            //检查是否有比目鱼
            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            if (hasHalibut) {
                //注册比目鱼立绘
                DialogueBoxBase.RegisterPortrait(HelenName.Value, ADVAsset.Helen_doubtADV);
                DialogueBoxBase.SetPortraitStyle(HelenName.Value, silhouette: false);

                //带比目鱼的对话流程
                Add(OldDukeName.Value, L0.Value);
                Add(OldDukeName.Value, L1.Value);
                Add(OldDukeName.Value, L2.Value);
                Add(HelenName.Value, HL1.Value);
                Add(HelenName.Value, HL2.Value);
                Add(OldDukeName.Value, L3.Value);
                Add(OldDukeName.Value, L4.Value);
                Add(OldDukeName.Value, L5.Value);
            }
            else {
                //无比目鱼的对话流程
                Add(OldDukeName.Value, L0.Value);
                Add(OldDukeName.Value, L1.Value);
                Add(OldDukeName.Value, L2.Value);
                Add(OldDukeName.Value, L3.Value);
                Add(OldDukeName.Value, L4.Value);
                Add(OldDukeName.Value, L5.Value);
            }

            //添加选项
            AddWithChoices(
                OldDukeName.Value,
                L6.Value,
                [
                    new Choice(C1.Value, Choice1),
                    new Choice(C2.Value, Choice2),
                    new Choice(C3.Value, Choice3),
                ],
                onStart: GiveOceanFragment,
                styleOverride: () => SulfseaDialogueBox.Instance,
                choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
            );
        }

        /// <summary>
        /// 给予海洋残片样本
        /// </summary>
        public static void GiveOceanFragment() {
            ADVRewardPopup.ShowReward(
                ModContent.ItemType<Oceanfragments>(),
                1,
                "",
                appearDuration: 24,
                holdDuration: -1,
                giveDuration: 16,
                requireClick: true,
                anchorProvider: () => {
                    var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                    if (rect == Rectangle.Empty) {
                        return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                    }
                    return new Vector2(rect.Center.X, rect.Y - 70f);
                },
                offset: Vector2.Zero,
                styleProvider: () => ADVRewardPopup.RewardStyle.Sulfsea
            );
        }

        //选项1：接受合作
        public void Choice1() {
            PlayerChoice = 1;
            ScenarioManager.Start<FirstMetOldDuke_Choice1>();
            Complete();
        }

        private class FirstMetOldDuke_Choice1 : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetOldDuke_Choice1);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, C1Response.Value);
            }

            protected override void OnScenarioComplete() {
                //保存状态：接受合作
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ADVSave.OldDukeCooperationAccepted = true;
                    halibutPlayer.ADVSave.OldDukeCooperationDeclined = false;
                    halibutPlayer.ADVSave.OldDukeChoseToFight = false;
                }

                //通知NPC执行离开AI
                //NPC会在ModifyOldDuke中检测PlayerChoice并执行相应AI

                //停止硫磺海效果
                OldDukeEffect.IsActive = false;
            }
        }

        //选项2：拒绝合作
        public void Choice2() {
            PlayerChoice = 2;
            ScenarioManager.Start<FirstMetOldDuke_Choice2>();
            Complete();
        }

        private class FirstMetOldDuke_Choice2 : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetOldDuke_Choice2);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, C2Response.Value);
            }

            protected override void OnScenarioComplete() {
                //保存状态：拒绝合作（但没有战斗，下次还可以选择）
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ADVSave.OldDukeCooperationDeclined = true;
                    halibutPlayer.ADVSave.OldDukeCooperationAccepted = false;
                    halibutPlayer.ADVSave.OldDukeChoseToFight = false;
                }

                //通知NPC执行离开AI
                //NPC会在ModifyOldDuke中检测PlayerChoice并执行相应AI

                //停止硫磺海效果
                OldDukeEffect.IsActive = false;
            }
        }

        //选项3：拒绝合作并拔出武器
        public void Choice3() {
            PlayerChoice = 3;
            ScenarioManager.Start<FirstMetOldDuke_Choice3>();
            Complete();
        }

        private class FirstMetOldDuke_Choice3 : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetOldDuke_Choice3);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, C3Response.Value);
            }

            protected override void OnScenarioComplete() {
                //保存状态：选择战斗，以后直接进入战斗
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ADVSave.OldDukeChoseToFight = true;
                    halibutPlayer.ADVSave.OldDukeCooperationAccepted = false;
                    halibutPlayer.ADVSave.OldDukeCooperationDeclined = false;
                }

                //通知NPC开始战斗
                //NPC会在ModifyOldDuke中检测PlayerChoice=3并恢复战斗AI

                //停止硫磺海效果
                OldDukeEffect.IsActive = false;
            }
        }

        protected override void OnScenarioComplete() {
            //场景完成时停止硫磺海效果
            OldDukeEffect.IsActive = false;
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            //场景触发逻辑移至ModifyOldDuke中处理
        }
    }
}
