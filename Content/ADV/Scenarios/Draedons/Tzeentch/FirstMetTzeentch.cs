using System;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch
{
    internal class FirstMetTzeentch : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        private const string Rolename1 = "?????????????????????";
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => TzeentchDialogueBox.Instance;
        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1, ADVAsset.Tzeentch);
            DialogueBoxBase.SetPortraitStyle(Rolename1, silhouette: true);

            //添加对话（使用本地化文本）
            Add(Rolename1, "嘻嘻嘻...我看到有趣的变化，无穷的变化！");
        }
    }
}
