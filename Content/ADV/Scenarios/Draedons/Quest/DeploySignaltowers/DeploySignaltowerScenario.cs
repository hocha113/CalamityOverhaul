using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    internal class DeploySignaltowerScenario : ADVScenarioBase, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(DeploySignaltowerScenario);

        public static bool Spawn { get; private set; }
        public static int RandTimer { get; private set; }

        //角色名称本地化
        public static LocalizedText DraedonName { get; private set; }

        //对话台词
        public static LocalizedText IntroLine1 { get; private set; }
        public static LocalizedText IntroLine2 { get; private set; }
        public static LocalizedText IntroLine3 { get; private set; }
        public static LocalizedText IntroLine4 { get; private set; }
        public static LocalizedText IntroLine5 { get; private set; }
        public static LocalizedText IntroLine6 { get; private set; }
        public static LocalizedText IntroLine7 { get; private set; }
        public static LocalizedText IntroLine8 { get; private set; }
        public static LocalizedText TechExplainLine1 { get; private set; }
        public static LocalizedText TechExplainLine2 { get; private set; }
        public static LocalizedText TaskLine { get; private set; }
        public static LocalizedText AcceptPrompt { get; private set; }

        //选项文本
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        private const string red = " ";
        private const string alt = " " + " ";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //介绍台词
            IntroLine1 = this.GetLocalization(nameof(IntroLine1), () => "我需要执行一项关键行动，而你是当前最适合协助我的个体");
            IntroLine2 = this.GetLocalization(nameof(IntroLine2), () => "两百个泰拉年前，一场能量风暴横扫星系，摧毁了我在泰拉的绝大部分设施");
            IntroLine3 = this.GetLocalization(nameof(IntroLine3), () => "一切都太过遥远，当我意识到和泰拉的意识体断连时，已经两百年过去了");
            IntroLine4 = this.GetLocalization(nameof(IntroLine4), () => "在那场风暴后，星际跃迁开始变得极度不稳定，因此我必须重建泰拉上的基础系统");
            IntroLine5 = this.GetLocalization(nameof(IntroLine5), () => "我需要你协助搭建最新的量子纠缠阵列");
            IntroLine6 = this.GetLocalization(nameof(IntroLine6), () => "这项技术可以突破光速限制，实现跨越星际的即时通讯");
            IntroLine7 = this.GetLocalization(nameof(IntroLine7), () => "但在实际应用之前，我需要在这个世界建立足够多的量子纠缠节点");
            IntroLine8 = this.GetLocalization(nameof(IntroLine8), () => "这就是我设计的量子信号塔，它的核心采用了纠缠态稳定器和零点能量放大器");
            TechExplainLine1 = this.GetLocalization(nameof(TechExplainLine1), () => "每座信号塔都能与其他节点建立量子纠缠链接，形成一个覆盖全星系的通讯网络");
            TechExplainLine2 = this.GetLocalization(nameof(TechExplainLine2), () => "理论上，这个网络的传输延迟可以达到普朗克时间级别");
            TaskLine = this.GetLocalization(nameof(TaskLine), () => "你的任务是在世界各地部署这些信号塔，建立起完整的纠缠网络");
            AcceptPrompt = this.GetLocalization(nameof(AcceptPrompt), () => "那么，你愿意接受这个委托吗？");

            //选项
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "接受委托");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "以后再说");
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            DeploySignaltowerRender.RegisterShowEffect();//注册信号塔展示特效
        }

        protected override void OnScenarioComplete() {
            DeploySignaltowerRender.Cleanup();//清理信号塔展示特效
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2RedADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.DraedonADV, silhouette: false);

            //构建对话流程
            Add(DraedonName.Value, IntroLine1.Value);
            Add(DraedonName.Value + alt, IntroLine2.Value);
            Add(DraedonName.Value, IntroLine3.Value);
            Add(DraedonName.Value, IntroLine4.Value);
            Add(DraedonName.Value, IntroLine5.Value);
            Add(DraedonName.Value, IntroLine6.Value);
            Add(DraedonName.Value, IntroLine7.Value);
            //触发信号塔图片展示

            //展示信号塔图片的对话，带展示动画
            Add(DraedonName.Value + red, IntroLine8.Value, onStart: DeploySignaltowerRender.ShowTowerImage);

            Add(DraedonName.Value + alt, TechExplainLine1.Value);
            Add(DraedonName.Value, TechExplainLine2.Value);
            Add(DraedonName.Value + red, TaskLine.Value);

            //添加选择界面
            AddWithChoices(DraedonName.Value + red, AcceptPrompt.Value, [
                new Choice(ChoiceAccept.Value, OnAcceptQuest),
                new Choice(ChoiceDecline.Value, OnDeclineQuest)
            ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
        }

        internal static void SetTurnOn() {
            Spawn = true;
            RandTimer = Main.rand.Next(60 * 32, 60 * 50);//大概半分钟到一分钟之间触发
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.DeploySignaltowerQuestCompleted) {
                return;//任务已完成，不更新
            }
            if (!Spawn) {
                return;//未触发任务，不更新
            }
            if (EbnEffect.IsActive) {
                return;//避免在不合适的时候触发
            }
            if (CWRWorld.HasBoss || CWRWorld.BossRush) {
                return;//有Boss战斗时不触发
            }
            if (--RandTimer > 0) {
                return;//等待计时器
            }
            if (StartScenario()) {
                Spawn = false;
                RandTimer = 0;
            }
        }

        //玩家接受任务
        private void OnAcceptQuest() {
            //标记接受任务
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.DeploySignaltowerQuestAccepted = true;
            }

            //生成目标点位
            SignalTowerTargetManager.GenerateTargetPoints();

            //完成当前场景
            Complete();
            ScenarioManager.Reset<Choice_Accept>();
            ScenarioManager.Start<Choice_Accept>();
        }

        public class Choice_Accept : ADVScenarioBase, ILocalizedModType
        {
            public string LocalizationCategory => "ADV.DeploySignaltowerScenario";
            public override string Key => nameof(Choice_Accept);
            public static LocalizedText AcceptResponse { get; private set; }
            public static LocalizedText L1 { get; private set; }
            public static LocalizedText L2 { get; private set; }
            public static LocalizedText L3 { get; private set; }
            //设置场景默认使用嘉登科技风格
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;
            public override void SetStaticDefaults() {
                AcceptResponse = this.GetLocalization(nameof(AcceptResponse), () => "很好，我会将信号塔的建造蓝图传输给你。记得定期向我汇报部署进度");
                L1 = this.GetLocalization(nameof(L1), () => "这是第一批建材，数量有限。请优先完成信号塔基础框架");
                L2 = this.GetLocalization(nameof(L2), () => "当第一个纠缠节点建成后，空间导航链路将更加稳定");
                L3 = this.GetLocalization(nameof(L3), () => "到那时，我就能通过亚空间持续输送更多资源，用于更大规模的建造");
            }
            protected override void OnScenarioComplete() {
                DraedonEffect.IsActive = false;
                DraedonEffect.Send();
            }
            private static void Give(int id, int num) {
                ADVRewardPopup.ShowReward(id, num, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero
                    , styleProvider: () => ADVRewardPopup.RewardStyle.Draedon);
            }
            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2RedADV, silhouette: false);
                Add(DraedonName.Value, AcceptResponse.Value, onStart: () => Give(ModContent.ItemType<ConstructionBlueprintQET>(), 1));
                Add(DraedonName.Value, L1.Value, onStart: () => Give(CWRID.Item_ExoPrism, 8082 + Main.rand.Next(30)));//这里数量给个30的随机数，以增加真实感(所谓的真实感就是不确定性)
                Add(DraedonName.Value, L2.Value);
                Add(DraedonName.Value, L3.Value);
            }
        }

        //玩家拒绝任务
        private void OnDeclineQuest() {
            //标记拒绝任务
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.DeploySignaltowerQuestDeclined = true;
            }

            //完成当前场景
            Complete();
            ScenarioManager.Reset<Choice_Decline>();
            ScenarioManager.Start<Choice_Decline>();
        }

        public class Choice_Decline : ADVScenarioBase, ILocalizedModType
        {
            public string LocalizationCategory => "ADV.DeploySignaltowerScenario";
            public override string Key => nameof(Choice_Decline);
            public static LocalizedText DeclineResponse { get; private set; }
            //设置场景默认使用嘉登科技风格
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;
            public override void SetStaticDefaults() {
                DeclineResponse = this.GetLocalization(nameof(DeclineResponse), () => "我理解，当你准备好时随时可以来找我");
            }
            protected override void OnScenarioComplete() {
                DraedonEffect.IsActive = false;
                DraedonEffect.Send();
            }
            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.DraedonADV, silhouette: false);
                Add(DraedonName.Value, DeclineResponse.Value);
            }
        }
    }
}
