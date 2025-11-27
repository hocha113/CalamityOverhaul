using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class SCalAltarScenario : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText L1;
        public static LocalizedText L2;
        public static LocalizedText L3;
        public static int Count;
        void IWorldInfo.OnWorldLoad() {
            Count = -1;
        }
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            L1 = this.GetLocalization(nameof(L1), () => "现在还不是时候，你的前方还有另一个挡路的敌人");
            L2 = this.GetLocalization(nameof(L2), () => "去把你那堆机械玩具拼好，再把他打倒");
            L3 = this.GetLocalization(nameof(L3), () => "……怎么？需要我再说一遍吗？");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalsADV[3]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            string line = L1.Value;
            if (Count == 1) {
                line = L2.Value;
            }
            if (Count == 2) {
                line = L3.Value;
            }
            Add(Rolename1.Value, line);
        }
    }
}
