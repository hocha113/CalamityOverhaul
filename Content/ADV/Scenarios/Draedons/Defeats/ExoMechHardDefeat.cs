using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Defeats
{
    /// <summary>
    /// 嘉登艰难战败对话场景
    /// </summary>
    internal class ExoMechHardDefeat : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(ExoMechHardDefeat);
        public override bool CanRepeat => true;

        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //艰难战败对话
        public static LocalizedText HardDefeatLine1 { get; private set; }
        public static LocalizedText HardDefeatLine2 { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() { }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //艰难战败对话：表现出对接近极限的认可
            HardDefeatLine1 = this.GetLocalization(nameof(HardDefeatLine1), () => "极限状态下的决策，这才是我想看到的数据");
            HardDefeatLine2 = this.GetLocalization(nameof(HardDefeatLine2), () => "在压力之下你仍能保持理性，令人满意");
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2RedADV, silhouette: false);

            //艰难战败的对话
            Add(DraedonName.Value, HardDefeatLine1.Value);
            Add(DraedonName.Value, HardDefeatLine2.Value);
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
