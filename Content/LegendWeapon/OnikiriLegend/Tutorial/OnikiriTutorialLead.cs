using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.Scenarios.Himayo;
using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using InnoVault.Cinematics;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>鬼切教程引导队列入口</summary>
    internal sealed class OnikiriTutorialLead : ModSystem, ILocalizedModType, IGuideLead
    {
        internal const int TutorialVersion = 5;

        public string LocalizationCategory => "Legend.OnikiriText";

        public static LocalizedText AskTitle { get; private set; }
        public static LocalizedText AskBody { get; private set; }
        public static LocalizedText AskPrompt { get; private set; }
        public static LocalizedText RefreshAskTitle { get; private set; }
        public static LocalizedText RefreshAskBody { get; private set; }
        public static LocalizedText RefreshAskPrompt { get; private set; }
        public static LocalizedText AcceptBtn { get; private set; }
        public static LocalizedText DeclineBtn { get; private set; }
        public static LocalizedText DeclineNotice { get; private set; }
        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudBody { get; private set; }
        public static LocalizedText HudPrompt { get; private set; }
        public static LocalizedText RegisterTitle { get; private set; }
        public static LocalizedText RegisterBody { get; private set; }
        public static LocalizedText RegisterPrompt { get; private set; }
        public static LocalizedText MeiTitle { get; private set; }
        public static LocalizedText MeiBody { get; private set; }
        public static LocalizedText MeiPrompt { get; private set; }
        public static LocalizedText CodexTitle { get; private set; }
        public static LocalizedText CodexBody { get; private set; }
        public static LocalizedText CodexPrompt { get; private set; }
        public static LocalizedText DomainTitle { get; private set; }
        public static LocalizedText DomainBody { get; private set; }
        public static LocalizedText DomainPrompt { get; private set; }
        public static LocalizedText PrepareTitle { get; private set; }
        public static LocalizedText PrepareBody { get; private set; }
        public static LocalizedText PreparePrompt { get; private set; }
        public static LocalizedText OpenDomainTitle { get; private set; }
        public static LocalizedText OpenDomainBody { get; private set; }
        public static LocalizedText OpenDomainPrompt { get; private set; }
        public static LocalizedText FlipDomainTitle { get; private set; }
        public static LocalizedText FlipDomainBody { get; private set; }
        public static LocalizedText FlipDomainPrompt { get; private set; }
        public static LocalizedText DismemberTitle { get; private set; }
        public static LocalizedText DismemberBody { get; private set; }
        public static LocalizedText DismemberPrompt { get; private set; }
        public static LocalizedText BacklashTitle { get; private set; }
        public static LocalizedText BacklashBody { get; private set; }
        public static LocalizedText BacklashPrompt { get; private set; }
        public static LocalizedText CloseDomainTitle { get; private set; }
        public static LocalizedText CloseDomainBody { get; private set; }
        public static LocalizedText CloseDomainPrompt { get; private set; }
        public static LocalizedText DomainUnboundHint { get; private set; }
        public static LocalizedText FlipUnboundHint { get; private set; }
        public static LocalizedText WaitingFeedback { get; private set; }
        public static LocalizedText BusyFeedback { get; private set; }
        public static LocalizedText RetryFeedback { get; private set; }
        public static LocalizedText NeedBladeHold { get; private set; }
        public static LocalizedText TargetStalledHint { get; private set; }
        public static LocalizedText AssistBtn { get; private set; }
        public static LocalizedText RetryBtn { get; private set; }
        public static LocalizedText NextBtn { get; private set; }
        public static LocalizedText OpenRegisterBtn { get; private set; }
        public static LocalizedText OpenMeiBtn { get; private set; }
        public static LocalizedText OpenCodexBtn { get; private set; }
        public static LocalizedText SkipBtn { get; private set; }
        public static LocalizedText SkipStepBtn { get; private set; }
        public static LocalizedText AbortBtn { get; private set; }

        private static OnikiriTutorialLead instance;

        internal static bool IsActive => instance != null && HasDisplayLease;

        private static bool HasDisplayLease {
            get {
                if (instance == null || Main.dedServ || Main.gameMenu) {
                    return false;
                }
                OnikiriTutorialPlayer tutorial = Main.LocalPlayer?.GetModPlayer<OnikiriTutorialPlayer>();
                return tutorial?.Forced == true || GuideLeadQueue.IsHolder(instance);
            }
        }

        public override void Load() => instance = this;
        public override void Unload() {
            OnikiriTutorialRenderer.Reset();
            instance = null;
        }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);

            AskTitle = this.GetLocalization(nameof(AskTitle), () => "要不要先受一遍教习");
            AskBody = this.GetLocalization(nameof(AskBody), () => "鬼切的气力、鬼域与肢解都有讲究");
            AskPrompt = this.GetLocalization(nameof(AskPrompt), () => "现在受教，或收下稽古符改日再来");
            RefreshAskTitle = this.GetLocalization(nameof(RefreshAskTitle), () => "补一段新的");
            RefreshAskBody = this.GetLocalization(nameof(RefreshAskBody),
                () => "这一版多了两段讲解:改铭台旁那本铭谱怎么查,以及鬼域之眼在动手前该认的东西");
            RefreshAskPrompt = this.GetLocalization(nameof(RefreshAskPrompt),
                () => "现在补讲;或收下稽古符,改日自己开讲");
            AcceptBtn = this.GetLocalization(nameof(AcceptBtn), () => "受教");
            DeclineBtn = this.GetLocalization(nameof(DeclineBtn), () => "不必了");
            DeclineNotice = this.GetLocalization(nameof(DeclineNotice), () => "稽古符已留给你，使用它可随时开讲");
            HudTitle = this.GetLocalization(nameof(HudTitle), () => "气力与架势");
            HudBody = this.GetLocalization(nameof(HudBody), () => "手持鬼切时，左下角常驻气力笔触与架势鞘");
            HudPrompt = this.GetLocalization(nameof(HudPrompt), () => "认一下这组读数");
            RegisterTitle = this.GetLocalization(nameof(RegisterTitle), () => "点鬼簿");
            RegisterBody = this.GetLocalization(nameof(RegisterBody), () => "改铭台顶梁左侧钉着「点鬼簿」驿牌,牌下悬卷");
            RegisterPrompt = this.GetLocalization(nameof(RegisterPrompt), () => "点高亮驿牌或卷轴移步后再收卷继续");
            MeiTitle = this.GetLocalization(nameof(MeiTitle), () => "改铭台");
            MeiBody = this.GetLocalization(nameof(MeiBody), () => "封印札是 HUD 的界面入口");
            MeiPrompt = this.GetLocalization(nameof(MeiPrompt), () => "按 {0} 或点封印札打开改铭台");
            CodexTitle = this.GetLocalization(nameof(CodexTitle), () => "铭谱");
            CodexBody = this.GetLocalization(nameof(CodexBody),
                () => "台账板下压着一本线装册子,里面记着每一枚铭的赋效、代价与来路");
            CodexPrompt = this.GetLocalization(nameof(CodexPrompt), () => "点高亮的册子展读,合卷后继续");
            DomainTitle = this.GetLocalization(nameof(DomainTitle), () => "鬼域之眼");
            DomainBody = this.GetLocalization(nameof(DomainBody), () => "鬼眼掌管领域的展收与表里翻转");
            DomainPrompt = this.GetLocalization(nameof(DomainPrompt), () => "右键或 {0} 翻转表里");

            PrepareTitle = this.GetLocalization(nameof(PrepareTitle), () => "实操准备");
            PrepareBody = this.GetLocalization(nameof(PrepareBody), () => "先让鬼域与界面恢复到可演练状态");
            PreparePrompt = this.GetLocalization(nameof(PreparePrompt), () => "手持鬼切，等待状态安定");
            OpenDomainTitle = this.GetLocalization(nameof(OpenDomainTitle), () => "展开表世界");
            OpenDomainBody = this.GetLocalization(nameof(OpenDomainBody), () => "按领域键展开浅层表世界");
            OpenDomainPrompt = this.GetLocalization(nameof(OpenDomainPrompt), () => "按 {0} 亲手展开领域");
            FlipDomainTitle = this.GetLocalization(nameof(FlipDomainTitle), () => "翻入里世界");
            FlipDomainBody = this.GetLocalization(nameof(FlipDomainBody), () => "在表世界按翻转键进入深层里世界");
            FlipDomainPrompt = this.GetLocalization(nameof(FlipDomainPrompt), () => "按 {0} 翻转领域");
            DismemberTitle = this.GetLocalization(nameof(DismemberTitle), () => "肢解演练");
            DismemberBody = this.GetLocalization(nameof(DismemberBody), () => "瞄准高亮的圣诞坦克并左键肢解真身");
            DismemberPrompt = this.GetLocalization(nameof(DismemberPrompt), () => "松开再新按一次左键");
            BacklashTitle = this.GetLocalization(nameof(BacklashTitle), () => "承受反噬");
            BacklashBody = this.GetLocalization(nameof(BacklashBody), () => "肢解会造成真实的四分之一最大生命反噬");
            BacklashPrompt = this.GetLocalization(nameof(BacklashPrompt), () => "等待演出与恢复结束");
            CloseDomainTitle = this.GetLocalization(nameof(CloseDomainTitle), () => "从鬼眼收域");
            CloseDomainBody = this.GetLocalization(nameof(CloseDomainBody), () => "最后左键点击鬼眼收阖领域");
            CloseDomainPrompt = this.GetLocalization(nameof(CloseDomainPrompt), () => "左键点击高亮鬼眼");
            DomainUnboundHint = this.GetLocalization(nameof(DomainUnboundHint), () => "领域键未绑定，本次可按 Q");
            FlipUnboundHint = this.GetLocalization(nameof(FlipUnboundHint), () => "翻转键未绑定，本次可按 Mouse3");
            WaitingFeedback = this.GetLocalization(nameof(WaitingFeedback), () => "正在等待结果落定");
            BusyFeedback = this.GetLocalization(nameof(BusyFeedback), () => "鬼域仍在变相，请稍候");
            RetryFeedback = this.GetLocalization(nameof(RetryFeedback), () => "这次没有生效，请重试");
            NeedBladeHold = this.GetLocalization(nameof(NeedBladeHold), () => "行囊里没有鬼切，先把它拿回来再继续");
            TargetStalledHint = this.GetLocalization(nameof(TargetStalledHint),
                () => "演练靶还没落位。附近生灵太挤时它排不上号，换个开阔处站定");
            AssistBtn = this.GetLocalization(nameof(AssistBtn), () => "替我演示");
            RetryBtn = this.GetLocalization(nameof(RetryBtn), () => "重试");
            NextBtn = this.GetLocalization(nameof(NextBtn), () => "已知晓");
            OpenRegisterBtn = this.GetLocalization(nameof(OpenRegisterBtn), () => "开点鬼簿");
            OpenMeiBtn = this.GetLocalization(nameof(OpenMeiBtn), () => "开改铭台");
            OpenCodexBtn = this.GetLocalization(nameof(OpenCodexBtn), () => "展读铭谱");
            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "跳过");
            SkipStepBtn = this.GetLocalization(nameof(SkipStepBtn), () => "跳过本步");
            AbortBtn = this.GetLocalization(nameof(AbortBtn), () => "收起教习");
        }

        int IGuideLead.GuidePriority => 5;
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        void IGuideLead.OnGuideAbandoned() => OnikiriTutorialFlow.DeferAfterQueueAbandon();

        private static bool Reserving {
            get {
                if (Main.dedServ || Main.gameMenu) return false;
                Player player = Main.LocalPlayer;
                if (player?.active != true) return false;
                OnikiriTutorialPlayer tutorial = player.GetModPlayer<OnikiriTutorialPlayer>();
                if (tutorial.Forced) return true;
                if (tutorial.ReservationDeferred) return false;
                OnikiriGuideData guide = player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
                if (guide.Declined || guide.CompletedVersion >= TutorialVersion) return false;
                if (!player.HasItem(OnikiriOverride.ID)) return false;
                return HimayoStorySync.PostFirstMetIsComplete;
            }
        }

        private static bool Ready
            => Reserving && !NarrativeTriggerGate.IsBusy && !CutsceneDirector.IsPlaying;

        public override void OnWorldUnload() {
            OnikiriTutorialFlow.Reset();
            OnikiriTutorialTargets.Clear();
            OnikiriTutorialRenderer.Reset();
            OnikiriTutorialEvents.ClearAll();
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) return;
            if (!HasDisplayLease) {
                OnikiriTutorialFlow.ResetIfHolderLost();
                OnikiriTutorialRenderer.Reset();
                return;
            }

            OnikiriTutorialFlow.Tick(gameTime);
            ToriiDusk.SetTutorialLease();
            if (OnikiriTutorialFlow.CurrentStep >= OnikiriTutorialFlow.Step_Prepare) {
                ToriiDusk.SuppressTutorialVisuals();
            }
            OnikiriTutorialRenderer.UpdateInput();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!HasDisplayLease || !OnikiriTutorialFlow.IsRunning) return;
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index < 0) {
                index = layers.FindIndex(layer => layer.Name == "Vanilla: Cursor");
            }
            if (index >= 0) {
                layers.Insert(index, new LegacyGameInterfaceLayer(
                    "CalamityOverhaul: OnikiriTutorial",
                    static () => { OnikiriTutorialRenderer.Draw(); return true; },
                    InterfaceScaleType.UI));
            }
        }

        internal static void MarkComplete() {
            if (Main.dedServ) return;
            Player player = Main.LocalPlayer;
            if (player?.active != true) return;
            OnikiriGuideData guide = player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            guide.CompletedVersion = TutorialVersion;
            guide.Checkpoint = OnikiriTutorialFlow.Checkpoint_ExplainDone;
            guide.PracticeCheckpoint = (int)OnikiriPracticeCheckpoint.Closed;
            guide.AskAnswered = true;
            guide.Declined = false;
            guide.RefresherAskedVersion = TutorialVersion;
            player.GetModPlayer<OnikiriTutorialPlayer>().ClearForced();
        }

        /// <summary>对话或过场占用中,符不该把教学卡盖上去</summary>
        internal static bool RuneStartBlocked
            => NarrativeTriggerGate.IsBusy || CutsceneDirector.IsPlaying;

        /// <summary>
        /// 稽古符启动:整段会话只靠 <see cref="OnikiriTutorialPlayer.Forced"/> 撑着,
        /// 不动存档进度,中途退出不会把自动教程重新打开
        /// </summary>
        internal static bool StartFromRune(Player player) {
            if (Main.dedServ || player?.whoAmI != Main.myPlayer) return false;
            OnikiriTutorialPlayer tutorial = player.GetModPlayer<OnikiriTutorialPlayer>();
            if (tutorial.IsRunning) return false;

            player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>().AskAnswered = true;
            tutorial.ForceStartFull();
            return true;
        }

        /// <summary>
        /// 稽古符收起进行中的教习。同时置 <see cref="OnikiriGuideData.Declined"/>,
        /// 否则排队会在下一帧把自动教程原样接回来
        /// </summary>
        internal static bool StopFromRune(Player player) {
            if (Main.dedServ || player?.whoAmI != Main.myPlayer) return false;
            OnikiriTutorialPlayer tutorial = player.GetModPlayer<OnikiriTutorialPlayer>();
            if (!tutorial.IsRunning) return false;

            OnikiriGuideData guide = player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            guide.AskAnswered = true;
            guide.Declined = true;
            tutorial.ResetAllRuntime();
            return true;
        }

        internal static void DebugStartPractice(Player player) {
            if (Main.dedServ || player?.whoAmI != Main.myPlayer) return;
            OnikiriTutorialPlayer tutorial = player.GetModPlayer<OnikiriTutorialPlayer>();
            if (tutorial.Forced && tutorial.IsRunning) return;
            if (!player.HasItem(OnikiriOverride.ID)) {
                player.QuickSpawnItem(player.GetSource_Misc("CWR_OnikiriTutorialDebug"), OnikiriOverride.ID);
            }
            OnikiriGuideData guide = player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            guide.CompletedVersion = TutorialVersion - 1;
            guide.Checkpoint = OnikiriTutorialFlow.Checkpoint_ExplainDone;
            guide.PracticeCheckpoint = (int)OnikiriPracticeCheckpoint.None;
            tutorial.ForceStartPractice();
        }

        internal static void DebugReset(Player player) {
            if (Main.dedServ || player?.whoAmI != Main.myPlayer) return;
            OnikiriGuideData guide = player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            guide.CompletedVersion = 0;
            guide.Checkpoint = 0;
            guide.PracticeCheckpoint = 0;
            guide.AskAnswered = false;
            guide.Declined = false;
            guide.RefresherAskedVersion = 0;
            player.GetModPlayer<OnikiriTutorialPlayer>().ResetAllRuntime();
            if (OniDomains.OniDomain.GetPhase(player) != OniDomains.OniDomainPhase.Closed) {
                OniDomains.OniDomain.Close(player);
            }
        }
    }
}
