using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 鬼切教程步骤状态机。<br/>
    /// 步骤：[0]HUD → [1]改铭台 → [2]点鬼簿 → [3]鬼域之眼 → [4]结束。<br/>
    /// 不演示练习鬼影、肢解、五连/疾走/处决等战斗跟做环节。
    /// </summary>
    internal static class OnikiriTutorialFlow
    {
        internal const int Step_HudIntro = 0;
        internal const int Step_Mei = 1;
        internal const int Step_Register = 2;
        internal const int Step_Domain = 3;
        internal const int Step_Done = 4;

        /// <summary>HUD/簿/台段完成后；重进从鬼域步恢复</summary>
        internal const int Checkpoint_Hud = 1;

        private static int currentStep = -1;
        private static int stepTimer;
        private static bool initialized;
        private static OniMeiSnapshot meiSnapshot;
        private static bool registerOpenedThisStep;

        internal static int CurrentStep => currentStep;
        internal static int StepTimer => stepTimer;
        internal static bool IsRunning => currentStep >= 0 && currentStep < Step_Done;
        internal static OniMeiSnapshot PendingMeiRestore => meiSnapshot;

        internal static void RequestAdvance()
        {
            if (currentStep < 0 || currentStep >= Step_Done) return;
            AdvanceStep();
        }

        internal static void Reset()
        {
            Unsubscribe();
            currentStep = -1;
            stepTimer = 0;
            initialized = false;
            registerOpenedThisStep = false;
            meiSnapshot = null;
            OnikiriTutorialEvents.ClearAll();
        }

        internal static void ResetIfHolderLost()
        {
            if (initialized && currentStep >= 0 && currentStep < Step_Done)
            {
                RestoreMeiSnapshotIfNeeded();
                Unsubscribe();
                initialized = false;
            }
        }

        internal static void Tick(GameTime _)
        {
            if (!initialized) Initialize();
            if (currentStep < 0 || currentStep >= Step_Done) return;

            stepTimer++;
            AdvanceIfReady();
        }

        private static void Initialize()
        {
            initialized = true;
            Subscribe();

            var guide = Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            //检查点≥1（含旧档战斗/鬼影段残留）从鬼域认知续
            currentStep = guide.Checkpoint >= Checkpoint_Hud ? Step_Domain : Step_HudIntro;
            stepTimer = 0;
            EnterStep(currentStep);
        }

        private static void Subscribe()
        {
            OnikiriTutorialEvents.OnDomainPhaseSettled += HandleDomainPhaseSettled;
        }

        private static void Unsubscribe()
        {
            OnikiriTutorialEvents.OnDomainPhaseSettled -= HandleDomainPhaseSettled;
        }

        private static void EnterStep(int step)
        {
            stepTimer = 0;

            switch (step)
            {
                case Step_Mei:
                    OniRegisterUI.Instance?.Close();
                    OniTalismanHud.RememberLedger(OniLedgerView.Mei);
                    break;

                case Step_Register:
                    registerOpenedThisStep = false;
                    break;

                case Step_Domain:
                    OniMeiUI.Instance?.Close();
                    OniRegisterUI.Instance?.Close();
                    break;

                case Step_Done:
                    FinishTutorial();
                    break;
            }
        }

        private static void HandleDomainPhaseSettled(OniDomainPhase phase)
        {
            if (currentStep == Step_Domain
                && phase is OniDomainPhase.Omote or OniDomainPhase.Ura)
            {
                AdvanceStep();
            }
        }

        private static void AdvanceIfReady()
        {
            if (currentStep == Step_HudIntro && stepTimer > 60 * 20)
            {
                AdvanceStep();
                return;
            }

            if (currentStep == Step_Mei && (OniMeiUI.Instance?.IsOpen ?? false))
            {
                AdvanceStep();
                return;
            }

            if (currentStep == Step_Register)
            {
                if (OniRegisterUI.Instance?.IsOpen ?? false) {
                    registerOpenedThisStep = true;
                }
                else if (registerOpenedThisStep) {
                    AdvanceStep();
                }
                return;
            }

            if (currentStep == Step_Domain && stepTimer > 60 * 25)
            {
                AdvanceStep();
            }
        }

        private static void AdvanceStep()
        {
            if (currentStep == Step_Register) WriteCheckpoint(Checkpoint_Hud);

            currentStep++;
            if (currentStep <= Step_Done) EnterStep(currentStep);
        }

        private static void WriteCheckpoint(int checkpoint)
        {
            var guide = Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            if (guide.Checkpoint < checkpoint) guide.Checkpoint = checkpoint;
        }

        private static void RestoreMeiSnapshotIfNeeded()
        {
            if (meiSnapshot == null) return;
            var data = OnikiriData.TryGet(Main.LocalPlayer?.GetItem());
            if (data == null) { meiSnapshot = null; return; }
            data.Mei.CopyFrom(meiSnapshot.Store);
            WraithVessels.SyncSlot(Main.LocalPlayer, Main.LocalPlayer.GetItem());
            meiSnapshot = null;
        }

        private static void FinishTutorial()
        {
            RestoreMeiSnapshotIfNeeded();
            Unsubscribe();
            OnikiriTutorialLead.MarkComplete();
        }

        internal sealed class OniMeiSnapshot
        {
            internal readonly Inscriptions.OniMeiStore Store = new();
            internal OniMeiSnapshot(Inscriptions.OniMeiStore source) => Store.CopyFrom(source);
        }

        internal static void BeginMeiTransaction(Inscriptions.OniMeiStore current)
            => meiSnapshot = new OniMeiSnapshot(current);

        internal static void RestoreMeiSnapshotOnClose()
            => RestoreMeiSnapshotIfNeeded();
    }
}
