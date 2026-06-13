using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 第一座信号塔搭建后的场景
    /// </summary>
    internal class FirstTowerBuiltScenario : ADVScenarioBase, ILocalizedModType
    {
        //角色名称本地化
        public static LocalizedText DraedonName { get; private set; }

        //对话台词
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        private const string red = " ";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            Line1 = this.GetLocalization(nameof(Line1), () => "新的量子纠缠节点运行平稳，信号稳定");
            Line2 = this.GetLocalization(nameof(Line2), () => "很好，这是网络的第一步。继续部署剩余的信号塔");
            Line3 = this.GetLocalization(nameof(Line3), () => "当节点数量达到标准时，我将能够对更大范围进行精确观测");
            Line4 = this.GetLocalization(nameof(Line4), () => "继续保持你的效率");
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2RedADV, silhouette: false);

            //构建对话流程
            Add(DraedonName.Value + red, Line1.Value);
            Add(DraedonName.Value, Line2.Value);
            Add(DraedonName.Value, Line3.Value);
            Add(DraedonName.Value, Line4.Value);
        }
    }
}
