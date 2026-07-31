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
        internal const int TutorialVersion = 1;

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
        public static LocalizedText ComboTitle { get; private set; }
        public static LocalizedText ComboBody { get; private set; }
        public static LocalizedText ComboPrompt { get; private set; }
        public static LocalizedText DashTitle { get; private set; }
        public static LocalizedText DashBody { get; private set; }
        public static LocalizedText DashPrompt { get; private set; }
        public static LocalizedText ZanshinTitle { get; private set; }
        public static LocalizedText ZanshinBody { get; private set; }
        public static LocalizedText ZanshinPrompt { get; private set; }
        public static LocalizedText SakuraTitle { get; private set; }
        public static LocalizedText SakuraBody { get; private set; }
        public static LocalizedText SakuraPrompt { get; private set; }
        public static LocalizedText AnnihilateTitle { get; private set; }
        public static LocalizedText AnnihilateBody { get; private set; }
        public static LocalizedText AnnihilatePrompt { get; private set; }
        public static LocalizedText FinaleTitle { get; private set; }
        public static LocalizedText FinaleBody { get; private set; }
        public static LocalizedText FinalePrompt { get; private set; }
        public static LocalizedText DismemberTitle { get; private set; }
        public static LocalizedText DismemberBody { get; private set; }
        public static LocalizedText DismemberPrompt { get; private set; }
        public static LocalizedText CloseTitle { get; private set; }
        public static LocalizedText CloseBody { get; private set; }
        public static LocalizedText ClosePrompt { get; private set; }
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
            HudBody = this.GetLocalization(nameof(HudBody), () => "手持鬼切时，左下角常驻气力笔触与架势鞘。气力供疾走，架势积满可衔出处决");
            HudPrompt = this.GetLocalization(nameof(HudPrompt), () => "认一下这组读数；按 {0} 或点札可开改铭台");

            RegisterTitle = this.GetLocalization(nameof(RegisterTitle), () => "点鬼簿");
            RegisterBody = this.GetLocalization(nameof(RegisterBody), () => "点击架势鞘，可展开点鬼簿——铭鬼名录、驾驭与共鸣都记在这里");
            RegisterPrompt = this.GetLocalization(nameof(RegisterPrompt), () => "点高亮的鞘，或按助手钮打开");

            MeiTitle = this.GetLocalization(nameof(MeiTitle), () => "改铭台");
            MeiBody = this.GetLocalization(nameof(MeiBody), () => "点封印札，或从点鬼簿移步改铭台。茎铭、樋位、雕位决定刀上赋效");
            MeiPrompt = this.GetLocalization(nameof(MeiPrompt), () => "按 {0} 或点札打开改铭台，看一眼铭位即可");

            ComboTitle = this.GetLocalization(nameof(ComboTitle), () => "绯红五拍");
            ComboBody = this.GetLocalization(nameof(ComboBody), () => "按住左键驱动绯红裂空斩。五拍连段命中练习鬼影，熟悉出刀节奏");
            ComboPrompt = this.GetLocalization(nameof(ComboPrompt), () => "对练习鬼影打满五拍");

            DashTitle = this.GetLocalization(nameof(DashTitle), () => "神威疾走");
            DashBody = this.GetLocalization(nameof(DashBody), () => "消耗气力施展疾走，穿身扫掠后墨痕纳刀。命中才会进入纳刀结算");
            DashPrompt = this.GetLocalization(nameof(DashPrompt), () => "按 {0} 对鬼影疾走一次");

            ZanshinTitle = this.GetLocalization(nameof(ZanshinTitle), () => "残心");
            ZanshinBody = this.GetLocalization(nameof(ZanshinBody), () => "樱流化身交还操控后有短暂残心窗。在窗内补一刀命中，可回收资源");
            ZanshinPrompt = this.GetLocalization(nameof(ZanshinPrompt), () => "残心窗内命中练习鬼影");

            SakuraTitle = this.GetLocalization(nameof(SakuraTitle), () => "樱流化身");
            SakuraBody = this.GetLocalization(nameof(SakuraBody), () => "展开鬼域表世界后可放樱流。化身起飞，随后操控交还——那是残心的前奏");
            SakuraPrompt = this.GetLocalization(nameof(SakuraPrompt), () => "展域并释放樱流，等操控交还");

            AnnihilateTitle = this.GetLocalization(nameof(AnnihilateTitle), () => "灭世一闪");
            AnnihilateBody = this.GetLocalization(nameof(AnnihilateBody), () => "架势过半时，双疾走后接左键可放灭世一闪。资源已替你备好");
            AnnihilatePrompt = this.GetLocalization(nameof(AnnihilatePrompt), () => "完成双疾走衔接触发灭世一闪");

            FinaleTitle = this.GetLocalization(nameof(FinaleTitle), () => "终结乱舞");
            FinaleBody = this.GetLocalization(nameof(FinaleBody), () => "架势圆满时，双疾走穿中目标释放终结乱舞。势已替你灌满");
            FinalePrompt = this.GetLocalization(nameof(FinalePrompt), () => "双疾走穿身，放出终结乱舞");

            DismemberTitle = this.GetLocalization(nameof(DismemberTitle), () => "里世界肢解");
            DismemberBody = this.GetLocalization(nameof(DismemberBody), () => "翻转至里世界后可施展肢解一刀。面影会自动快门，对准练习鬼影落刀");
            DismemberPrompt = this.GetLocalization(nameof(DismemberPrompt), () => "翻到里世界，肢解命中鬼影");

            CloseTitle = this.GetLocalization(nameof(CloseTitle), () => "收域");
            CloseBody = this.GetLocalization(nameof(CloseBody), () => "练习结束。收阖鬼域，夜色归位——刀仍在鞘，名仍在簿");
            ClosePrompt = this.GetLocalization(nameof(ClosePrompt), () => "按 {0} 或点鬼域之眼收域");

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
            OnikiriTutorialWraith.ClearServerState();
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
            guide.Checkpoint = 3;
        }
    }
}