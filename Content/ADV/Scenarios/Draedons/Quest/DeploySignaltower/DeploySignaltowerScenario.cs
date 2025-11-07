using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltower
{
    internal class DeploySignaltowerScenario : ADVScenarioBase, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(DeploySignaltowerScenario);

        //角色名称本地化
        public static LocalizedText DraedonName { get; private set; }

        //对话台词
        public static LocalizedText IntroLine1 { get; private set; }
        public static LocalizedText IntroLine2 { get; private set; }
        public static LocalizedText IntroLine3 { get; private set; }
        public static LocalizedText IntroLine4 { get; private set; }
        public static LocalizedText ShowTowerLine { get; private set; }
        public static LocalizedText TechExplainLine1 { get; private set; }
        public static LocalizedText TechExplainLine2 { get; private set; }
        public static LocalizedText TaskLine { get; private set; }
        public static LocalizedText AcceptPrompt { get; private set; }

        //选项文本
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }
        public static LocalizedText AcceptResponse { get; private set; }
        public static LocalizedText DeclineResponse { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        private const string red = " ";
        private const string alt = " " + " ";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //介绍台词
            IntroLine1 = this.GetLocalization(nameof(IntroLine1), () => "我需要你的协助来完成一项重要的实验");
            IntroLine2 = this.GetLocalization(nameof(IntroLine2), () => "星际通讯一直是我研究的核心领域之一，而你正好可以帮我测试最新的量子纠缠通讯系统");
            IntroLine3 = this.GetLocalization(nameof(IntroLine3), () => "这项技术可以突破光速限制，实现跨越星际的即时通讯");
            IntroLine4 = this.GetLocalization(nameof(IntroLine4), () => "但在实际应用之前，我需要在这个世界建立足够多的量子纠缠节点");
            ShowTowerLine = this.GetLocalization(nameof(ShowTowerLine), () => "这就是我设计的量子信号塔，它的核心采用了纠缠态稳定器和零点能量放大器");
            TechExplainLine1 = this.GetLocalization(nameof(TechExplainLine1), () => "每座信号塔都能与其他节点建立量子纠缠链接，形成一个覆盖全星系的通讯网络");
            TechExplainLine2 = this.GetLocalization(nameof(TechExplainLine2), () => "理论上，这个网络的传输延迟可以达到普朗克时间级别");
            TaskLine = this.GetLocalization(nameof(TaskLine), () => "你的任务是在世界各地部署这些信号塔，建立起完整的纠缠网络");
            AcceptPrompt = this.GetLocalization(nameof(AcceptPrompt), () => "那么，你愿意接受这个委托吗？");

            //选项
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "接受委托");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "稍后再说");
            AcceptResponse = this.GetLocalization(nameof(AcceptResponse), () => "很好我会将信号塔的制作图纸发送给你，记得定期向我汇报部署进度");
            DeclineResponse = this.GetLocalization(nameof(DeclineResponse), () => "我理解当你准备好时随时可以回来找我");
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            DeploySignaltowerRender.RegisterShowEffect();//注册信号塔展示特效
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
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
            //触发信号塔图片展示

            //展示信号塔图片的对话，带展示动画
            Add(DraedonName.Value + red, ShowTowerLine.Value, onStart: DeploySignaltowerRender.ShowTowerImage);

            Add(DraedonName.Value + alt, TechExplainLine1.Value);
            Add(DraedonName.Value, TechExplainLine2.Value);
            Add(DraedonName.Value + red, TaskLine.Value);

            //添加选择界面
            AddWithChoices(DraedonName.Value + red, AcceptPrompt.Value, [
                new Choice(ChoiceAccept.Value, OnAcceptQuest),
                new Choice(ChoiceDecline.Value, OnDeclineQuest)
            ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
        }

        //玩家接受任务
        private void OnAcceptQuest() {
            //完成当前场景
            Complete();
        }

        //玩家拒绝任务
        private void OnDeclineQuest() {
            //完成当前场景
            Complete();
        }
    }
}
