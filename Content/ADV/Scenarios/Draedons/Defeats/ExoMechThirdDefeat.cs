using CalamityOverhaul.Content.ADV.DialogueBoxs;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Defeats
{
    /// <summary>
    /// 嘉登第三次战败对话场景
    /// </summary>
    internal class ExoMechThirdDefeat : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public override bool CanRepeat => false;

        //角色名称
        public static LocalizedText DraedonName { get; private set; }

        //第三次战败对话
        public static LocalizedText ThirdDefeatLine1 { get; private set; }
        public static LocalizedText ThirdDefeatLine2 { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() { }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //第三次战败对话：表现出对玩家一贯性的肯定
            ThirdDefeatLine1 = this.GetLocalization(nameof(ThirdDefeatLine1), () => "稳定的表现，这正是我所追求的完美的一部分");
            ThirdDefeatLine2 = this.GetLocalization(nameof(ThirdDefeatLine2), () => "你已经证明了自己的价值不是偶然");
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2RedADV, silhouette: false);

            //第三次战败的简短对话
            Add(DraedonName.Value, ThirdDefeatLine1.Value);
            Add(DraedonName.Value, ThirdDefeatLine2.Value);
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.ExoMechThirdDefeat = true;
            }
        }
    }
}
