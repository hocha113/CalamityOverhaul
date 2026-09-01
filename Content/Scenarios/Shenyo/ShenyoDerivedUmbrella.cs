using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Kiame.Gate;
using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using CalamityOverhaul.OtherMods.SubWorld;
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
    /// 门伞初谈「衍生灵异」：持伞玩家在主世界第一次走近鬼域门伞
    /// （<see cref="KiameGateUmbrella"/>）时，沈幽随黑雨现身，
    /// 点破这把伞是从鬼伞源头肢解出的衍生灵异，并留下鬼域的死亡承诺
    /// （死在里面会被重启拉回来，兑现处 <see cref="Kiame.KiameWake"/>）。
    /// 一次性，进度随玩家存档；开口距离比交互距离更远，先听完再够得着伞
    /// </summary>
    internal sealed class ShenyoDerivedUmbrella : StoryScenario, ILocalizedModType
    {
        private const string AskLabel = "ask";
        private const string SourceLabel = "source";

        /// <summary>开口距离（像素）：门伞交互距离 150，先在外圈拦下玩家</summary>
        private const float TriggerDistance = 340f;

        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L1 { get; private set; }
        public static LocalizedText Opt1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }
        public static LocalizedText Opt2 { get; private set; }
        public static LocalizedText L4 { get; private set; }
        public static LocalizedText L5 { get; private set; }
        public static LocalizedText L6 { get; private set; }
        public static LocalizedText L7 { get; private set; }
        public static LocalizedText L8 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "这把伞看起来是衍生灵异");
            Opt1 = this.GetLocalization(nameof(Opt1), () => "衍生灵异？也就是说，它是你的一部分？");
            L2 = this.GetLocalization(nameof(L2), () => "没错，我曾经肢解了自身的灵异，这些被肢解出的碎片，在经过一定岁月后，会重新复苏");
            L3 = this.GetLocalization(nameof(L3), () => "比如这把入侵现实的伞，就是从鬼伞源头衍生出的灵异现象");
            Opt2 = this.GetLocalization(nameof(Opt2), () => "所以，你其实不是鬼伞的源头？");
            L4 = this.GetLocalization(nameof(L4), () => "曾经是");
            L5 = this.GetLocalization(nameof(L5), () => "我本来应该已经死了，但我的意识又在这个世界上的一块灵异碎片中苏醒了");
            L6 = this.GetLocalization(nameof(L6), () => "接触这把衍生出的鬼伞，应该就可以进入它的鬼域");
            L7 = this.GetLocalization(nameof(L7), () => "去不去随你");
            L8 = this.GetLocalization(nameof(L8), () => "如果你死在鬼域里，我会用重启把你拉回来");
        }

        protected override void Build(NarrativeComposer n) {
            n.AllowSkipThrough()
             //黑雨成形期间开口；玩家的反问是唯一选项，成形完才点得动
             .Choice(NarrativeIds.Shenyo, L1.Value, c => c
                 .Voice(Voice[1])
                 .Option(AskLabel, Opt1.Value, NarrativeTarget.Goto(AskLabel)))
             .Label(AskLabel)
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[2],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             //追问同样只有一句可选
             .Choice(NarrativeIds.Shenyo, L3.Value, c => c
                 .Voice(Voice[3])
                 .Option(SourceLabel, Opt2.Value, NarrativeTarget.Goto(SourceLabel)))
             .Label(SourceLabel)
             .Say(NarrativeIds.Shenyo, L4.Value, Voice[4],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Lidded))
             .Say(NarrativeIds.Shenyo, L5.Value, Voice[5],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.CloseEye))
             .Say(NarrativeIds.Shenyo, L6.Value, Voice[6],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.None))
             .Say(NarrativeIds.Shenyo, L7.Value, Voice[7],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L8.Value, Voice[8],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Calm))
             .End();
        }

        //完成判定走播完位：中途掉线重进会从头再播一遍（与初遇同口径）
        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => ShenyoStorySync.PostDerivedUmbrellaIsComplete,
            CanTrigger = (_, player) => CanTriggerDerivedTalk(player),
            OnTriggered = _ => ShenyoStorySync.MarkDerivedUmbrellaMet(),
            OnCompleted = _ => ShenyoStorySync.MarkPostDerivedUmbrellaComplete(),
        };

        private static bool CanTriggerDerivedTalk(Player player) {
            if (Main.dedServ || player == null || !player.Alives()
                || player.whoAmI != Main.myPlayer) {
                return false;
            }
            //门伞是主世界地标：子世界与叠加层雨里都不谈
            if (SubWorldRef.AnyActiveSubWorld() || OniRainWorldState.LocalIn) {
                return false;
            }
            if (!KiameGateSpawn.IsGenerated || !KiameGateUmbrella.LocalPlayerHasKikasa()) {
                return false;
            }
            //Boss 战与各路演出让位（镜像初遇与礼物线的 blocker 组合）
            if (CWRWorld.HasBoss || OniRainWorldTransition.Active
                || OniRainDescentTransition.Active || OniRainExitTransition.Active
                || CutsceneDirector.IsPlaying) {
                return false;
            }
            return player.Center.Distance(KiameGateSpawn.GatePosition) < TriggerDistance;
        }

        protected override void OnStarted() => ShenyoNarrativePortrait.ShowRainAssembly();

        protected override void OnCompleted() => ShenyoNarrativePortrait.Hide();
    }
}
