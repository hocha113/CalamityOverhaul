using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch
{
    internal class FirstMetTzeentch : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        private const string Rolename1 = "?????????????????????";
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => TzeentchDialogueBox.Instance;
        public static LocalizedText L1;
        public static LocalizedText L2;
        public static LocalizedText L3;
        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "嘻嘻嘻...有趣的变化正在酝酿...");
            L2 = this.GetLocalization(nameof(L2), () => "我亲爱的朋友，很高兴认识你！");
            L3 = this.GetLocalization(nameof(L3), () => "你可以称我为魔术师...什么都行...");
        }
        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1, ADVAsset.Tzeentch);
            DialogueBoxBase.SetPortraitStyle(Rolename1, silhouette: true);

            //添加对话
            Add(Rolename1, L1.Value);
            Add(Rolename1, L2.Value);
            Add(Rolename1, L3.Value);
        }
        protected override void OnScenarioStart() {
            TzeentchEffect.IsActive = true;
            TzeentchEffect.Send();
        }
        protected override void OnScenarioComplete() {
            TzeentchEffect.IsActive = false;
            TzeentchEffect.Send();
        }
    }
}
