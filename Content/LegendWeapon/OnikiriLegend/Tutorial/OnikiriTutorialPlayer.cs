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
        private bool registerOpenedThisStep;
        private bool expectedCommandAccepted;
        private bool suppressCommandEvent;
        private bool prepareReachedClosed;
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

            StepTimer++;
            if (CurrentStep == OnikiriTutorialFlow.Step_Dismember && !Main.mouseLeft) {
                dismemberInputArmed = true;
            }
            if (feedbackTimer > 0 && --feedbackTimer == 0) {
                Feedback = OnikiriTutorialFeedback.None;
            }

            //讲解与实操都依赖鬼切 HUD/台账:每帧确保手持并锁槽,避免切刀后点鬼簿闪关
            MaintainOnikiriHoldLock();

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
                    AdvanceExplanatoryStep();
                    break;
                case OnikiriTutorialFlow.Step_Mei:
                    if (!(OniMeiUI.Instance?.IsOpen ?? false)) {
                        EnsureHoldingOnikiri();
                        OniMeiUI.Instance?.Open();
                    }
                    break;
                case OnikiriTutorialFlow.Step_Register:
                    if (OniRegisterUI.Instance?.IsOpen ?? false) {
                        OniRegisterUI.Instance.Close();
                        AdvanceExplanatoryStep();
                    }
                    else if (!(OniMeiUI.Instance?.IsOpen ?? false)) {
                        EnsureHoldingOnikiri();
                        OniMeiUI.Instance?.Open();
                    }
                    break;
                case OnikiriTutorialFlow.Step_OpenOmote:
                case OnikiriTutorialFlow.Step_FlipUra:
                case OnikiriTutorialFlow.Step_Dismember:
                case OnikiriTutorialFlow.Step_CloseEye:
                    if (Feedback == OnikiriTutorialFeedback.Retry) {
                        RetryCurrentStep();
                    }
                    break;
            }
        }

        internal void HandleSecondaryAction() {
            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_Ask:
                    DeclineTutorial();
                    break;
                case OnikiriTutorialFlow.Step_Mei:
                    AdvanceExplanatoryStep();
                    break;
                case OnikiriTutorialFlow.Step_Register:
                    if (!(OniRegisterUI.Instance?.IsOpen ?? false)) {
                        EnsureHoldingOnikiri();
                        OniRegisterUI.Instance?.Open();
                    }
                    break;
                case OnikiriTutorialFlow.Step_OpenOmote:
                case OnikiriTutorialFlow.Step_FlipUra:
                case OnikiriTutorialFlow.Step_Dismember:
                case OnikiriTutorialFlow.Step_CloseEye:
                    PerformAssistedAction();
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

        /// <summary>稽古符启动:越过排队前提,从讲解第一步重走</summary>
        internal void ForceStartFull() => ForceStart(OnikiriTutorialEntry.ForceFull);

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
            expectedCommandAccepted = false;
            prepareReachedClosed = false;
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

        internal void BeginMeiTransaction(Inscriptions.OniMeiStore current)
            => meiSnapshot = new OniMeiSnapshot(current);

        internal void RestoreMeiSnapshotIfNeeded() {
            if (meiSnapshot == null) {
                return;
            }
            OnikiriData data = OnikiriData.TryGet(Player?.GetItem());
            if (data != null) {
                data.Mei.CopyFrom(meiSnapshot.Store);
                OnikiriNet.SyncLocalItem(Player, Player.GetItem());
            }
            meiSnapshot = null;
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
                || guide.Checkpoint >= OnikiriTutorialFlow.Checkpoint_Hud
                || guide.CompletedVersion >= 3;
            if (!introDone) {
                SetStep(guide.AskAnswered
                    ? OnikiriTutorialFlow.Step_HudIntro
                    : OnikiriTutorialFlow.Step_Ask);
                return;
            }

            OnikiriPracticeCheckpoint checkpoint = (OnikiriPracticeCheckpoint)Math.Clamp(
                guide.PracticeCheckpoint, 0, (int)OnikiriPracticeCheckpoint.Closed);
            resumeStep = checkpoint switch {
                OnikiriPracticeCheckpoint.None => OnikiriTutorialFlow.Step_OpenOmote,
                OnikiriPracticeCheckpoint.Omote => OnikiriTutorialFlow.Step_FlipUra,
                OnikiriPracticeCheckpoint.Ura => OnikiriTutorialFlow.Step_Dismember,
                OnikiriPracticeCheckpoint.Dismembered => OnikiriTutorialFlow.Step_CloseEye,
                _ => OnikiriTutorialFlow.Step_Done,
            };
            SetStep(OnikiriTutorialFlow.Step_Prepare);
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
            expectedCommandAccepted = false;
            dismemberInputArmed = false;
            if (step == OnikiriTutorialFlow.Step_Dismember) {
                sawTargetSplit = false;
            }
            Feedback = OnikiriTutorialFeedback.None;
            feedbackTimer = 0;

            switch (step) {
                case OnikiriTutorialFlow.Step_Mei:
                    EnsureHoldingOnikiri();
                    OniRegisterUI.Instance?.Close();
                    OniTalismanHud.RememberLedger(OniLedgerView.Mei);
                    break;
                case OnikiriTutorialFlow.Step_Register:
                    EnsureHoldingOnikiri();
                    registerOpenedThisStep = false;
                    break;
                case OnikiriTutorialFlow.Step_HudIntro:
                    EnsureHoldingOnikiri();
                    break;
                case OnikiriTutorialFlow.Step_Prepare:
                    OniMeiUI.Instance?.Close();
                    OniRegisterUI.Instance?.Close();
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

        private void AdvanceIfReady() {
            switch (CurrentStep) {
                case OnikiriTutorialFlow.Step_HudIntro:
                    if (StepTimer > 60 * 20) {
                        AdvanceExplanatoryStep();
                    }
                    break;
                case OnikiriTutorialFlow.Step_Mei:
                    if (OniMeiUI.Instance?.IsOpen ?? false) {
                        AdvanceExplanatoryStep();
                    }
                    break;
                case OnikiriTutorialFlow.Step_Register:
                    TickRegisterStep();
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

        private void TickRegisterStep() {
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
                registerOpenedThisStep = true;
            }
            else if (registerOpenedThisStep) {
                AdvanceExplanatoryStep();
            }
        }

        private void TickPrepareStep() {
            OniDomainPhase phase = Domain.Phase;
            if (!prepareReachedClosed) {
                if (phase == OniDomainPhase.Closed) {
                    prepareReachedClosed = true;
                }
                else if (phase is OniDomainPhase.Opening or OniDomainPhase.Omote or OniDomainPhase.Ura) {
                    TryInternalToggle();
                }
                return;
            }
            if (ToriiDusk.Visible) {
                return;
            }

            if (resumeStep == OnikiriTutorialFlow.Step_Done) {
                SetStep(OnikiriTutorialFlow.Step_Done);
            }
            else if (resumeStep == OnikiriTutorialFlow.Step_OpenOmote) {
                SetStep(resumeStep);
            }
            else if (resumeStep == OnikiriTutorialFlow.Step_FlipUra && EnsureStableOmote()) {
                SetStep(resumeStep);
            }
            else if (resumeStep is OnikiriTutorialFlow.Step_Dismember or OnikiriTutorialFlow.Step_CloseEye
                && EnsureStableUra()) {
                SetStep(resumeStep);
            }
        }

        private void TickOpenStep() {
            if (expectedCommandAccepted) {
                if (IsStableOmote) {
                    CompleteOpenStep();
                }
                else if (Domain.Phase == OniDomainPhase.Closed) {
                    expectedCommandAccepted = false;
                }
                return;
            }
            if (Domain.Phase != OniDomainPhase.Closed) {
                NormalizeClosed();
            }
        }

        private void TickFlipStep() {
            EnsureTutorialTarget();
            if (expectedCommandAccepted) {
                if (IsStableUra) {
                    CompleteFlipStep();
                }
                else if (IsStableOmote) {
                    expectedCommandAccepted = false;
                }
                return;
            }
            EnsureStableOmote();
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

        private void TickCloseStep() {
            if (expectedCommandAccepted) {
                if (Domain.Phase == OniDomainPhase.Closed) {
                    WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Closed);
                    SetStep(OnikiriTutorialFlow.Step_Done);
                }
                else if (Domain.Phase != OniDomainPhase.Closing) {
                    expectedCommandAccepted = false;
                    SetFeedback(OnikiriTutorialFeedback.Retry, 150);
                }
                return;
            }
            EnsureStableUra();
        }

        private void AcceptTutorial() {
            if (CurrentStep != OnikiriTutorialFlow.Step_Ask) {
                return;
            }
            OnikiriGuideData guide = GuideData;
            guide.AskAnswered = true;
            guide.Declined = false;
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.25f, Volume = 0.45f });
            SetStep(OnikiriTutorialFlow.Step_HudIntro);
        }

        private void DeclineTutorial() {
            if (CurrentStep != OnikiriTutorialFlow.Step_Ask) {
                return;
            }
            OnikiriGuideData guide = GuideData;
            guide.AskAnswered = true;
            guide.Declined = true;
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.7f, Volume = 0.32f });
            OniKeikoRune.GrantTo(Player);
            Suspend(releaseTarget: true);
        }

        private void AdvanceExplanatoryStep() {
            if (CurrentStep == OnikiriTutorialFlow.Step_Register) {
                GuideData.Checkpoint = Math.Max(GuideData.Checkpoint, OnikiriTutorialFlow.Checkpoint_Hud);
                SetStep(OnikiriTutorialFlow.Step_Prepare);
                resumeStep = OnikiriTutorialFlow.Step_OpenOmote;
                return;
            }
            if (CurrentStep >= OnikiriTutorialFlow.Step_HudIntro
                && CurrentStep < OnikiriTutorialFlow.Step_Register) {
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
            StepTimer = 0;
            expectedCommandAccepted = false;
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

        /// <summary>每帧:找到鬼切并锁在快捷栏,同步世界冻结的手持快照</summary>
        private void MaintainOnikiriHoldLock() {
            if (!NeedsOnikiriHold(CurrentStep)) {
                lockedOnikiriSlot = -1;
                return;
            }
            EnsureHoldingOnikiri();
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

        private void HandleDomainCommandAccepted(Player player, OnikiriDomainCommandKind kind,
            OnikiriDomainCommandSource source) {
            if (player != Player || suppressCommandEvent) {
                return;
            }

            OniDomainPlayer domain = Domain;
            if (CurrentStep == OnikiriTutorialFlow.Step_OpenOmote
                && kind == OnikiriDomainCommandKind.Toggle
                && source is OnikiriDomainCommandSource.Keybind
                    or OnikiriDomainCommandSource.TutorialFallback
                    or OnikiriDomainCommandSource.TutorialAssist
                && domain.Phase == OniDomainPhase.Opening) {
                expectedCommandAccepted = true;
                SetFeedback(OnikiriTutorialFeedback.Waiting, 90);
                return;
            }
            if (CurrentStep == OnikiriTutorialFlow.Step_FlipUra
                && kind == OnikiriDomainCommandKind.Flip
                && source is OnikiriDomainCommandSource.Keybind
                    or OnikiriDomainCommandSource.TutorialFallback
                    or OnikiriDomainCommandSource.TutorialAssist
                && domain.Phase == OniDomainPhase.Flipping && domain.FlipToUra) {
                expectedCommandAccepted = true;
                SetFeedback(OnikiriTutorialFeedback.Waiting, 150);
                return;
            }
            if (CurrentStep == OnikiriTutorialFlow.Step_CloseEye
                && kind == OnikiriDomainCommandKind.Toggle
                && source is OnikiriDomainCommandSource.HudLeft
                    or OnikiriDomainCommandSource.TutorialAssist
                && domain.Phase == OniDomainPhase.Closing && domain.WorldIsUra) {
                expectedCommandAccepted = true;
                SetFeedback(OnikiriTutorialFeedback.Waiting, 100);
                return;
            }
            if (CurrentStep is OnikiriTutorialFlow.Step_OpenOmote
                or OnikiriTutorialFlow.Step_FlipUra
                or OnikiriTutorialFlow.Step_Dismember
                or OnikiriTutorialFlow.Step_CloseEye) {
                expectedCommandAccepted = false;
                SetFeedback(OnikiriTutorialFeedback.Retry, 240);
            }
        }

        private void HandleDomainPhaseSettled(Player player, OniDomainPhase phase) {
            if (player != Player) {
                return;
            }
            if (CurrentStep == OnikiriTutorialFlow.Step_OpenOmote
                && expectedCommandAccepted && phase == OniDomainPhase.Omote) {
                CompleteOpenStep();
            }
            else if (CurrentStep == OnikiriTutorialFlow.Step_FlipUra
                && expectedCommandAccepted && phase == OniDomainPhase.Ura) {
                CompleteFlipStep();
            }
            else if (CurrentStep == OnikiriTutorialFlow.Step_CloseEye
                && expectedCommandAccepted && phase == OniDomainPhase.Closed) {
                WritePracticeCheckpoint(OnikiriPracticeCheckpoint.Closed);
                SetStep(OnikiriTutorialFlow.Step_Done);
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

            internal OniMeiSnapshot(Inscriptions.OniMeiStore source)
                => Store.CopyFrom(source);
        }
    }
}
