using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using InnoVault.Cinematics;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 沈幽初见：鬼雨叙事皮肤，立绘走黑雨汇聚入场（<see cref="ShenyoPortraitRainRenderer"/>）。<br/>
    /// 自动触发：本地玩家抵达鬼雨深层（深度≥2，夺伞下潜或被鬼奴杀死拖入皆可），
    /// 落定静默拍走完即开口；入场方式决定选项可选性
    /// （<see cref="ShenyoStorySync.ArrivedByDeath"/>）。<br/>
    /// 对话落幕后由 <see cref="OniRainWorldSystem"/> 接管送出与交付
    /// （<see cref="OniRainExitTransition"/>）。
    /// </summary>
    internal sealed class FirstMetShenyo : StoryScenario, ILocalizedModType
    {
        private const string DeadLabel = "dead";
        private const string RainLabel = "rain";
        private const string OutroLabel = "outro";

        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText ShenyoName { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText OptDead { get; private set; }
        public static LocalizedText OptRain { get; private set; }
        public static LocalizedText OptDeadLocked { get; private set; }
        public static LocalizedText OptRainLocked { get; private set; }
        public static LocalizedText A1 { get; private set; }
        public static LocalizedText A2 { get; private set; }
        public static LocalizedText B1 { get; private set; }
        public static LocalizedText B2 { get; private set; }
        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            ShenyoName = this.GetLocalization(nameof(ShenyoName), () => "沈幽");
            L1 = this.GetLocalization(nameof(L1), () => "这地方很久没看到有活人来了");
            L2 = this.GetLocalization(nameof(L2), () => "你是怎么到这里来的？");
            OptDead = this.GetLocalization(nameof(OptDead), () => "死过一次，醒了就在这了");
            OptRain = this.GetLocalization(nameof(OptRain), () => "抢了把伞冲进来的");
            OptDeadLocked = this.GetLocalization(nameof(OptDeadLocked), () => "（你还活着，这话说不出口）");
            OptRainLocked = this.GetLocalization(nameof(OptRainLocked), () => "（你两手空空，没伞可言）");
            A1 = this.GetLocalization(nameof(A1), () => "……死而复生？");
            A2 = this.GetLocalization(nameof(A2), () => "稀罕事，不过看你的反应，倒像是不在意");
            B1 = this.GetLocalization(nameof(B1), () => "那你运气真不错");
            B2 = this.GetLocalization(nameof(B2), () => "现在想想该怎么出去吧");
            C1 = this.GetLocalization(nameof(C1), () => "雨会替你把路撑开，回去吧");
            C2 = this.GetLocalization(nameof(C2), () => "那把伞，往后就是你的了");
        }

        protected override void Build(NarrativeComposer n) {
            n.AllowSkipThrough()
             //黑雨成形期间闭目开口，睁眼（眯眼打量）落在提问那一拍
             .Say(NarrativeIds.Shenyo, L1.Value, Voice[1],
                    onExit: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             //入场方式决定哪句是真话：说不出口的那句压灰
             .Choice(NarrativeIds.Shenyo, L2.Value, c => c
                 .Voice(Voice[2])
                 .Option(DeadLabel, OptDead.Value, NarrativeTarget.Goto(DeadLabel),
                     enabled: () => ShenyoStorySync.ArrivedByDeath,
                     disabledHint: OptDeadLocked.Value)
                 .Option(RainLabel, OptRain.Value, NarrativeTarget.Goto(RainLabel),
                     enabled: () => !ShenyoStorySync.ArrivedByDeath,
                     disabledHint: OptRainLocked.Value))
             .Label(DeadLabel)
             .Say(NarrativeIds.Shenyo, A1.Value, Voice[3],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Lidded))
             .Say(NarrativeIds.Shenyo, A2.Value, Voice[4],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Goto(OutroLabel)
             .Label(RainLabel)
             .Say(NarrativeIds.Shenyo, B1.Value, Voice[5],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Say(NarrativeIds.Shenyo, B2.Value, Voice[6],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             //共同收尾：送客与赠伞，落幕后送出演出接管
             .Label(OutroLabel)
             .Say(NarrativeIds.Shenyo, C1.Value, Voice[7],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Say(NarrativeIds.Shenyo, C2.Value, Voice[8],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Lidded))
             .End();
        }

        //抵达深层（无论夺伞还是被拖入）且演出全收、静默拍走完才开口；
        //完成判定走 PostFirstMetIsComplete：中途掉线重进会从头再播一遍
        //切歌不在本脚本：深潜/深层起时 OniRainThemeClaim 认领 Rains，提示框跟认领首胜
        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => ShenyoStorySync.PostFirstMetIsComplete,
            CanTrigger = (_, player) => CanTriggerFirstMet(player),
            OnTriggered = _ => ShenyoStorySync.MarkFirstMet(),
            //对话真正落幕才记「播完」：送出与交付以此为门禁
            OnCompleted = _ => ShenyoStorySync.MarkPostFirstMetComplete(),
        };

        private static bool CanTriggerFirstMet(Player player) {
            if (Main.dedServ || player == null || !player.Alives()
                || player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (OniRainWorldState.LocalDepth < 2) {
                return false;
            }
            if (OniRainWorldTransition.Active || OniRainDescentTransition.Active
                || OniRainExitTransition.Active || CutsceneDirector.IsPlaying) {
                return false;
            }
            return player.GetModPlayer<OniRainWorldPlayer>().DeepArrivalCalm <= 0;
        }

        protected override void OnStarted() => ShenyoNarrativePortrait.ShowRainAssembly();

        protected override void OnCompleted() => ShenyoNarrativePortrait.Hide();
    }
}
