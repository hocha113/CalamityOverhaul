using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Defeats
{
    /// <summary>
    /// 嘉登第二次战败对话场景
    /// </summary>
    internal class ExoMechSecondDefeat : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(ExoMechSecondDefeat);
        public override bool CanRepeat => false;

        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //第二次战败对话
        public static LocalizedText SecondDefeatLine1 { get; private set; }
        public static LocalizedText SecondDefeatLine2 { get; private set; }
        public static LocalizedText SecondDefeatLine3 { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() { }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //第二次战败对话：表现出对玩家学习能力的认可
            SecondDefeatLine1 = this.GetLocalization(nameof(SecondDefeatLine1), () => "有趣，你的适应速度超出了我的预期");
            SecondDefeatLine2 = this.GetLocalization(nameof(SecondDefeatLine2), () => "数据显示你在上次战斗后已经进行了针对性的改进");
            SecondDefeatLine3 = this.GetLocalization(nameof(SecondDefeatLine3), () => "看来我需要重新评估你的学习曲线了");
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2RedADV, silhouette: false);

            //第二次战败的简短对话
            Add(DraedonName.Value, SecondDefeatLine1.Value);
            Add(DraedonName.Value, SecondDefeatLine2.Value);
            Add(DraedonName.Value, SecondDefeatLine3.Value);
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.ExoMechSecondDefeat = true;
            }
        }
    }
}
