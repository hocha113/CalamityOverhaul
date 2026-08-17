using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using CalamityOverhaul.Content.TimeFreezes;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    internal enum OnikiriPracticeCheckpoint : byte
    {
        None,
        Omote,
        Ura,
        Dismembered,
        Closed,
    }

    internal enum OnikiriTutorialFeedback : byte
    {
        None,
        Waiting,
        Busy,
        Retry,
        /// <summary>行囊无鬼切,硬闸挂起依赖手持的步骤</summary>
        NeedBlade,
    }

    /// <summary>本次进入教程的方式</summary>
    internal enum OnikiriTutorialEntry : byte
    {
        /// <summary>常规排队进入,按存档决定询问/讲解/续练</summary>
        Auto,
        /// <summary>稽古符启动,从头讲解,不再询问</summary>
        ForceFull,
        /// <summary>调试直入实操段</summary>
        ForcePractice,
    }

    /// <summary>鬼切教程的本地玩家运行态</summary>
    internal sealed class OnikiriTutorialPlayer : ModPlayer
    {
        private const int TargetRetryFrames = 120;
        private const int SelfCutStartTimeout = 180;
        private const int DismemberVisualTimeout = 360;

        internal int CurrentStep { get; private set; } = -1;
        internal int StepTimer { get; private set; }
        internal bool IsRunning => CurrentStep >= 0 && CurrentStep < OnikiriTutorialFlow.Step_Done;
        /// <summary>越过排队前提的强制会话(稽古符或调试)</summary>
        internal bool Forced { get; private set; }
        internal uint ReservationDeferredUntil { get; private set; }
        internal OnikiriTutorialFeedback Feedback { get; private set; }

        private bool initialized;
        private bool subscribed;
        private bool sigilOpenedThisStep;
        private bool codexOpenedThisStep;
        private bool suppressCommandEvent;
        private bool prepareReachedClosed;
        /// <summary>本实操步已见过它的起始相位,达标才算玩家亲手做到</summary>
        private bool practicePrimed;
        private int needBladeTimer;
        private int resumeStep;
        private int feedbackTimer;
        private int targetRetryTimer;
        private int targetSession;
        private bool sawSelfCutLock;
        private bool sawTargetSplit;
        private bool healthRecovered;
        private bool healthGuardActive;
        private bool dismemberInputArmed;
        private bool previousTutorialUiLeft;
        private bool previousMiddleDown;
        private bool detachedSafetySawLock;
        private int detachedSafetyTimer;
        private int guardedLife;
        private int lifeBeforeDemonstration;
        private OniMeiSnapshot meiSnapshot;
        /// <summary>教程锁刀槽(-1 未锁)。HUD/改铭台/点鬼簿都要求手持鬼切</summary>
        private int lockedOnikiriSlot = -1;

        internal bool ReservationDeferred
            => Main.GameUpdateCount < ReservationDeferredUntil;

        internal NPC TutorialTarget
            => targetSession > 0
                ? OnikiriTutorialNet.GetLocalTarget(Player.whoAmI, targetSession)
                : null;

        internal void TickTutorial() {
            if (!initialized) {
                if (Player.dead || !Player.active) {
                    return;
                }
                OnikiriGuideData guide = GuideData;
                if (!Forced && (guide.Declined
                    || guide.CompletedVersion >= OnikiriTutorialLead.TutorialVersion)) {
                    return;
                }
                InitializeTutorial();
            }
            if (!IsRunning) {
                return;
            }
            if (Player.dead || !Player.active) {
                Suspend(releaseTarget: true);
                return;
            }
            //暂停时不走表:否则挂机回来会发现讲解卡自己翻过去了
            if (Main.gamePaused) {
                return;
            }

            StepTimer++;
            if (CurrentStep == OnikiriTutorialFlow.Step_Dismember && !Main.mouseLeft) {
                dismemberInputArmed = true;
            }
            if (feedbackTimer > 0 && --feedbackTimer == 0) {
                Feedback = OnikiriTutorialFeedback.None;
            }

            //讲解与实操都依赖鬼切 HUD/台账:每帧确保手持并锁槽,避免切刀后点鬼簿闪关
            MaintainOnikiriHoldLock();
            if (TickNeedBladeAbort()) {
                return;
            }

            HandleUnboundFallbackInput();
            MaintainTutorialTarget();
            AdvanceIfReady();
        }

        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (!IsRunning || lockedOnikiriSlot < 0) {
                return;
            }
            //冻结未开时也要吞快捷栏,否则锁刀会被数字键拆掉
            if (WorldFreezeSystem.IsActive) {
                return;
            }
            triggersSet.Hotbar1 = false;
            triggersSet.Hotbar2 = false;
            triggersSet.Hotbar3 = false;
            triggersSet.Hotbar4 = false;
            triggersSet.Hotbar5 = false;
            triggersSet.Hotbar6 = false;
            triggersSet.Hotbar7 = false;
            triggersSet.Hotbar8 = false;
            triggersSet.Hotbar9 = false;
            triggersSet.Hotbar10 = false;
            triggersSet.HotbarPlus = false;
            triggersSet.HotbarMinus = false;
            triggersSet.RadialHotbar = false;
            triggersSet.RadialQuickbar = false;
        }

        public override void PreUpdate() {
            if (IsRunning) {
                MaintainOnikiriHoldLock();
            }
        }

        public override void PostUpdate() {
            if (IsRunning) {
                MaintainOnikiriHoldLock();
            }
            if (healthGuardActive) {
                SetPlayerLifeAtLeast(GetTutorialLifeFloor(), showEffect: false);
            }
            if (detachedSafetyTimer <= 0) {
                return;
            }
            bool locked = OniPlayerDismember.IsLocked(Player);
            if (locked) {
                detachedSafetySawLock = true;
            }
            if ((detachedSafetySawLock && !locked) || --detachedSafetyTimer <= 0) {
                detachedSafetyTimer = 0;
                detachedSafetySawLock = false;
                RestoreTutorialHealth();
            }
        }

        internal void HandlePrimaryAction() {
            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_Ask:
                    AcceptTutorial();
                    break;
                case OnikiriTutorialFlow.Step_HudIntro:
                case OnikiriTutorialFlow.Step_Domain:
                    AdvanceExplanatoryStep();
                    break;
                case OnikiriTutorialFlow.Step_Mei:
                    //台开着=读完了,点已知晓走人;台没开就先开台
                    if (OniMeiUI.Instance?.IsOpen ?? false) {
                        AdvanceExplanatoryStep();
                    }
                    else {
                        TryBeginLedgerOpen(() => OniMeiUI.Instance?.Open());
                    }
                    break;
                case OnikiriTutorialFlow.Step_Codex:
                    if (OniMeiCodexUI.Instance?.IsOpen ?? false) {
                        OniMeiCodexUI.Instance.Close();
                        AdvanceExplanatoryStep();
                    }
                    else if (!(OniMeiUI.Instance?.IsOpen ?? false)) {
                        TryBeginLedgerOpen(() => OniMeiUI.Instance?.Open());
                    }
                    break;
                case OnikiriTutorialFlow.Step_Sigil:
                    if (OniSigilUI.Instance?.IsOpen ?? false) {
                        OniSigilUI.Instance.Close();
                        AdvanceExplanatoryStep();
                    }
                    else if (!(OniMeiUI.Instance?.IsOpen ?? false)) {
                        TryBeginLedgerOpen(() => OniMeiUI.Instance?.Open());
                    }
                    break;
                case OnikiriTutorialFlow.Step_OpenOmote:
                case OnikiriTutorialFlow.Step_FlipUra:
                case OnikiriTutorialFlow.Step_Dismember:
                case OnikiriTutorialFlow.Step_CloseEye:
                    if (Feedback == OnikiriTutorialFeedback.Retry) {
                        RetryCurrentStep();
                    }
                    else if (CanSkipPracticeStep) {
                        SkipPracticeStep();
                    }
                    break;
            }
        }

        internal void HandleSecondaryAction() {
            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_Ask:
                    DeclineTutorial();
                    break;
                case OnikiriTutorialFlow.Step_HudIntro:
                case OnikiriTutorialFlow.Step_Mei:
                case OnikiriTutorialFlow.Step_Domain:
                    AdvanceExplanatoryStep();
                    break;
                case OnikiriTutorialFlow.Step_Codex:
                    if (!(OniMeiCodexUI.Instance?.IsOpen ?? false)) {
                        TryBeginLedgerOpen(OniMeiCodexUI.OpenFromStand);
                    }
                    break;
                case OnikiriTutorialFlow.Step_Sigil:
                    if (!(OniSigilUI.Instance?.IsOpen ?? false)) {
                        TryBeginLedgerOpen(() => OniSigilUI.Instance?.Open());
                    }
                    break;
                case OnikiriTutorialFlow.Step_Prepare:
                    //准备段只是复位,等不动就直接进下一步,后续步骤自己会归位
                    EnterResumeStep();
                    break;
                case OnikiriTutorialFlow.Step_OpenOmote:
                case OnikiriTutorialFlow.Step_FlipUra:
                case OnikiriTutorialFlow.Step_Dismember:
                case OnikiriTutorialFlow.Step_CloseEye:
                    PerformAssistedAction();
                    break;
            }
        }

        /// <summary>
        /// 询问步之外都有的收起入口:补符、记为婉拒,排队不会下一帧再接回来。
        /// 这条是玩家主动点的,才允许改存档
        /// </summary>
        internal void AbortTutorial() {
            if (!IsRunning || CurrentStep == OnikiriTutorialFlow.Step_Ask) {
                return;
            }
            OnikiriGuideData guide = GuideData;
            guide.AskAnswered = true;
            guide.Declined = true;
            SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.4f, Volume = 0.35f });
            OniKeikoRune.GrantTo(Player);
            ResetAllRuntime();
        }

        /// <summary>
        /// 无刀收摊。不写 Declined——这不是玩家的决定,而且补符万一落地丢了就再也开不起来。
        /// 只把排队推远一分钟,刀回到行囊后自己会重新找上门,讲解也从存档的落点接着走
        /// </summary>
        private void SuspendForMissingBlade() {
            Suspend(releaseTarget: true);
            ClearForced();
            ReservationDeferredUntil = Main.GameUpdateCount + 60 * 60;
        }

        /// <summary>实操步久攻不下后开放的硬跳过</summary>
        internal bool CanSkipPracticeStep
            => StepTimer >= OnikiriTutorialFlow.PracticeSkipDelayFrames
                && CurrentStep is OnikiriTutorialFlow.Step_OpenOmote
                    or OnikiriTutorialFlow.Step_FlipUra
                    or OnikiriTutorialFlow.Step_Dismember
                    or OnikiriTutorialFlow.Step_CloseEye;

        private void SkipPracticeStep() {
            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_OpenOmote:
                    WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Omote);
                    SetStep(OnikiriTutorialFlow.Step_FlipUra);
                    break;
                case OnikiriTutorialFlow.Step_FlipUra:
                    WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Ura);
                    SetStep(OnikiriTutorialFlow.Step_Dismember);
                    break;
                case OnikiriTutorialFlow.Step_Dismember:
                    WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Dismembered);
                    ReleaseTutorialTarget();
                    SetStep(OnikiriTutorialFlow.Step_CloseEye);
                    break;
                case OnikiriTutorialFlow.Step_CloseEye:
                    //跳过收域不能把人留在里世界:他正是还没学会怎么出来才跳的
                    NormalizeClosed();
                    WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Closed);
                    SetStep(OnikiriTutorialFlow.Step_Done);
                    break;
            }
        }

        internal void NotifyDismemberMiss() {
            if (CurrentStep == OnikiriTutorialFlow.Step_Dismember) {
                SetFeedback(OnikiriTutorialFeedback.Retry, 150);
            }
        }

        internal bool TryConsumeDismemberInput() {
            if (CurrentStep != OnikiriTutorialFlow.Step_Dismember || !dismemberInputArmed) {
                return false;
            }
            dismemberInputArmed = false;
            return true;
        }

        internal bool PollTutorialUiClick(bool mouseDown) {
            bool clicked = mouseDown && !previousTutorialUiLeft;
            previousTutorialUiLeft = mouseDown;
            return clicked;
        }

        internal void DeferAfterQueueAbandon() {
            ReservationDeferredUntil = Main.GameUpdateCount + 60;
            Suspend(releaseTarget: true);
        }

        internal void ForceStartPractice() => ForceStart(OnikiriTutorialEntry.ForcePractice);

        /// <summary>稽古符启动:越过排队前提,讲解与实操都从头重走</summary>
        internal void ForceStartFull() {
            GuideData.PracticeCheckpoint = 0;
            ForceStart(OnikiriTutorialEntry.ForceFull);
        }

        private void ForceStart(OnikiriTutorialEntry entry) {
            Forced = true;
            ReservationDeferredUntil = 0;
            Suspend(releaseTarget: true);
            InitializeTutorial(entry);
        }

        internal void ClearForced() {
            Forced = false;
            ReservationDeferredUntil = 0;
        }

        internal void Suspend(bool releaseTarget) {
            if (!initialized && !subscribed && targetSession <= 0) {
                return;
            }
            RestoreMeiSnapshotIfNeeded();
            if (targetSession > 0) {
                OniSeverStrike.CancelPendingTutorialStrikes(Player, targetSession);
            }
            if (healthGuardActive && CurrentStep == OnikiriTutorialFlow.Step_Backlash) {
                detachedSafetySawLock = sawSelfCutLock || OniPlayerDismember.IsLocked(Player);
                detachedSafetyTimer = SelfCutStartTimeout;
            }
            else {
                RestoreTutorialHealth();
            }
            Unsubscribe();
            if (releaseTarget && targetSession > 0) {
                OnikiriTutorialNet.RequestReleaseTarget(targetSession);
            }
            targetSession = 0;
            CurrentStep = -1;
            StepTimer = 0;
            initialized = false;
            prepareReachedClosed = false;
            practicePrimed = false;
            codexOpenedThisStep = false;
            needBladeTimer = 0;
            dismemberInputArmed = false;
            lockedOnikiriSlot = -1;
            Feedback = OnikiriTutorialFeedback.None;
            feedbackTimer = 0;
            previousMiddleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
            previousTutorialUiLeft = Main.mouseLeft;
        }

        internal void ResetAllRuntime() {
            Suspend(releaseTarget: true);
            ClearForced();
        }

        /// <summary>
        /// 讲解段打开改铭台时留档,关台即回滚,教程里试刻的铭不进存档。
        /// 只认手持那把刀自己的铭库,不接受外部传入,免得把展示缓存当成刀上真值存下来
        /// </summary>
        internal void BeginMeiTransaction() {
            if (meiSnapshot != null || CurrentStep is not (OnikiriTutorialFlow.Step_Mei
                or OnikiriTutorialFlow.Step_Sigil)) {
                return;
            }
            OnikiriData data = OnikiriData.TryGet(Player?.GetItem());
            if (data != null) {
                meiSnapshot = new OniMeiSnapshot(data);
            }
        }

        internal void RestoreMeiSnapshotIfNeeded() {
            if (meiSnapshot == null) {
                return;
            }
            OniMeiSnapshot snapshot = meiSnapshot;
            meiSnapshot = null;

            Item held = Player?.GetItem();
            OnikiriData data = OnikiriData.TryGet(held);
            //中途换刀则快照已不对应这把,宁可放弃回滚也不能把铭盖到别的刀上
            if (data == null || data.InstanceId != snapshot.InstanceId) {
                return;
            }
            data.Mei.CopyFrom(snapshot.Store);
            //修订必须前进一格,否则服务端 ReconcileAuthoritativeState 会把教程那次改铭再按回来
            data.AdvanceEditRevision();
            OnikiriNet.SyncLocalItem(Player, held);
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!healthGuardActive || OniPlayerDismember.SelfHurtResolving) {
                return;
            }
            int lifeFloor = GetTutorialLifeFloor();
            int allowedDamage = Player.statLife - lifeFloor;
            if (allowedDamage <= 0) {
                modifiers.Cancel();
                return;
            }
            modifiers.SetMaxDamage(allowedDamage);
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (!healthGuardActive) {
                return true;
            }
            playSound = false;
            genGore = false;
            SetPlayerLifeAtLeast(GetTutorialLifeFloor(), showEffect: false);
            return false;
        }

        private void InitializeTutorial(OnikiriTutorialEntry entry = OnikiriTutorialEntry.Auto) {
            initialized = true;
            Subscribe();

            if (entry == OnikiriTutorialEntry.ForceFull) {
                SetStep(OnikiriTutorialFlow.Step_HudIntro);
                return;
            }

            OnikiriGuideData guide = GuideData;
            bool introDone = entry == OnikiriTutorialEntry.ForcePractice
                || guide.Checkpoint >= OnikiriTutorialFlow.Checkpoint_ExplainDone;
            if (!introDone) {
                //没答复过 → 首次询问;通关过旧版本 → 补讲询问;其余接着上次被打断的卡讲
                SetStep(!guide.AskAnswered || IsRefresherAsk
                    ? OnikiriTutorialFlow.Step_Ask
                    : ResolveExplainStep());
                return;
            }

            resumeStep = ResolveResumeStep();
            SetStep(OnikiriTutorialFlow.Step_Prepare);
        }

        /// <summary>
        /// 本次询问是"旧版通关者的补讲",不是首次开场询问。
        /// 按版本记账,以后再加内容还能再问一次
        /// </summary>
        internal bool IsRefresherAsk {
            get {
                OnikiriGuideData guide = GuideData;
                return guide.CompletedVersion > 0
                    && guide.CompletedVersion < OnikiriTutorialLead.TutorialVersion
                    && guide.RefresherAskedVersion < OnikiriTutorialLead.TutorialVersion;
            }
        }

        /// <summary>接着被打断的那张卡的下一张讲。旧档的 1 正好是"讲完 HUD",语义天然对齐</summary>
        private int ResolveExplainStep()
            => Math.Clamp(GuideData.Checkpoint + 1,
                OnikiriTutorialFlow.Step_HudIntro, OnikiriTutorialFlow.Step_Domain);

        /// <summary>按存档的实操检查点算出续练落点;已练完则直接收尾</summary>
        private int ResolveResumeStep() {
            OnikiriPracticeCheckpoint checkpoint = (OnikiriPracticeCheckpoint)Math.Clamp(
                GuideData.PracticeCheckpoint, 0, (int)OnikiriPracticeCheckpoint.Closed);
            return checkpoint switch {
                OnikiriPracticeCheckpoint.None => OnikiriTutorialFlow.Step_OpenOmote,
                OnikiriPracticeCheckpoint.Omote => OnikiriTutorialFlow.Step_FlipUra,
                OnikiriPracticeCheckpoint.Ura => OnikiriTutorialFlow.Step_Dismember,
                OnikiriPracticeCheckpoint.Dismembered => OnikiriTutorialFlow.Step_CloseEye,
                _ => OnikiriTutorialFlow.Step_Done,
            };
        }

        private OnikiriGuideData GuideData
            => Player.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();

        private void Subscribe() {
            if (subscribed) {
                return;
            }
            OnikiriTutorialEvents.OnDomainCommandAccepted += HandleDomainCommandAccepted;
            OnikiriTutorialEvents.OnDomainPhaseSettled += HandleDomainPhaseSettled;
            OnikiriTutorialEvents.OnDismemberLanded += HandleDismemberLanded;
            subscribed = true;
        }

        private void Unsubscribe() {
            if (!subscribed) {
                return;
            }
            OnikiriTutorialEvents.OnDomainCommandAccepted -= HandleDomainCommandAccepted;
            OnikiriTutorialEvents.OnDomainPhaseSettled -= HandleDomainPhaseSettled;
            OnikiriTutorialEvents.OnDismemberLanded -= HandleDismemberLanded;
            subscribed = false;
        }

        private void SetStep(int step) {
            CurrentStep = step;
            StepTimer = 0;
            dismemberInputArmed = false;
            practicePrimed = false;
            if (step == OnikiriTutorialFlow.Step_Dismember) {
                sawTargetSplit = false;
            }
            Feedback = OnikiriTutorialFeedback.None;
            feedbackTimer = 0;

            switch (step) {
                case OnikiriTutorialFlow.Step_Mei:
                    EnsureHoldingOnikiri();
                    OniSigilUI.Instance?.Close();
                    OniRegisterUI.Instance?.Close();
                    OniTalismanHud.RememberLedger(OniLedgerView.Mei);
                    break;
                case OnikiriTutorialFlow.Step_Codex:
                    EnsureHoldingOnikiri();
                    codexOpenedThisStep = false;
                    break;
                case OnikiriTutorialFlow.Step_Sigil:
                    EnsureHoldingOnikiri();
                    sigilOpenedThisStep = false;
                    break;
                case OnikiriTutorialFlow.Step_HudIntro:
                    EnsureHoldingOnikiri();
                    break;
                case OnikiriTutorialFlow.Step_Domain:
                    //讲鬼眼要看得见鬼眼:三块屏全收
                    EnsureHoldingOnikiri();
                    CloseAllLedgers();
                    break;
                case OnikiriTutorialFlow.Step_Prepare:
                    CloseAllLedgers();
                    prepareReachedClosed = false;
                    ReleaseTutorialTarget();
                    break;
                case OnikiriTutorialFlow.Step_FlipUra:
                case OnikiriTutorialFlow.Step_Dismember:
                    EnsureTutorialTarget();
                    break;
                case OnikiriTutorialFlow.Step_Done:
                    FinishTutorial();
                    break;
            }
        }

        private static void CloseAllLedgers() {
            if (OniMeiCodexUI.Instance?.IsOpen == true) {
                OniMeiCodexUI.Instance.Close();
            }
            if (OniMeiUI.Instance?.IsOpen == true) {
                OniMeiUI.Instance.Close();
            }
            if (OniRegisterUI.Instance?.IsOpen == true) {
                OniRegisterUI.Instance.Close();
            }
            if (OniSigilUI.Instance?.IsOpen == true) {
                OniSigilUI.Instance.Close();
            }
        }

        private void AdvanceIfReady() {
            //无刀硬闸:依赖手持的步骤不推进、不开台
            if (NeedsOnikiriHold(CurrentStep) && !HasOnikiriAnywhere()) {
                return;
            }
            //讲解步的终极兜底:读不懂提示的玩家也不会永远停在同一张卡上
            if (OnikiriTutorialFlow.IsExplanatoryStep(CurrentStep)
                && StepTimer > OnikiriTutorialFlow.ExplainAutoAdvanceFrames) {
                AdvanceExplanatoryStep();
                return;
            }

            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_HudIntro:
                    if (StepTimer > 60 * 20) {
                        AdvanceExplanatoryStep();
                    }
                    break;
                case OnikiriTutorialFlow.Step_Codex:
                    TickCodexStep();
                    break;
                case OnikiriTutorialFlow.Step_Sigil:
                    TickSigilStep();
                    break;
                case OnikiriTutorialFlow.Step_Prepare:
                    TickPrepareStep();
                    break;
                case OnikiriTutorialFlow.Step_OpenOmote:
                    TickOpenStep();
                    break;
                case OnikiriTutorialFlow.Step_FlipUra:
                    TickFlipStep();
                    break;
                case OnikiriTutorialFlow.Step_Dismember:
                    EnsureStableUra();
                    break;
                case OnikiriTutorialFlow.Step_Backlash:
                    TickBacklashStep();
                    break;
                case OnikiriTutorialFlow.Step_CloseEye:
                    TickCloseStep();
                    break;
            }
        }

        private void TickCodexStep() {
            if (OniMeiCodexUI.Instance?.IsOpen ?? false) {
                codexOpenedThisStep = true;
            }
            else if (codexOpenedThisStep) {
                AdvanceExplanatoryStep();
            }
        }

        private void TickSigilStep() {
            if (OniSigilUI.Instance?.IsOpen ?? false) {
                sigilOpenedThisStep = true;
            }
            else if (sigilOpenedThisStep) {
                AdvanceExplanatoryStep();
            }
        }

        private void TickPrepareStep() {
            //复位超时:相位或黄昏赖着不走也不能把玩家钉死在准备卡上
            bool overdue = StepTimer > OnikiriTutorialFlow.PrepareTimeoutFrames;
            OniDomainPhase phase = Domain.Phase;
            if (!prepareReachedClosed) {
                if (phase == OniDomainPhase.Closed) {
                    prepareReachedClosed = true;
                }
                else if (phase is OniDomainPhase.Opening or OniDomainPhase.Omote or OniDomainPhase.Ura) {
                    TryInternalToggle();
                }
                if (!overdue) {
                    return;
                }
            }
            if (ToriiDusk.Visible && !overdue) {
                return;
            }

            if (resumeStep == OnikiriTutorialFlow.Step_OpenOmote || overdue) {
                //超时进后续步骤不预铺相位,各步的归位逻辑会自己接手
                EnterResumeStep();
            }
            else if (resumeStep == OnikiriTutorialFlow.Step_FlipUra && EnsureStableOmote()) {
                EnterResumeStep();
            }
            else if (resumeStep is OnikiriTutorialFlow.Step_Dismember or OnikiriTutorialFlow.Step_CloseEye
                && EnsureStableUra()) {
                EnterResumeStep();
            }
            else if (resumeStep == OnikiriTutorialFlow.Step_Done) {
                SetStep(OnikiriTutorialFlow.Step_Done);
            }
        }

        /// <summary>进入本次续练的落点;落点不成立就直接收尾,不把玩家留在准备卡上</summary>
        private void EnterResumeStep() {
            SetStep(resumeStep is OnikiriTutorialFlow.Step_OpenOmote
                or OnikiriTutorialFlow.Step_FlipUra
                or OnikiriTutorialFlow.Step_Dismember
                or OnikiriTutorialFlow.Step_CloseEye
                ? resumeStep
                : OnikiriTutorialFlow.Step_Done);
        }

        /// <summary>
        /// 开域步按结果收货:玩家用键位、鬼眼还是别的路子展开都算数。
        /// 只在"进步骤时域就开着"这一种情况下先收回来,让他亲手开一次;超时则按现状认账
        /// </summary>
        private void TickOpenStep() {
            if (Domain.Phase == OniDomainPhase.Closed) {
                practicePrimed = true;
                return;
            }
            if (!IsStableOmote && !IsStableUra) {
                return;
            }
            if (practicePrimed || StepTimer > OnikiriTutorialFlow.PracticePrimeGraceFrames) {
                CompleteOpenStep();
                return;
            }
            NormalizeClosed();
        }

        /// <summary>翻转步同样看结果:已经在里世界就过,绝不把玩家翻回表世界</summary>
        private void TickFlipStep() {
            EnsureTutorialTarget();
            if (IsStableUra) {
                CompleteFlipStep();
                return;
            }
            //只有域被整个收了才补开,否则静静等翻转仪式走完
            if (Domain.Phase == OniDomainPhase.Closed) {
                EnsureStableOmote();
            }
        }

        private void TickBacklashStep() {
            NPC target = TutorialTarget;
            bool splitVisible = target != null && OniDismember.IsDismembered(target.whoAmI);
            if (splitVisible) {
                sawTargetSplit = true;
            }
            bool locked = OniPlayerDismember.IsLocked(Player);
            if (locked) {
                sawSelfCutLock = true;
            }
            if (!sawSelfCutLock) {
                if (StepTimer > SelfCutStartTimeout) {
                    RestoreTutorialHealth();
                    ReleaseTutorialTarget();
                    SetStep(OnikiriTutorialFlow.Step_Dismember);
                    SetFeedback(OnikiriTutorialFeedback.Retry, 180);
                }
                return;
            }
            if (locked) {
                return;
            }

            RestoreTutorialHealth();
            healthGuardActive = true;
            if (target == null || !sawTargetSplit || splitVisible) {
                if (StepTimer > DismemberVisualTimeout) {
                    healthGuardActive = false;
                    ReleaseTutorialTarget();
                    SetStep(OnikiriTutorialFlow.Step_Dismember);
                    SetFeedback(OnikiriTutorialFeedback.Retry, 180);
                }
                return;
            }

            WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Dismembered);
            healthGuardActive = false;
            SetStep(OnikiriTutorialFlow.Step_CloseEye);
        }

        /// <summary>收域步看结果:域阖上就算过。进步骤时若已阖,先补开一次再收,免得白送</summary>
        private void TickCloseStep() {
            if (Domain.Phase != OniDomainPhase.Closed) {
                practicePrimed = true;
                return;
            }
            if (practicePrimed || StepTimer > OnikiriTutorialFlow.PracticePrimeGraceFrames) {
                WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Closed);
                SetStep(OnikiriTutorialFlow.Step_Done);
                return;
            }
            EnsureStableUra();
        }

        private void AcceptTutorial() {
            if (CurrentStep != OnikiriTutorialFlow.Step_Ask) {
                return;
            }
            bool refresher = IsRefresherAsk;
            OnikiriGuideData guide = GuideData;
            guide.AskAnswered = true;
            guide.Declined = false;
            guide.RefresherAskedVersion = OnikiriTutorialLead.TutorialVersion;
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.25f, Volume = 0.45f });
            //补讲只接上旧档读到的地方,不把已经会的再讲一遍
            SetStep(refresher ? ResolveExplainStep() : OnikiriTutorialFlow.Step_HudIntro);
        }

        private void DeclineTutorial() {
            if (CurrentStep != OnikiriTutorialFlow.Step_Ask) {
                return;
            }
            bool refresher = IsRefresherAsk;
            OnikiriGuideData guide = GuideData;
            guide.AskAnswered = true;
            guide.RefresherAskedVersion = OnikiriTutorialLead.TutorialVersion;
            if (refresher) {
                //旧档回绝补讲 = 就当这一版也学过了,别再拦路;想看仍可用符
                guide.CompletedVersion = OnikiriTutorialLead.TutorialVersion;
            }
            else {
                guide.Declined = true;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.7f, Volume = 0.32f });
            OniKeikoRune.GrantTo(Player);
            //Forced 会盖过婉拒;不一并清掉的话强制会话下一帧就把卡接回来了
            ResetAllRuntime();
        }

        private void AdvanceExplanatoryStep() {
            if (NeedsOnikiriHold(CurrentStep) && !HasOnikiriAnywhere()) {
                NotifyNeedBlade();
                return;
            }
            //每读完一张就落点,下次被打断从这里的下一张接着讲
            GuideData.Checkpoint = Math.Max(GuideData.Checkpoint, CurrentStep);
            if (CurrentStep == OnikiriTutorialFlow.Step_Domain) {
                //补讲版讲解段走完后不再从头练一遍:落点仍按存档的实操进度算
                resumeStep = ResolveResumeStep();
                SetStep(OnikiriTutorialFlow.Step_Prepare);
                return;
            }
            if (CurrentStep >= OnikiriTutorialFlow.Step_HudIntro
                && CurrentStep < OnikiriTutorialFlow.Step_Domain) {
                SetStep(CurrentStep + 1);
            }
        }

        private void PerformAssistedAction() {
            if (!CanAcceptTutorialInput()) {
                SetFeedback(OnikiriTutorialFeedback.Busy, 120);
                return;
            }

            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_OpenOmote:
                    if (Domain.Phase != OniDomainPhase.Closed) {
                        NormalizeClosed();
                        SetFeedback(OnikiriTutorialFeedback.Waiting, 90);
                        return;
                    }
                    if (!OniDomain.TryToggle(Player, out bool openBusy,
                        OnikiriDomainCommandSource.TutorialAssist)) {
                        SetFeedback(openBusy ? OnikiriTutorialFeedback.Busy : OnikiriTutorialFeedback.Retry, 120);
                    }
                    break;
                case OnikiriTutorialFlow.Step_FlipUra:
                    if (!IsStableOmote) {
                        EnsureStableOmote();
                        SetFeedback(OnikiriTutorialFeedback.Waiting, 90);
                        return;
                    }
                    if (!OniDomain.TryFlip(Player, out bool flipBusy,
                        OnikiriDomainCommandSource.TutorialAssist)) {
                        SetFeedback(flipBusy ? OnikiriTutorialFeedback.Busy : OnikiriTutorialFeedback.Retry, 120);
                    }
                    break;
                case OnikiriTutorialFlow.Step_Dismember:
                    NPC target = TutorialTarget;
                    if (target == null) {
                        EnsureTutorialTarget(force: true);
                        SetFeedback(OnikiriTutorialFeedback.Waiting, 120);
                        return;
                    }
                    if (!IsStableUra) {
                        EnsureStableUra();
                        SetFeedback(OnikiriTutorialFeedback.Waiting, 90);
                        return;
                    }
                    if (!Player.GetModPlayer<OnikiriPlayer>().TryTutorialDismember(target)) {
                        SetFeedback(OnikiriTutorialFeedback.Retry, 150);
                    }
                    break;
                case OnikiriTutorialFlow.Step_CloseEye:
                    if (!IsStableUra) {
                        EnsureStableUra();
                        SetFeedback(OnikiriTutorialFeedback.Waiting, 90);
                        return;
                    }
                    if (!OniDomain.TryToggle(Player, out bool closeBusy,
                        OnikiriDomainCommandSource.TutorialAssist)) {
                        SetFeedback(closeBusy ? OnikiriTutorialFeedback.Busy : OnikiriTutorialFeedback.Retry, 120);
                    }
                    break;
            }
        }

        private void RetryCurrentStep() {
            //StepTimer 不清零:它是「替我演示」「跳过本步」的解锁计时,
            //重试若把它推回去,反复失手的玩家反而永远够不到兜底按钮
            Feedback = OnikiriTutorialFeedback.None;
            feedbackTimer = 0;
            if (CurrentStep == OnikiriTutorialFlow.Step_Dismember) {
                dismemberInputArmed = false;
                EnsureTutorialTarget(force: true);
            }
        }

        private void HandleUnboundFallbackInput() {
            bool middleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
            bool middlePressed = middleDown && !previousMiddleDown;
            previousMiddleDown = middleDown;
            if (!CanAcceptTutorialInput()) {
                return;
            }

            if (CurrentStep == OnikiriTutorialFlow.Step_OpenOmote
                && CWRKeySystem.IsKeybindUnbound(CWRKeySystem.Legend_Domain)
                && IsHoldingOnikiri()
                && Main.keyState.IsKeyDown(Keys.Q) && Main.oldKeyState.IsKeyUp(Keys.Q)) {
                if (!OniDomain.TryToggle(Player, out bool busy,
                    OnikiriDomainCommandSource.TutorialFallback) && busy) {
                    SetFeedback(OnikiriTutorialFeedback.Busy, 120);
                }
            }

            if (CurrentStep == OnikiriTutorialFlow.Step_FlipUra
                && CWRKeySystem.IsKeybindUnbound(CWRKeySystem.Onikiri_DomainFlip)
                && !Player.mouseInterface
                && middlePressed) {
                if (!OniDomain.TryFlip(Player, out bool busy,
                    OnikiriDomainCommandSource.TutorialFallback) && busy) {
                    SetFeedback(OnikiriTutorialFeedback.Busy, 120);
                }
            }
        }

        private bool CanAcceptTutorialInput()
            => !Player.dead && !HackTime.Active && !OniPlayerDismember.IsLocked(Player)
                && !(OniRegisterUI.Instance?.IsOpen ?? false)
                && !Main.editSign && !Main.editChest && !Main.drawingPlayerChat;

        private bool IsHoldingOnikiri() {
            Item held = Player.GetItem();
            return held != null && !held.IsAir && held.type == ModContent.ItemType<OnikiriItem>();
        }

        /// <summary>询问之后的步骤都需要鬼切在手(HUD/台账/域)</summary>
        private static bool NeedsOnikiriHold(int step)
            => step is >= OnikiriTutorialFlow.Step_HudIntro and < OnikiriTutorialFlow.Step_Done;

        /// <summary>行囊或鼠标上是否还有鬼切</summary>
        private bool HasOnikiriAnywhere() {
            int type = ModContent.ItemType<OnikiriItem>();
            if (Player.HasItem(type)) {
                return true;
            }
            Item mouse = Main.mouseItem;
            return mouse != null && !mouse.IsAir && mouse.type == type;
        }

        /// <summary>每帧:有刀则锁持;无刀则硬闸关台并提示</summary>
        private void MaintainOnikiriHoldLock() {
            if (!NeedsOnikiriHold(CurrentStep)) {
                lockedOnikiriSlot = -1;
                return;
            }
            if (!HasOnikiriAnywhere()) {
                lockedOnikiriSlot = -1;
                CloseAllLedgers();
                NotifyNeedBlade();
                return;
            }
            if (Feedback == OnikiriTutorialFeedback.NeedBlade) {
                Feedback = OnikiriTutorialFeedback.None;
                feedbackTimer = 0;
            }
            EnsureHoldingOnikiri();
        }

        /// <summary>
        /// 无刀硬闸的出口:刀进了箱子又不打算取回来时,教习自己收摊,
        /// 而不是把一张永远提示"没有鬼切"的卡片钉在屏幕上。返回真表示本帧已经收摊
        /// </summary>
        private bool TickNeedBladeAbort() {
            if (!NeedsOnikiriHold(CurrentStep) || HasOnikiriAnywhere()) {
                needBladeTimer = 0;
                return false;
            }
            if (++needBladeTimer < OnikiriTutorialFlow.NeedBladeAbortFrames) {
                return false;
            }
            needBladeTimer = 0;
            SuspendForMissingBlade();
            return true;
        }

        /// <summary>开台账前先确认有刀并选中;失败则提示且不开</summary>
        private bool TryBeginLedgerOpen(Action open) {
            if (!HasOnikiriAnywhere() || !EnsureHoldingOnikiri()) {
                NotifyNeedBlade();
                return false;
            }
            open?.Invoke();
            return true;
        }

        private void NotifyNeedBlade() {
            bool first = Feedback != OnikiriTutorialFeedback.NeedBlade;
            SetFeedback(OnikiriTutorialFeedback.NeedBlade, 180);
            if (first) {
                VaultUtils.Text(OnikiriTutorialLead.NeedBladeHold.Value, OnikiriUITheme.Seal);
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.4f, Volume = 0.35f });
            }
        }

        /// <summary>
        /// 确保鬼切在快捷栏并被选中。优先已选手持→快捷栏→鼠标→背包(换入空快捷栏或当前槽)。
        /// 背包里没有鬼切时放弃(无法凭空造刀)
        /// </summary>
        private bool EnsureHoldingOnikiri() {
            int type = ModContent.ItemType<OnikiriItem>();
            if (IsHoldingOnikiri()) {
                ApplyOnikiriLock(Player.selectedItem);
                return true;
            }

            for (int i = 0; i < 10; i++) {
                Item hot = Player.inventory[i];
                if (hot != null && !hot.IsAir && hot.type == type) {
                    ApplyOnikiriLock(i);
                    return true;
                }
            }

            if (Main.mouseItem != null && !Main.mouseItem.IsAir && Main.mouseItem.type == type) {
                int dest = FindEmptyHotbarSlot();
                if (dest < 0) {
                    dest = Math.Clamp(Player.selectedItem, 0, 9);
                }
                Item swap = Player.inventory[dest];
                Player.inventory[dest] = Main.mouseItem;
                Main.mouseItem = swap ?? new Item();
                ApplyOnikiriLock(dest);
                return true;
            }

            for (int i = 10; i < Player.inventory.Length; i++) {
                Item bag = Player.inventory[i];
                if (bag == null || bag.IsAir || bag.type != type) {
                    continue;
                }
                int dest = FindEmptyHotbarSlot();
                if (dest < 0) {
                    dest = Math.Clamp(Player.selectedItem, 0, 9);
                }
                Utils.Swap(ref Player.inventory[dest], ref Player.inventory[i]);
                ApplyOnikiriLock(dest);
                return true;
            }

            lockedOnikiriSlot = -1;
            return false;
        }

        private int FindEmptyHotbarSlot() {
            for (int i = 0; i < 10; i++) {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir) {
                    return i;
                }
            }
            return -1;
        }

        private void ApplyOnikiriLock(int slot) {
            slot = Math.Clamp(slot, 0, 9);
            lockedOnikiriSlot = slot;
            Player.selectedItem = slot;
            Player.HotbarOffset = 0;
            Player.changeItem = -1;
            if (WorldFreezeSystem.IsActive) {
                Player.GetModPlayer<WorldFreezePlayer>().RetargetFrozenHotbar(slot);
            }
        }

        /// <summary>
        /// 命令受理只用来切"正在等待"的反馈,不判对错也不认来源:
        /// 键位、鬼眼、教程代按都是同一件事,推进与否一律由各步的结果判定说了算
        /// </summary>
        private void HandleDomainCommandAccepted(Player player, OnikiriDomainCommandKind kind,
            OnikiriDomainCommandSource source) {
            if (player != Player || suppressCommandEvent) {
                return;
            }

            OniDomainPlayer domain = Domain;
            bool onTrack = CurrentStep switch {
                OnikiriTutorialFlow.Step_OpenOmote => kind == OnikiriDomainCommandKind.Toggle
                    && domain.Phase == OniDomainPhase.Opening,
                OnikiriTutorialFlow.Step_FlipUra => kind == OnikiriDomainCommandKind.Flip
                    && domain.Phase == OniDomainPhase.Flipping && domain.FlipToUra,
                OnikiriTutorialFlow.Step_CloseEye => kind == OnikiriDomainCommandKind.Toggle
                    && domain.Phase == OniDomainPhase.Closing,
                _ => false,
            };
            if (onTrack) {
                SetFeedback(OnikiriTutorialFeedback.Waiting, 150);
            }
        }

        private void HandleDomainPhaseSettled(Player player, OniDomainPhase phase) {
            //落定的推进交给逐帧结果判定,这里只收掉已经过期的"正在等待"
            if (player == Player && Feedback == OnikiriTutorialFeedback.Waiting) {
                Feedback = OnikiriTutorialFeedback.None;
                feedbackTimer = 0;
            }
        }

        private void HandleDismemberLanded(Player player, NPC target) {
            if (player != Player || CurrentStep != OnikiriTutorialFlow.Step_Dismember
                || target == null
                || !OnikiriTutorialTargetGlobal.IsTutorialTarget(target, out int owner, out int session)
                || owner != Player.whoAmI || session != targetSession) {
                return;
            }

            lifeBeforeDemonstration = Player.statLife;
            int selfDamage = Math.Max((int)(Player.statLifeMax2 * OniPlayerDismember.SelfHurtFraction), 1);
            guardedLife = Math.Min(Player.statLifeMax2, Math.Max(lifeBeforeDemonstration, selfDamage + 1));
            SetPlayerLifeAtLeast(guardedLife, showEffect: false);
            healthGuardActive = true;
            sawSelfCutLock = false;
            sawTargetSplit = false;
            healthRecovered = false;
            SetStep(OnikiriTutorialFlow.Step_Backlash);
        }

        private void CompleteOpenStep() {
            if (CurrentStep != OnikiriTutorialFlow.Step_OpenOmote) {
                return;
            }
            WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Omote);
            SetStep(OnikiriTutorialFlow.Step_FlipUra);
        }

        private void CompleteFlipStep() {
            if (CurrentStep != OnikiriTutorialFlow.Step_FlipUra) {
                return;
            }
            WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Ura);
            SetStep(OnikiriTutorialFlow.Step_Dismember);
        }

        private void WritePracticeCheckpoint(OnikiriPracticeCheckpoint checkpoint) {
            GuideData.PracticeCheckpoint = Math.Max(GuideData.PracticeCheckpoint, (int)checkpoint);
        }

        private OniDomainPlayer Domain => Player.GetModPlayer<OniDomainPlayer>();
        private bool IsStableOmote => Domain.Phase == OniDomainPhase.Omote && !Domain.WorldIsUra;
        private bool IsStableUra => Domain.Phase == OniDomainPhase.Ura && Domain.WorldIsUra;

        private void NormalizeClosed() {
            OniDomainPhase phase = Domain.Phase;
            if (phase is OniDomainPhase.Opening or OniDomainPhase.Omote or OniDomainPhase.Ura) {
                TryInternalToggle();
            }
        }

        private bool EnsureStableOmote() {
            if (IsStableOmote) {
                return true;
            }
            if (Domain.Phase == OniDomainPhase.Closed) {
                TryInternalToggle();
            }
            else if (IsStableUra) {
                TryInternalFlip();
            }
            return false;
        }

        private bool EnsureStableUra() {
            if (IsStableUra) {
                return true;
            }
            if (Domain.Phase == OniDomainPhase.Closed) {
                TryInternalToggle();
            }
            else if (IsStableOmote) {
                TryInternalFlip();
            }
            return false;
        }

        private void TryInternalToggle() {
            suppressCommandEvent = true;
            try {
                OniDomain.TryToggle(Player, out _, OnikiriDomainCommandSource.TutorialAssist);
            } finally {
                suppressCommandEvent = false;
            }
        }

        private void TryInternalFlip() {
            suppressCommandEvent = true;
            try {
                OniDomain.TryFlip(Player, out _, OnikiriDomainCommandSource.TutorialAssist);
            } finally {
                suppressCommandEvent = false;
            }
        }

        private void MaintainTutorialTarget() {
            if (CurrentStep < OnikiriTutorialFlow.Step_FlipUra
                || CurrentStep > OnikiriTutorialFlow.Step_Backlash
                || CurrentStep == OnikiriTutorialFlow.Step_Backlash && sawSelfCutLock) {
                return;
            }
            if (TutorialTarget != null) {
                targetRetryTimer = 0;
                return;
            }
            if (++targetRetryTimer >= TargetRetryFrames) {
                targetRetryTimer = 0;
                EnsureTutorialTarget(force: true);
            }
        }

        private void EnsureTutorialTarget(bool force = false) {
            if (targetSession <= 0) {
                targetSession = Main.rand.Next(1, int.MaxValue);
                force = true;
            }
            if (force || TutorialTarget == null && targetRetryTimer == 0) {
                OnikiriTutorialNet.RequestEnsureTarget(targetSession);
            }
        }

        private void ReleaseTutorialTarget() {
            if (targetSession > 0) {
                OnikiriTutorialNet.RequestReleaseTarget(targetSession);
            }
            targetSession = 0;
            targetRetryTimer = 0;
        }

        private void SetFeedback(OnikiriTutorialFeedback feedback, int frames) {
            Feedback = feedback;
            feedbackTimer = Math.Max(frames, 1);
        }

        private void SetPlayerLifeAtLeast(int target, bool showEffect) {
            int clamped = Math.Clamp(target, 1, Player.statLifeMax2);
            if (Player.statLife >= clamped) {
                return;
            }
            int heal = clamped - Player.statLife;
            Player.statLife = clamped;
            if (showEffect) {
                Player.HealEffect(heal, true);
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
            }
        }

        private int GetTutorialLifeFloor() {
            bool backlashStarted = sawSelfCutLock || detachedSafetySawLock
                || OniPlayerDismember.IsLocked(Player);
            if (backlashStarted) {
                return 1;
            }
            int selfDamage = Math.Max((int)(Player.statLifeMax2
                * OniPlayerDismember.SelfHurtFraction), 1);
            return Math.Min(Player.statLifeMax2, selfDamage + 1);
        }

        private void RestoreTutorialHealth() {
            healthGuardActive = false;
            if (healthRecovered || guardedLife <= 0) {
                return;
            }
            healthRecovered = true;
            SetPlayerLifeAtLeast(guardedLife, showEffect: sawSelfCutLock);
            guardedLife = 0;
            lifeBeforeDemonstration = 0;
        }

        private void FinishTutorial() {
            RestoreMeiSnapshotIfNeeded();
            RestoreTutorialHealth();
            ReleaseTutorialTarget();
            Unsubscribe();
            initialized = false;
            OnikiriTutorialLead.MarkComplete();
        }

        internal sealed class OniMeiSnapshot
        {
            internal readonly Inscriptions.OniMeiStore Store = new();
            /// <summary>留档时那把刀的身份,回滚前据此确认没换刀</summary>
            internal readonly long InstanceId;

            internal OniMeiSnapshot(OnikiriData source) {
                Store.CopyFrom(source.Mei);
                InstanceId = source.InstanceId;
            }
        }
    }
}
