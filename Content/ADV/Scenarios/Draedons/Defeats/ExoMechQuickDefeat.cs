using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Defeats
{
    /// <summary>
    /// 嘉登快速战败对话场景（玩家用很短时间击败机甲）
    /// </summary>
    internal class ExoMechQuickDefeat : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public override bool CanRepeat => true;

        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //快速战败对话
        public static LocalizedText QuickDefeatLine1 { get; private set; }
        public static LocalizedText QuickDefeatLine2 { get; private set; }
        public static LocalizedText QuickDefeatLine3 { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() { }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //快速战败对话：表现出对玩家压倒性实力的惊讶
            QuickDefeatLine1 = this.GetLocalization(nameof(QuickDefeatLine1), () => "……这个时间远低于我的预测模型");
            QuickDefeatLine2 = this.GetLocalization(nameof(QuickDefeatLine2), () => "看来我低估了你当前的战斗效率");
            QuickDefeatLine3 = this.GetLocalization(nameof(QuickDefeatLine3), () => "或许是时候考虑更激进的设计方案了");
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2RedADV, silhouette: false);

            //快速战败的对话
            Add(DraedonName.Value, QuickDefeatLine1.Value);
            Add(DraedonName.Value, QuickDefeatLine2.Value);
            Add(DraedonName.Value, QuickDefeatLine3.Value);
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
        }
    }
}
