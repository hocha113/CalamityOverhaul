using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.Panorama;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.ServantWheel;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.QuestLogs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 鬼伞七步引导：首次持伞后串起 开域 → 沉溺 → 湖心景 → 转盘号令 → 鬼雨异化 → 雨中重启 → 鬼梦。
    /// 卡片底板走 KikasaScene.fx 的 TechCard 湿纸技法（入口已迁 KikasaPanoramaRenderer）。
    /// 防呆四件：检查点续讲（存 <see cref="KikasaGuideData.StepCheckpoint"/>，中断不从头来）、
    /// 场面失守退步（湖收了退回开域，翻回血湖退回异化）、死路与条件读数（键未绑定/册空/水未满/重启回卷）、
    /// 帮做分流（世界步卡住出「替我演示」，湖心景常驻「帮我打开」，沉溺动玩家物品不代做）。
    /// 收起写 <see cref="KikasaGuideData.Declined"/>，湖心景页脚「?」经 <see cref="RestartFromHelp"/> 重开。
    /// 经 <see cref="GuideLeadQueue"/> 排队，晚于比目鱼(10)、早于义体(15)
    /// </summary>
    internal class KikasaHudLead : ModSystem, ILocalizedModType, IGuideLead
    {
        public string LocalizationCategory => "Legend.KikasaText";

        /// <summary>教程版本。步骤改版时 +1，走完过旧版的老玩家会从检查点补讲</summary>
        private const int TutorialVersion = 1;

        private enum Phase { Inactive, Domain, Sink, Panorama, Wheel, Rain, Restart, Dream, Complete }

        //七个教学步的相位序，计数、检查点与跳过推进共用
        private static readonly Phase[] StepOrder =
            [Phase.Domain, Phase.Sink, Phase.Panorama, Phase.Wheel, Phase.Rain, Phase.Restart, Phase.Dream];

        #region 本地化
        public static LocalizedText DomainTitle { get; private set; }
        public static LocalizedText DomainBody { get; private set; }
        public static LocalizedText DomainPrompt { get; private set; }
        public static LocalizedText SinkTitle { get; private set; }
        public static LocalizedText SinkBody { get; private set; }
        public static LocalizedText SinkPrompt { get; private set; }
        public static LocalizedText PanoramaTitle { get; private set; }
        public static LocalizedText PanoramaBody { get; private set; }
        public static LocalizedText PanoramaPrompt { get; private set; }
        public static LocalizedText WheelTitle { get; private set; }
        public static LocalizedText WheelBody { get; private set; }
        public static LocalizedText WheelPrompt { get; private set; }
        public static LocalizedText WheelNoMemory { get; private set; }
        public static LocalizedText RainTitle { get; private set; }
        public static LocalizedText RainBody { get; private set; }
        public static LocalizedText RainPrompt { get; private set; }
        public static LocalizedText RestartTitle { get; private set; }
        public static LocalizedText RestartBody { get; private set; }
        public static LocalizedText RestartPrompt { get; private set; }
        public static LocalizedText DreamTitle { get; private set; }
        public static LocalizedText DreamBody { get; private set; }
        public static LocalizedText DreamPrompt { get; private set; }
        public static LocalizedText SkipBtn { get; private set; }
        public static LocalizedText ConfirmBtn { get; private set; }
        public static LocalizedText AssistBtn { get; private set; }
        public static LocalizedText OpenPanoramaBtn { get; private set; }
        public static LocalizedText DismissBtn { get; private set; }
        public static LocalizedText KeyUnbound { get; private set; }
        public static LocalizedText AlreadyDoneNote { get; private set; }
        public static LocalizedText WaterRisingNote { get; private set; }
        public static LocalizedText ResetCooldownFormat { get; private set; }
        public static LocalizedText HelpHover { get; private set; }
        public static LocalizedText MutateFallbackKey { get; private set; }

        public override void SetStaticDefaults() {
            GuideLeadQueue.Register(this);

            DomainTitle = this.GetLocalization(nameof(DomainTitle), () => "Raise the Blood Lake");
            DomainBody = this.GetLocalization(nameof(DomainBody),
                () => "Everything the umbrella owns grows in the blood lake — the hoard, the drowning, the shades. Let the water rise first.");
            DomainPrompt = this.GetLocalization(nameof(DomainPrompt),
                () => "Hold the umbrella and press {0}");

            SinkTitle = this.GetLocalization(nameof(SinkTitle), () => "Sink Something");
            SinkBody = this.GetLocalization(nameof(SinkBody),
                () => "The lake is your private vault. Press the sink key with an item in hand and it sinks to the lakebed; point at a creature and the lake drags it under — drowned bosses become sunken shades, yours forever.");
            SinkPrompt = this.GetLocalization(nameof(SinkPrompt),
                () => "Once the water reaches your feet, press {0} holding an item or pointing at a foe");

            PanoramaTitle = this.GetLocalization(nameof(PanoramaTitle), () => "Open the Lakeheart");
            PanoramaBody = this.GetLocalization(nameof(PanoramaBody),
                () => "One screen holds it all: the hound and the gold flame up top, three seats of shades on the waterline, and the hoard on the lakebed below.");
            PanoramaPrompt = this.GetLocalization(nameof(PanoramaPrompt),
                () => "Click the wind chime at the bottom-left, or hold the umbrella and press {0}");

            WheelTitle = this.GetLocalization(nameof(WheelTitle), () => "Command the Shades");
            WheelBody = this.GetLocalization(nameof(WheelBody),
                () => "Seated shades surface on their own when the lake is ready. The wheel calls them out or holds them back mid-fight — fewer afield, harder each one hits. The gold-flame sector on the same wheel lights or draws back the ghost fire.");
            WheelPrompt = this.GetLocalization(nameof(WheelPrompt),
                () => "Hold {0} to open the wheel, release on a seat to toggle it");
            WheelNoMemory = this.GetLocalization(nameof(WheelNoMemory),
                () => "No shades in the codex yet — drown a boss first, or skip this step.");

            RainTitle = this.GetLocalization(nameof(RainTitle), () => "Mutate into Ghost Rain");
            RainBody = this.GetLocalization(nameof(RainBody),
                () => "The mutate key (middle mouse by default) speaks twice: a tap flips the full lake into ghost rain, a long hold pulls you into the dream. Under the rain, slain foes rise as umbrella thralls that fight for you.");
            RainPrompt = this.GetLocalization(nameof(RainPrompt),
                () => "At full water, tap {0} to flip the lake");

            RestartTitle = this.GetLocalization(nameof(RestartTitle), () => "Rewind in the Rain");
            RestartBody = this.GetLocalization(nameof(RestartBody),
                () => "Ghost rain remembers. One press freezes the scene into a photograph and washes everyone back to where they stood seconds ago — wounds close, curses rinse off, and nothing can touch you while it rewinds.");
            RestartPrompt = this.GetLocalization(nameof(RestartPrompt),
                () => "In ghost rain at full water, press {0}");

            DreamTitle = this.GetLocalization(nameof(DreamTitle), () => "Sink into the Ghost Dream");
            DreamBody = this.GetLocalization(nameof(DreamBody),
                () => "Once the lake is full, hold the mutate key and the reflection pulls you under — hold left-click in the dream to call hounds, press the same key again to return.");
            DreamPrompt = this.GetLocalization(nameof(DreamPrompt),
                () => "At full water, hold {0}");

            SkipBtn = this.GetLocalization(nameof(SkipBtn), () => "Skip");
            ConfirmBtn = this.GetLocalization(nameof(ConfirmBtn), () => "Got it");
            AssistBtn = this.GetLocalization(nameof(AssistBtn), () => "Show me");
            OpenPanoramaBtn = this.GetLocalization(nameof(OpenPanoramaBtn), () => "Open it for me");
            DismissBtn = this.GetLocalization(nameof(DismissBtn), () => "Put away");
            KeyUnbound = this.GetLocalization(nameof(KeyUnbound),
                () => "This step's key \"{0}\" isn't bound. Bind it in Settings > Controls, or click Skip.");
            AlreadyDoneNote = this.GetLocalization(nameof(AlreadyDoneNote),
                () => "This one is already done — read on, then press Got it.");
            WaterRisingNote = this.GetLocalization(nameof(WaterRisingNote),
                () => "Wait for the blood water to reach your feet. Water level: {0}%.");
            ResetCooldownFormat = this.GetLocalization(nameof(ResetCooldownFormat),
                () => "The rewind ink is still coiling back: {0}%");
            HelpHover = this.GetLocalization(nameof(HelpHover),
                () => "Replay the umbrella tutorial");
            MutateFallbackKey = this.GetLocalization(nameof(MutateFallbackKey),
                () => "middle mouse");
        }
        #endregion

        private const int CardW = 340;
        //约9秒卡住才出低调跳过；键未绑定或死路时立即放出
        private const int StuckFramesBeforeSkip = 60 * 9;
        //世界操作步约30秒还没做成，放出「替我演示」；键未绑定时立即放出
        private const int StuckFramesBeforeAssist = 60 * 30;
        //被队列放弃后的让位时长，只挂起绝不写存档标记
        private const int ReserveDeferFrames = 60 * 60;

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        private static int phaseTimer;
        //Sink 步基线：进相位时的湖藏数与记忆
        private static int sinkBaselineCount;
        private static int sinkBaselineMemory;
        /// <summary>进步时完成条件已成立（重看/续讲场景）：不闪卡，改出「明白了」等确认</summary>
        private static bool enterSatisfied;
        private static int reserveDeferTicks;

        #region 引导排队协议
        int IGuideLead.GuidePriority => 12;//晚于比目鱼(10)，早于义体(15)
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        //现行队列不再饿死调用这里；万一被放弃也只挂起让位，检查点在存档里，回来续讲
        void IGuideLead.OnGuideAbandoned() {
            ResetRuntime();
            reserveDeferTicks = ReserveDeferFrames;
        }
        #endregion

        private static KikasaGuideData Guide
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<KikasaGuideData>();

        /// <summary>
        /// 教学卡当前在讲：HUD 的提示行让位，别两处重复同一句按键话。
        /// 同时是鬼雨主题曲发伞后的播放窗口（OniRainTheme），卡停曲停，改语义前先看那边
        /// </summary>
        internal static bool CardVisible
            => Array.IndexOf(StepOrder, currentPhase) >= 0
            && GuideLeadQueue.IsHolder(ModContent.GetInstance<KikasaHudLead>());

        /// <summary>占位：存活 + 未走完 + 未婉拒 + 背包里有鬼伞</summary>
        private static bool Reserving {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return false;
                }
                if (reserveDeferTicks > 0) {
                    return false;
                }
                KikasaGuideData guide = Guide;
                if (guide.GuideSeen || guide.CompletedVersion >= TutorialVersion || guide.Declined) {
                    return false;
                }
                return p.HasItem(ModContent.ItemType<KikasaItem>());
            }
        }

        /// <summary>就绪：占位 + 无对话/过场 + 会话可用</summary>
        private static bool Ready {
            get {
                if (!Reserving) {
                    return false;
                }
                if (NarrativeTriggerGate.IsBusy || InnoVault.Cinematics.CutsceneDirector.IsPlaying) {
                    return false;
                }
                return SessionUsable();
            }
        }

        /// <summary>会话可用；不满足只暂停不重置。湖心景开着不算占用，它本身是教学的一步</summary>
        private static bool SessionUsable() {
            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead) {
                return false;
            }
            if (HackTime.Active) {
                return false;
            }
            if (QuestLog.Instance?.IsOpen == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return true;
        }

        private static KikasaDomainPlayer Domain
            => Main.LocalPlayer.GetModPlayer<KikasaDomainPlayer>();

        //==================== 状态机 ====================

        private static void MarkSeen() {
            KikasaGuideData guide = Guide;
            guide.GuideSeen = true;
            guide.CompletedVersion = TutorialVersion;
            guide.StepCheckpoint = StepOrder.Length;
            currentPhase = Phase.Complete;
            animProgress = 0f;
        }

        private static void ResetRuntime() {
            currentPhase = Phase.Inactive;
            animProgress = 0f;
            phaseTimer = 0;
            enterSatisfied = false;
        }

        private static void SetPhase(Phase phase) {
            currentPhase = phase;
            animProgress = 0f;
            phaseTimer = 0;
            if (phase == Phase.Sink) {
                Player p = Main.LocalPlayer;
                sinkBaselineCount = p.GetModPlayer<KikasaVaultPlayer>().Stored.Count;
                sinkBaselineMemory = p.GetModPlayer<KikasaServantPlayer>().LastDrownedType;
            }
            //Sink 基线刚重记永不算已满足；其余步进步即查，已满足的换确认按钮不闪卡
            enterSatisfied = phase != Phase.Sink
                && Array.IndexOf(StepOrder, phase) >= 0 && StepConditionMet(phase);
        }

        /// <summary>各步的完成真值，推进与「进步已满足」变体共用同一份判定</summary>
        private static bool StepConditionMet(Phase phase) {
            KikasaDomainPlayer domain = Domain;
            return phase switch {
                Phase.Domain => domain.AnyActive,
                Phase.Sink => SinkProgressed(),
                Phase.Panorama => KikasaPanoramaUI.Instance?.IsOpen == true,
                Phase.Wheel => KikasaServantWheelController.LocalInstance?.IsOpen == true,
                Phase.Rain => domain.IsRainForm,
                Phase.Restart => KikasaReset.IsPlayerAffected(Main.myPlayer),
                Phase.Dream => domain.InDreamPhase,
                _ => false,
            };
        }

        private static bool SinkProgressed() {
            Player p = Main.LocalPlayer;
            return p.GetModPlayer<KikasaVaultPlayer>().Stored.Count > sinkBaselineCount
                || p.GetModPlayer<KikasaServantPlayer>().LastDrownedType != sinkBaselineMemory;
        }

        /// <summary>
        /// 完成当前步并写检查点，下一步按检查点续跳——
        /// 场面失守退回重讲的步做完后直接回到没讲过的地方，不重放中间几步
        /// </summary>
        private static void AdvanceStep() {
            int idx = Array.IndexOf(StepOrder, currentPhase);
            if (idx < 0) {
                MarkSeen();
                return;
            }
            KikasaGuideData guide = Guide;
            guide.StepCheckpoint = Math.Max(guide.StepCheckpoint, idx + 1);
            if (guide.StepCheckpoint >= StepOrder.Length) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.45f, Volume = 0.55f });
                MarkSeen();
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f, Volume = 0.5f });
            SetPhase(StepOrder[guide.StepCheckpoint]);
        }

        private static void StartFromCheckpoint() {
            int start = Math.Clamp(Guide.StepCheckpoint, 0, StepOrder.Length - 1);
            SetPhase(StepOrder[start]);
        }

        /// <summary>卡角「收起」：记婉拒但检查点留着，湖心景「?」随时重开续讲</summary>
        private static void Dismiss() {
            Guide.Declined = true;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            ResetRuntime();
        }

        /// <summary>
        /// 湖心景页脚「?」：清进度当场从头重讲。ForceHold 抢展示权（任务书先例，
        /// 不算把别人挤成放弃）；合屏让位，前几步都在世界里操作
        /// </summary>
        internal static void RestartFromHelp() {
            KikasaGuideData guide = Guide;
            guide.GuideSeen = false;
            guide.CompletedVersion = 0;
            guide.StepCheckpoint = 0;
            guide.Declined = false;
            reserveDeferTicks = 0;
            GuideLeadQueue.ForceHold(ModContent.GetInstance<KikasaHudLead>());
            KikasaPanoramaUI.Instance?.Close();
            //绘制帧里队列 Pump 已跑过，必须当帧起步卡片才画得出
            SetPhase(Phase.Domain);
            animProgress = 1f;
        }

        /// <summary>
        /// 场面失守退步：依赖湖的步湖收了退回开域重讲；
        /// 重启只活在鬼雨里，稳态翻回血湖就退回异化步（翻转途中不动）。
        /// 只退步等人，不逐帧硬扳玩家刚做的操作
        /// </summary>
        private static bool KeepStepAlive(KikasaDomainPlayer domain) {
            bool needLake = currentPhase
                is Phase.Sink or Phase.Rain or Phase.Restart or Phase.Dream;
            if (needLake && !domain.AnyActive) {
                SetPhase(Phase.Domain);
                return false;
            }
            if (currentPhase == Phase.Restart && !domain.IsRainForm
                && domain.Phase == KikasaDomainPhase.Open) {
                SetPhase(Phase.Rain);
                return false;
            }
            return true;
        }

        public override void OnWorldUnload() {
            ResetRuntime();
            reserveDeferTicks = 0;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            if (reserveDeferTicks > 0) {
                reserveDeferTicks--;
            }
            //统一排队、未轮到则挂起待命；检查点在存档里，回来从断点续讲
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                    ResetRuntime();
                }
                return;
            }

            if (currentPhase == Phase.Inactive) {
                StartFromCheckpoint();
            }
            //前提暂时不满足（骇客/全屏 UI/死亡）：暂停推进与绘制，不回退
            if (!SessionUsable()) {
                return;
            }

            phaseTimer++;
            animProgress = MathHelper.Lerp(animProgress, 1f, 0.12f);

            KikasaDomainPlayer domain = Domain;
            if (!KeepStepAlive(domain)) {
                return;
            }

            //进步时就满足的等确认按钮，别让卡片一闪而过
            if (!enterSatisfied && StepConditionMet(currentPhase)) {
                AdvanceStep();
            }
        }

        //==================== 帮做 ====================

        /// <summary>世界操作步：卡住够久（或键未绑定）时放出「替我演示」</summary>
        private static bool IsWorldAssistStep(Phase phase)
            => phase is Phase.Domain or Phase.Rain or Phase.Restart or Phase.Dream;

        /// <summary>
        /// 「替我演示」此刻点得动吗。不满足时按钮暗显——可用性在点击前可见，
        /// 差什么由 subText 读数说明
        /// </summary>
        private static bool AssistReady(KikasaDomainPlayer domain) {
            return currentPhase switch {
                //TryToggle 在 Open 态是收域，帮做只认"域还没起来"的场合
                Phase.Domain => (domain.Phase == KikasaDomainPhase.Closed
                        || domain.Phase == KikasaDomainPhase.Closing)
                    && !Main.LocalPlayer.GetModPlayer<OniDomainPlayer>().AnyActive,
                Phase.Rain => domain.Phase == KikasaDomainPhase.Open && domain.RiseT >= 0.999f,
                //与 KikasaReset.TryReset 的受理门同口径
                Phase.Restart => domain.LakeAbilityReady && domain.IsRainForm
                    && KikasaReset.LocalCooldown01 <= 0f,
                Phase.Dream => domain.DreamPullReady,
                _ => false,
            };
        }

        /// <summary>替玩家把这一步做掉；受理后有完整演出，完成条件自然变真推进</summary>
        private static void PerformAssist() {
            Player p = Main.LocalPlayer;
            bool ok;
            switch (currentPhase) {
                case Phase.Domain:
                    ok = KikasaDomain.TryToggle(p, out _);
                    break;
                case Phase.Rain:
                    ok = KikasaDomain.TryMutate(p, out _);
                    break;
                case Phase.Restart:
                    //void 受理，内部自带拒绝反馈
                    KikasaReset.TryReset(p);
                    ok = true;
                    break;
                case Phase.Dream:
                    ok = KikasaDomain.TryDreamPull(p, out _);
                    break;
                default:
                    ok = false;
                    break;
            }
            if (!ok) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.55f, Volume = 0.4f });
            }
        }

        //==================== 绘制 ====================

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (Array.IndexOf(StepOrder, currentPhase) < 0) {
                return;
            }
            if (!GuideLeadQueue.IsHolder(this) || !SessionUsable() || animProgress < 0.02f) {
                return;
            }
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) {
                return;
            }
            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: Kikasa HUD Guide",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            float alpha = MathHelper.Clamp(animProgress, 0f, 1f);
            KikasaDomainPlayer domain = Domain;
            float rain = MathHelper.Clamp(domain.RainBlend, 0f, 1f);
            float time = Main.GlobalTimeWrappedHourly;

            //第一步给掌中风铃的铃身一个脉冲环：铃即领域的读数。伞没拿在手上时风铃不在场，环也不画；
            //第三步同样用脉冲环指路，点铃即开湖心景
            bool ringStep = currentPhase == Phase.Domain || currentPhase == Phase.Panorama;
            if (ringStep && KikasaHud.Instance?.Active == true) {
                float pulse = KikasaHudTheme.Breath(time, 1.3f, 3f);
                KikasaVaultRenderer.DrawRing(sb, KikasaHud.BellAnchor,
                    KikasaHudTheme.BellSize * 0.5f + 12f + pulse * 4f, 12f,
                    KikasaHudTheme.Glow(rain) * ((0.35f + pulse * 0.2f) * alpha));
            }

            //====== 键位与文案 ======
            //异化键（雨/梦两步共用）有原生中键兜底，不算未绑定
            ModKeybind actionKey = currentPhase switch {
                Phase.Domain => CWRKeySystem.Legend_Domain,
                Phase.Sink => CWRKeySystem.Kikasa_Sink,
                Phase.Panorama => CWRKeySystem.Legend_UIControl,
                Phase.Wheel => CWRKeySystem.RadialWheel_Key,
                Phase.Restart => CWRKeySystem.Legend_Restart,
                _ => CWRKeySystem.Kikasa_DomainMutate,
            };
            bool keyBound = actionKey == null
                || currentPhase is Phase.Rain or Phase.Dream
                || !CWRKeySystem.IsKeybindUnbound(actionKey);
            //雨/梦步的异化键被清空绑定时游戏逻辑回退原生中键，提示跟着显示中键而不是「未绑定」
            bool mutateFallback = currentPhase is Phase.Rain or Phase.Dream
                && CWRKeySystem.IsKeybindUnbound(actionKey);
            string keyText = actionKey == null
                ? string.Empty
                : mutateFallback
                    ? MutateFallbackKey.Value
                    : actionKey.ToTooltipString(CWRKeySystem.Notbound.Value);

            (string title, string body, string promptFmt) = currentPhase switch {
                Phase.Domain => (DomainTitle.Value, DomainBody.Value, DomainPrompt.Value),
                Phase.Sink => (SinkTitle.Value, SinkBody.Value, SinkPrompt.Value),
                Phase.Panorama => (PanoramaTitle.Value, PanoramaBody.Value, PanoramaPrompt.Value),
                Phase.Wheel => (WheelTitle.Value, WheelBody.Value, WheelPrompt.Value),
                Phase.Rain => (RainTitle.Value, RainBody.Value, RainPrompt.Value),
                Phase.Restart => (RestartTitle.Value, RestartBody.Value, RestartPrompt.Value),
                _ => (DreamTitle.Value, DreamBody.Value, DreamPrompt.Value),
            };
            string promptText = string.IsNullOrEmpty(keyText)
                ? promptFmt
                : string.Format(promptFmt, keyText);

            //转盘步死路：册里还没有沉影，开盘也只有空席可看
            bool wheelDeadEnd = currentPhase == Phase.Wheel
                && Main.LocalPlayer.GetModPlayer<KikasaServantPlayer>().BuildCodexKeys().Count == 0;
            string subText = ResolveSubText(domain, actionKey, keyBound, wheelDeadEnd);

            //====== 量高排版（字号跟全域字体规范：正文 ≥0.8） ======
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float titleSc = 0.95f;
            const float bodySc = 0.8f;
            const float subSc = 0.75f;
            float lineT = font.MeasureString("A").Y * titleSc + 2f;
            float lineB = font.MeasureString("A").Y * bodySc + 1f;

            int bodyWrapW = (int)((CardW - 28) / bodySc);
            List<string> bodyLines = WrapLines(font, body, bodyWrapW);
            List<string> promptLines = WrapLines(font, promptText, bodyWrapW);
            List<string> subLines = subText != null
                ? WrapLines(font, subText, (int)((CardW - 28) / subSc))
                : null;
            float cardH = 12f + lineT + 2f + 7f
                + bodyLines.Count * lineB + 4f
                + promptLines.Count * lineB
                + (subLines?.Count ?? 0) * (lineB - 1f)
                + 38f;

            //====== 卡位：默认悬在风铃上方；湖心景开着时让位到右上 ======
            float cardX, cardY;
            bool panoOpen = KikasaPanoramaUI.Instance?.IsOpen == true;
            if (panoOpen) {
                cardX = MathHelper.Clamp(KikasaHudTheme.UIScreenW - CardW - 20f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenW - CardW - 16f));
                cardY = 78f;
            }
            else {
                Vector2 chime = KikasaHud.Anchor;
                cardX = MathHelper.Clamp(chime.X - 30f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenW - CardW - 16f));
                cardY = MathHelper.Clamp(
                    chime.Y - (KikasaHudTheme.ChimeH * 0.5f + 10f) - cardH - 8f,
                    16f, Math.Max(16f, KikasaHudTheme.UIScreenH - cardH - 16f));
            }
            float slideY = (1f - alpha) * 16f;
            Rectangle card = new((int)cardX, (int)(cardY + slideY), CardW, (int)cardH);

            KikasaPanoramaRenderer.DrawCardBg(sb, card, alpha, rain);
            //连线：卡底垂到风铃檐钩顶；湖心景让位时不画，风铃不在场也不画
            if (!panoOpen && KikasaHud.Instance?.Active == true) {
                DrawDashedLine(sb, new Vector2(card.X + 26f, card.Bottom),
                    KikasaHud.Anchor + new Vector2(0f, -(KikasaHudTheme.ChimeH * 0.5f + 6f)),
                    KikasaHudTheme.Accent(rain) * (0.45f * alpha), time);
            }

            //====== 内容 ======
            float px = card.X + 14f;
            float py = card.Y + 12f;
            Color titleCol = KikasaHudTheme.Glow(rain);
            Color bodyCol = KikasaHudTheme.TextDim(rain);
            Color promptCol = KikasaHudTheme.Text(rain);
            Color accent = KikasaHudTheme.Accent(rain);

            int stepIdx = Array.IndexOf(StepOrder, currentPhase);
            string counter = $"{stepIdx + 1:00} / {StepOrder.Length:00}";
            float counterW = font.MeasureString(counter).X * subSc;
            Utils.DrawBorderString(sb, counter,
                new Vector2(card.Right - 14f - counterW, py),
                KikasaHudTheme.TextDim(rain) * (0.7f * alpha), subSc);
            Utils.DrawBorderString(sb, title, new Vector2(px, py), titleCol * alpha, titleSc);
            py += lineT + 2f;

            KikasaVaultRenderer.DrawLine(sb, new Vector2(px, py),
                new Vector2(px + CardW - 28f, py), 1f, accent * (0.4f * alpha));
            py += 7f;

            foreach (string wl in bodyLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px, py), bodyCol * (0.9f * alpha), bodySc);
                py += lineB;
            }
            py += 4f;

            float promptPulse = 0.8f + 0.2f * MathF.Sin(time * 4.2f);
            foreach (string wl in promptLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px, py),
                    promptCol * (alpha * promptPulse), bodySc);
                py += lineB;
            }

            if (subLines != null) {
                float pulseKey = 0.75f + 0.25f * MathF.Sin(time * 7f);
                foreach (string wl in subLines) {
                    Utils.DrawBorderString(sb, wl, new Vector2(px, py),
                        KikasaHudTheme.Glow(rain) * (0.85f * alpha * pulseKey), subSc);
                    py += lineB - 1f;
                }
            }

            DrawButtonRow(sb, card, alpha, rain, domain, keyBound, wheelDeadEnd);
        }

        /// <summary>
        /// 防呆读数：紧急的先说。键未绑定 > 转盘死路 > 已完成提示 > 步内条件差什么
        /// </summary>
        private static string ResolveSubText(KikasaDomainPlayer domain,
            ModKeybind actionKey, bool keyBound, bool wheelDeadEnd) {
            if (!keyBound) {
                //报键位表真名，玩家去设置里才找得到要绑哪一把
                return string.Format(KeyUnbound.Value, actionKey.DisplayName.Value);
            }
            if (wheelDeadEnd) {
                return WheelNoMemory.Value;
            }
            if (enterSatisfied) {
                return AlreadyDoneNote.Value;
            }
            switch (currentPhase) {
                //沉与翻与梦都吃满水：域开着水没到脚，给出水位读数让人知道在等什么
                case Phase.Sink:
                    if (domain.AnyActive && domain.RiseT < 0.999f) {
                        return WaterNote(domain);
                    }
                    break;
                case Phase.Rain:
                case Phase.Dream:
                    if (domain.Phase == KikasaDomainPhase.Open && domain.RiseT < 0.999f) {
                        return WaterNote(domain);
                    }
                    break;
                case Phase.Restart: {
                    float cd = KikasaReset.LocalCooldown01;
                    if (cd > 0f) {
                        return string.Format(ResetCooldownFormat.Value,
                            (int)MathF.Round(cd * 100f));
                    }
                    if (domain.Phase == KikasaDomainPhase.Open && domain.IsRainForm
                        && domain.RiseT < 0.999f) {
                        return WaterNote(domain);
                    }
                    break;
                }
            }
            return null;
        }

        /// <summary>等水提示带当前水位；向下取整，没真满就不显示 100%</summary>
        private static string WaterNote(KikasaDomainPlayer domain)
            => string.Format(WaterRisingNote.Value,
                (int)(MathHelper.Clamp(domain.RiseT, 0f, 1f) * 100f));

        //==================== 按钮行 ====================

        /// <summary>
        /// 卡底一行三席：左角低调「收起」常驻；右侧自右向左排「跳过」（9秒/死路/未绑键）
        /// 与主动作按钮（明白了 / 帮我打开 / 替我演示，同刻至多一个）
        /// </summary>
        private static void DrawButtonRow(SpriteBatch sb, Rectangle card, float alpha,
            float rain, KikasaDomainPlayer domain, bool keyBound, bool wheelDeadEnd) {
            const int btnH = 22, margin = 10, gap = 8;
            int rowY = card.Bottom - btnH - margin;

            //收起：常驻，婉拒后由湖心景「?」重开
            Rectangle dismissBtn = ButtonRect(DismissBtn.Value, card.X + margin, rowY, btnH);
            if (DrawCardButton(sb, dismissBtn, DismissBtn.Value, alpha, rain,
                emphasized: false, enabled: true)) {
                Dismiss();
                return;
            }

            int rightX = card.Right - margin;

            //跳过：卡住/死路/键未绑定的出路
            if (!keyBound || wheelDeadEnd || phaseTimer > StuckFramesBeforeSkip) {
                Rectangle skipBtn = ButtonRectRight(SkipBtn.Value, rightX, rowY, btnH);
                rightX = skipBtn.X - gap;
                if (DrawCardButton(sb, skipBtn, SkipBtn.Value, alpha, rain,
                    emphasized: false, enabled: true)) {
                    AdvanceStep();
                    return;
                }
            }

            //主动作按钮：同刻至多一个
            string mainLabel = null;
            bool mainEnabled = true;
            if (enterSatisfied) {
                mainLabel = ConfirmBtn.Value;
            }
            else if (currentPhase == Phase.Panorama) {
                mainLabel = OpenPanoramaBtn.Value;
            }
            else if (IsWorldAssistStep(currentPhase)
                && (!keyBound || phaseTimer > StuckFramesBeforeAssist)) {
                mainLabel = AssistBtn.Value;
                mainEnabled = AssistReady(domain);
            }
            if (mainLabel == null) {
                return;
            }

            Rectangle mainBtn = ButtonRectRight(mainLabel, rightX, rowY, btnH);
            if (!DrawCardButton(sb, mainBtn, mainLabel, alpha, rain,
                emphasized: true, enabled: mainEnabled)) {
                return;
            }
            if (enterSatisfied) {
                AdvanceStep();
            }
            else if (currentPhase == Phase.Panorama) {
                KikasaPanoramaUI.Instance?.Open();
            }
            else {
                PerformAssist();
            }
        }

        private static Rectangle ButtonRect(string label, int x, int y, int h) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int w = (int)(font.MeasureString(label).X * 0.75f) + 22;
            return new Rectangle(x, y, w, h);
        }

        private static Rectangle ButtonRectRight(string label, int rightX, int y, int h) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int w = (int)(font.MeasureString(label).X * 0.75f) + 22;
            return new Rectangle(rightX - w, y, w, h);
        }

        private static List<string> WrapLines(DynamicSpriteFont font, string text, int wrapW) {
            List<string> result = [];
            foreach (string line in text.Split('\n')) {
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    result.Add(wl.TrimEnd('-', ' '));
                }
            }
            return result;
        }

        //虚线连接线：卡片指向风铃
        private static void DrawDashedLine(SpriteBatch sb, Vector2 from, Vector2 to,
            Color color, float time) {
            Vector2 dir = to - from;
            float len = dir.Length();
            if (len < 8f) {
                return;
            }
            dir /= len;
            const float dash = 5f, gap = 4f;
            float offset = (time * 18f) % (dash + gap);
            for (float t = offset; t < len; t += dash + gap) {
                float end = MathF.Min(t + dash, len);
                KikasaVaultRenderer.DrawLine(sb, from + dir * t, from + dir * end, 1.2f, color);
            }
        }

        /// <summary>
        /// 卡上按钮通用件。emphasized=主动作（边框亮些），disabled=暗显且点击只回拒绝低音——
        /// 可用性在点击前可见，点了也绝不无声
        /// </summary>
        private static bool DrawCardButton(SpriteBatch sb, Rectangle btn, string label,
            float alpha, float rain, bool emphasized, bool enabled) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float sc = 0.75f;
            Vector2 size = font.MeasureString(label) * sc;

            Vector2 uiMouse = KikasaHudTheme.UIMouse;
            bool hovered = btn.Contains((int)uiMouse.X, (int)uiMouse.Y);
            float dim = enabled ? 1f : 0.45f;
            float emph = emphasized ? 1.2f : 1f;
            Color bg = KikasaHudTheme.Deep(rain) * ((hovered && enabled ? 0.95f : 0.7f) * alpha * dim);
            Color border = KikasaHudTheme.Accent(rain)
                * ((hovered && enabled ? 0.85f : 0.45f) * emph * alpha * dim);
            Color textCol = (hovered && enabled
                ? KikasaHudTheme.Text(rain)
                : KikasaHudTheme.TextDim(rain)) * (alpha * dim);

            sb.Draw(VaultAsset.placeholder2.Value, btn, bg);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Left, btn.Top),
                new Vector2(btn.Right, btn.Top), 1f, border);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Left, btn.Bottom),
                new Vector2(btn.Right, btn.Bottom), 1f, border * 0.7f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Left, btn.Top),
                new Vector2(btn.Left, btn.Bottom), 1f, border * 0.85f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(btn.Right, btn.Top),
                new Vector2(btn.Right, btn.Bottom), 1f, border * 0.85f);
            Vector2 textPos = btn.Center.ToVector2() - size * 0.5f;
            Utils.DrawBorderString(sb, label, textPos, textCol, sc);

            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    if (!enabled) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.55f, Volume = 0.4f });
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
