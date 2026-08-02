using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.Scenarios.Himayo;
using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 鬼切教程引导队列入口（<see cref="IGuideLead"/> 优先级 5）。<br/>
    /// SHPC 子世界教程 = 0 → 本教程 = 5 → Halibut = 10 → 委托引导 = 20。<br/>
    /// 持有展示权期间：每帧续租 <see cref="ToriiDusk"/>，驱动 <see cref="OnikiriTutorialFlow"/>，注入渲染层。
    /// </summary>
    internal sealed class OnikiriTutorialLead : ModSystem, ILocalizedModType, IGuideLead
    {
        /// <summary>当前教程版本；<see cref="OnikiriGuideData.CompletedVersion"/> 须达到此值才视为完成</summary>
        internal const int TutorialVersion = 3;

        public string LocalizationCategory => "Legend.OnikiriText";

        //====本地化（结构对齐 HalibutHudLead）====
        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudBody { get; private set; }
        public static LocalizedText HudPrompt { get; private set; }
        public static LocalizedText RegisterTitle { get; private set; }
        public static LocalizedText RegisterBody { get; private set; }
        public static LocalizedText RegisterPrompt { get; private set; }
        public static LocalizedText MeiTitle { get; private set; }
        public static LocalizedText MeiBody { get; private set; }
        public static LocalizedText MeiPrompt { get; private set; }
        public static LocalizedText DomainTitle { get; private set; }
        public static LocalizedText DomainBody { get; private set; }
        public static LocalizedText DomainPrompt { get; private set; }
        public static LocalizedText NextBtn { get; private set; }
        public static LocalizedText OpenRegisterBtn { get; private set; }
        public static LocalizedText OpenMeiBtn { get; private set; }
        public static LocalizedText SkipBtn { get; private set; }

        //====单例引用====
        private static OnikiriTutorialLead _instance;

        /// <summary>教程当前持有引导队列展示权</summary>
        internal static bool IsActive => _instance != null && GuideLeadQueue.IsHolder(_instance);

        public override void Load() => _instance = this;
        public override void Unload() => _instance = null;

        //====IGuideLead 注册====

        public override void SetStaticDefaults()
        {
            GuideLeadQueue.Register(this);

            HudTitle = this.GetLocalization(nameof(HudTitle), () => "气力与架势");
            HudBody = this.GetLocalization(nameof(HudBody), () => "手持鬼切时，左下角常驻气力笔触与架势鞘，两者都只显示读数。半架势可双疾走接左键灭世，满架势可用处决键锁敌终结；按住樱流键会先付费疾走再化樱");
            HudPrompt = this.GetLocalization(nameof(HudPrompt), () => "认一下这组读数；HUD 的界面入口只有封印札");

            RegisterTitle = this.GetLocalization(nameof(RegisterTitle), () => "点鬼簿");
            RegisterBody = this.GetLocalization(nameof(RegisterBody), () => "改铭台左上悬着点鬼簿卷轴。移步后，札与传奇界面键会记住点鬼簿；铭鬼名录、驾驭与共鸣都记在这里");
            RegisterPrompt = this.GetLocalization(nameof(RegisterPrompt), () => "点高亮卷轴移步；打开后看一眼名录，再收卷继续");

            MeiTitle = this.GetLocalization(nameof(MeiTitle), () => "改铭台");
            MeiBody = this.GetLocalization(nameof(MeiBody), () => "封印札是 HUD 唯一的界面入口，首次打开进入改铭台。茎铭、樋位、雕位决定刀上赋效");
            MeiPrompt = this.GetLocalization(nameof(MeiPrompt), () => "按 {0} 或点高亮的札打开改铭台");

            DomainTitle = this.GetLocalization(nameof(DomainTitle), () => "鬼域之眼");
            DomainBody = this.GetLocalization(nameof(DomainBody), () => "气力旁那只眼掌管鬼域：展开表世界（泛黄和纸）、翻到里世界（水墨阴间），再可收阖");
            DomainPrompt = this.GetLocalization(nameof(DomainPrompt), () => "左键展/收域，右键或 {0} 翻转表里；展一次也可推进");

            NextBtn = this.GetLocalization(nameof(NextBtn), () => "已知晓");
            OpenRegisterBtn = this.GetLocalization(nameof(OpenRegisterBtn), () => "开点鬼簿");
            OpenMeiBtn = this.GetLocalization(nameof(OpenMeiBtn), () => "开改铭台");
            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "跳过");
        }

        //====IGuideLead 实现====

        int IGuideLead.GuidePriority => 5;
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;

        /// <summary>3 分钟饥饿保底触发时强制跳过教程</summary>
        void IGuideLead.OnGuideAbandoned() => MarkComplete();

        private static bool Reserving
        {
            get
            {
                if (Main.dedServ || Main.gameMenu) return false;
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) return false;
                var guide = p.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
                if (guide.CompletedVersion >= TutorialVersion) return false;
                if (!p.HasItem(OnikiriOverride.ID)) return false;
                return HimayoStorySync.PostFirstMetIsComplete;
            }
        }

        private static bool Ready
        {
            get
            {
                if (!Reserving) return false;
                return !NarrativeTriggerGate.IsBusy && !CutsceneDirector.IsPlaying;
            }
        }

        //====生命周期====

        public override void OnWorldUnload()
        {
            OnikiriTutorialFlow.Reset();
            OnikiriTutorialTargets.Clear();
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.dedServ || Main.gameMenu) return;

            if (!GuideLeadQueue.IsHolder(this))
            {
                OnikiriTutorialFlow.ResetIfHolderLost();
                return;
            }

            //每帧续租黄昏场景与音乐
            ToriiDusk.SetTutorialLease();

            //推进教程步骤状态机
            OnikiriTutorialFlow.Tick(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (!GuideLeadQueue.IsHolder(this)) return;
            if (!OnikiriTutorialFlow.IsRunning) return;
            //插在原版鼠标文本前，盖过 UIHandle（与 HalibutHudLead 同层）
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx < 0) {
                idx = layers.FindIndex(l => l.Name == "Vanilla: Cursor");
            }
            if (idx >= 0)
            {
                layers.Insert(idx, new LegacyGameInterfaceLayer(
                    "CalamityOverhaul: OnikiriTutorial",
                    static () => { OnikiriTutorialRenderer.Draw(); return true; },
                    InterfaceScaleType.UI));
            }
        }

        //====教程完成====

        /// <summary>标记教程完成（写存档版本，释放引导队列占位）</summary>
        internal static void MarkComplete()
        {
            if (Main.dedServ) return;
            Player p = Main.LocalPlayer;
            if (p == null || !p.active) return;
            var guide = p.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            guide.CompletedVersion = TutorialVersion;
            guide.Checkpoint = OnikiriTutorialFlow.Checkpoint_Hud;
        }
    }
}
