using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 石像鬼虫群飞越后的阿波利娅警示对话场景
    /// <para>
    /// 触发时机：<see cref="ApolliaPlayer"/> 中石像鬼演出结束后自动启动
    /// </para>
    /// </summary>
    internal class GargoyleWarningScenario : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(GargoyleWarningScenario);
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => StarStreamDialogueBox.Instance;

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            Line1 = this.GetLocalization(nameof(Line1), () => "......虫族的侦察群落，虫群大概率在后面");
            Line2 = this.GetLocalization(nameof(Line2), () => "它们肯定发现你了，跟我走，前方有一座废弃的要塞，用那里的防御工事可以抵挡一会儿");
        }

        protected override void OnScenarioStart() {
            StarStreamDialogueBox.Instance?.ShowFullBodyPortrait<ApolliaFullBodyPortrait>();
            StarStreamDialogueBox.Instance?.GetActiveFullBodyPortrait()?.SkipFadeIn();
        }

        protected override void Build() {
            string speaker = FirstMetApollia.Rolename.Value;
            Add(speaker, Line1.Value);
            Add(speaker, Line2.Value);
        }

        protected override void OnScenarioComplete() {
            if (Main.LocalPlayer?.TryGetModPlayer(out ApolliaPlayer ap) == true) {
                ap.StartLeadToFortress();
            }
        }
    }
}
