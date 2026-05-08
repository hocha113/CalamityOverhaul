using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.ADV.Scenarios.Shepel.CybCourses;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel
{
    /// <summary>
    /// 初遇SHPC场景，首次获得SHPC时触发
    /// </summary>
    internal class FirstMetShepel : ADVScenarioBase, ILocalizedModType
    {
        //角色名称
        public static LocalizedText RolenameSHPC { get; private set; }
        //对话文本
        public static LocalizedText Line1 { get; private set; }

        //选择分支
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice1Silence { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }

        //Choice1后的超梦接入询问文本
        public static LocalizedText CybCourseOfferLine { get; private set; }
        public static LocalizedText CybCourseAcceptText { get; private set; }
        public static LocalizedText CybCourseDeclineText { get; private set; }
        public static LocalizedText CybCourseAcceptResponse { get; private set; }
        public static LocalizedText CybCourseDeclineResponse { get; private set; }

        //使用SHPC专属赛博女仆风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle
            => () => SHPCDialogueBox.Instance;

        public override void SetStaticDefaults() {
            RolenameSHPC = this.GetLocalization(nameof(RolenameSHPC), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1), () => "主人！很高兴再见到您！");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "你认错人了吧？");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "...好久不见");
            Choice1Silence = this.GetLocalization(nameof(Choice1Silence), () => "......");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "...是的，只要是您，我每次都愿意认错");
            CybCourseOfferLine = this.GetLocalization(nameof(CybCourseOfferLine), () => "超梦程序已成功录入。主人，是否现在接入超梦空间，学习骇客模式及SHPC武器操作规范？");
            CybCourseAcceptText = this.GetLocalization(nameof(CybCourseAcceptText), () => "现在就去");
            CybCourseDeclineText = this.GetLocalization(nameof(CybCourseDeclineText), () => "暂时不去");
            CybCourseAcceptResponse = this.GetLocalization(nameof(CybCourseAcceptResponse), () => "收到，正在建立神经链路……请保持稳定。");
            CybCourseDeclineResponse = this.GetLocalization(nameof(CybCourseDeclineResponse), () => "了解。超梦接入凭证已写入您的存档，主人随时可以自行激活。");
        }

        protected override ScenarioPolicy ConfigurePolicy() => new() {
            IsCompleted = save => save.Get<ShepelADVData>().FirstSHPCObtained,
            MarkCompleted = save => save.Get<ShepelADVData>().FirstSHPCObtained = true,
            CanTrigger = (save, player) => player.HasItem(SHPCOverride.ID),
            BlockedBy = ScenarioBlockers.Boss | ScenarioBlockers.BossRush | ScenarioBlockers.ActiveScenario,
        };

        internal static bool CanStartSHPCTrialQuests(Player player) {
            if (player == null || !player.active || !player.HasItem(SHPCOverride.ID)) {
                return false;
            }

            if (!player.TryGetADVSave(out var save)) {
                return false;
            }

            var data = save.Get<ShepelADVData>();
            if (data.FirstSHPCIntroCompleted) {
                return true;
            }

            if (data.FirstSHPCObtained && !ScenarioManager.IsActive()) {
                data.FirstSHPCIntroCompleted = true;
                return true;
            }

            return false;
        }

        private static void MarkFirstSHPCIntroCompleted() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<ShepelADVData>().FirstSHPCIntroCompleted = true;
            }
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RolenameSHPC.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RolenameSHPC.Value, silhouette: false);

            //对话 + 选择
            AddWithChoices(RolenameSHPC.Value, Line1.Value, [
                new Choice(Choice1Text.Value, Choice1),
                new Choice(Choice2Text.Value, Choice2, enabled: false, disabledHint: string.Empty),
            ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.SHPC);
        }

        protected override void OnScenarioStart() {
            SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.SkipFadeIn();
                portrait.currentFace = ShepelFullBodyPortrait.Face.None;
            }
        }

        public void Choice1() {
            ScenarioManager.Start<FirstMetShepel_Choice1>();
            Complete();
        }

        public void Choice2() {
            //TODO
            MarkFirstSHPCIntroCompleted();
            Complete();
        }

        private class FirstMetShepel_Choice1 : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetShepel_Choice1);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle
                => () => SHPCDialogueBox.Instance;
            protected override void Build() {
                Add(RolenameSHPC.Value, Choice1Silence.Value, onStart: () => {
                    if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                        portrait.TriggerGlitch(1f, 1f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(CWRSound.Fault);
                        }
                    }
                });
                Add(RolenameSHPC.Value, Choice1Response.Value, onStart: () => {
                    if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                        portrait.currentFace = ShepelFullBodyPortrait.Face.Smirk;
                    }
                });
            }
            protected override void OnScenarioStart() {
                SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
                if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                    portrait.SkipFadeIn();
                    portrait.currentFace = ShepelFullBodyPortrait.Face.None;
                }
            }
            protected override void OnScenarioComplete() {
                ScenarioManager.Start<FirstMetShepel_CybCourseOffer>();
            }
        }

        //Choice1结束后的超梦接入询问场景
        private class FirstMetShepel_CybCourseOffer : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetShepel_CybCourseOffer);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle
                => () => SHPCDialogueBox.Instance;
            protected override void Build() {
                AddWithChoices(RolenameSHPC.Value, CybCourseOfferLine.Value, [
                    new Choice(CybCourseAcceptText.Value, AcceptCybCourse),
                    new Choice(CybCourseDeclineText.Value, DeclineCybCourse),
                ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.SHPC);
            }
            protected override void OnScenarioStart() {
                SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
                if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                    portrait.SkipFadeIn();
                    portrait.currentFace = ShepelFullBodyPortrait.Face.None;
                }
            }
            public void AcceptCybCourse() {
                ScenarioManager.Start<FirstMetShepel_CybCourseAccept>();
                Complete();
            }
            public void DeclineCybCourse() {
                ScenarioManager.Start<FirstMetShepel_CybCourseDecline>();
                Complete();
            }
        }

        //接受超梦，进入CybCourse
        private class FirstMetShepel_CybCourseAccept : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetShepel_CybCourseAccept);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle
                => () => SHPCDialogueBox.Instance;
            protected override void Build() {
                Add(RolenameSHPC.Value, CybCourseAcceptResponse.Value, onStart: () => {
                    if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                        portrait.currentFace = ShepelFullBodyPortrait.Face.Serious;
                    }
                });
            }
            protected override void OnScenarioStart() {
                SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
                if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                    portrait.SkipFadeIn();
                    portrait.currentFace = ShepelFullBodyPortrait.Face.None;
                }
            }
            protected override void OnScenarioComplete() {
                MarkFirstSHPCIntroCompleted();
                CybCourse.ScheduleMewtwoGrant();
                if (Main.myPlayer == Main.LocalPlayer.whoAmI) {
                    CybCourse.Enter();
                }
            }
        }

        //拒绝超梦，给予Mewtwo道具
        private class FirstMetShepel_CybCourseDecline : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetShepel_CybCourseDecline);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle
                => () => SHPCDialogueBox.Instance;
            protected override void Build() {
                Add(RolenameSHPC.Value, CybCourseDeclineResponse.Value, onStart: () => {
                    if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                        portrait.currentFace = ShepelFullBodyPortrait.Face.Blank;
                    }
                    ADVRewardPopup.ShowReward(
                        ModContent.ItemType<Mewtwo>(),
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
                        styleProvider: () => ADVRewardPopup.RewardStyle.Draedon
                    );
                });
            }
            protected override void OnScenarioStart() {
                SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
                if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                    portrait.SkipFadeIn();
                    portrait.currentFace = ShepelFullBodyPortrait.Face.None;
                }
            }
            protected override void OnScenarioComplete() {
                MarkFirstSHPCIntroCompleted();
            }
        }
    }
}
