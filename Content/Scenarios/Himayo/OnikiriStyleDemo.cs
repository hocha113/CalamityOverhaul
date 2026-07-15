using CalamityOverhaul.Content.Narrative;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    /// <summary>
    /// 鬼切叙事皮肤演示场景(调试用):跑一遍对话/换行/换说话人/选择(含禁用项)/奖励弹窗,
    /// 由 <see cref="OnikiriStyleDemoItem"/> 手动触发,可循环回菜单反复看效果
    /// </summary>
    internal sealed class OnikiriStyleDemo : NarrativeScenario, ILocalizedModType
    {
        private const string MenuLabel = "menu";
        private const string RewardLabel = "reward";
        private const string ReplayLabel = "replay";
        private const string FarewellLabel = "farewell";

        public string LocalizationCategory => "ADV";

        public static LocalizedText MayoName { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText ChoicePrompt { get; private set; }
        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }
        public static LocalizedText C3 { get; private set; }
        public static LocalizedText C4 { get; private set; }
        public static LocalizedText SealedHint { get; private set; }
        public static LocalizedText RewardLine { get; private set; }
        public static LocalizedText ReplayLine1 { get; private set; }
        public static LocalizedText ReplayLine2 { get; private set; }
        public static LocalizedText Farewell1 { get; private set; }
        public static LocalizedText Farewell2 { get; private set; }

        public override StyleId DefaultStyle => "Onikiri";

        public override void SetStaticDefaults() {
            MayoName = this.GetLocalization(nameof(MayoName), () => "绯村真夜");
            L1 = this.GetLocalization(nameof(L1), () => "月亮升起来了。绯红的,和我离开那晚一模一样");
            L2 = this.GetLocalization(nameof(L2), () => "(纸垂在夜风里轻轻晃动,某种视线落在你肩上)");
            L3 = this.GetLocalization(nameof(L3), () => "别紧张,刀还在鞘里");
            L4 = this.GetLocalization(nameof(L4), () => "这块面板是新裱的:和纸吃了墨,边框是一笔收出来的,顶上那条绸子……嗯,是我系的。你要是盯着这行字看,应该能看到最新的字迹还带着一点没干透的绯色");
            ChoicePrompt = this.GetLocalization(nameof(ChoicePrompt), () => "那么——想先看哪一样?");
            C1 = this.GetLocalization(nameof(C1), () => "看看奖励弹窗(绘马)");
            C2 = this.GetLocalization(nameof(C2), () => "把刚才的话再演一遍");
            C3 = this.GetLocalization(nameof(C3), () => "解开鬼切的封印");
            C4 = this.GetLocalization(nameof(C4), () => "今晚就到这里");
            SealedHint = this.GetLocalization(nameof(SealedHint), () => "封印中");
            RewardLine = this.GetLocalization(nameof(RewardLine), () => "拿好。普通的刀而已,但挂牌的样子你看到了");
            ReplayLine1 = this.GetLocalization(nameof(ReplayLine1), () => "好,再来一遍。注意看说话人换行时,朱印是怎么重新盖下去的");
            ReplayLine2 = this.GetLocalization(nameof(ReplayLine2), () => "(无名的低语——这一行没有名牌,也没有印)");
            Farewell1 = this.GetLocalization(nameof(Farewell1), () => "夜还长,茶就不留你了");
            Farewell2 = this.GetLocalization(nameof(Farewell2), () => "下次再见时,希望是在正式的故事里");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Mayo, L1.Value)
             .Say(NarrativeIds.System, L2.Value)
             .Say(NarrativeIds.Mayo, L3.Value)
             .Say(NarrativeIds.Mayo, L4.Value)
             .Label(MenuLabel)
             .Choice(NarrativeIds.Mayo, ChoicePrompt.Value, c => c
                 .Option("reward", C1.Value, NarrativeTarget.Goto(RewardLabel))
                 .Option("replay", C2.Value, NarrativeTarget.Goto(ReplayLabel))
                 .Option("sealed", C3.Value, enabled: () => false, disabledHint: SealedHint.Value)
                 .Option("leave", C4.Value, NarrativeTarget.Goto(FarewellLabel)))
             .Label(RewardLabel)
             .Reward(ItemID.Katana)
             .Say(NarrativeIds.Mayo, RewardLine.Value)
             .Goto(MenuLabel)
             .Label(ReplayLabel)
             .Say(NarrativeIds.Mayo, ReplayLine1.Value)
             .Say(NarrativeIds.System, ReplayLine2.Value)
             .Goto(MenuLabel)
             .Label(FarewellLabel)
             .Say(NarrativeIds.Mayo, Farewell1.Value)
             .Say(NarrativeIds.Mayo, Farewell2.Value)
             .End();
        }

        //调试场景:永不自动触发、永不判定为已完成,可反复手动播放
        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => false,
            CanTrigger = (_, _) => false,
        };
    }
}
