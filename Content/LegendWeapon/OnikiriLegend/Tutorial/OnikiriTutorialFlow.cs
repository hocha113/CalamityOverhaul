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
    /// 步骤：[0]HUD → [1]点鬼簿 → [2]改铭台 → [3]鬼域之眼 → [4]练习鬼影 → [5]肢解介绍 → [6]结束。<br/>
    /// 不演示五连/疾走/处决/乱舞等难跟做环节；鬼影与肢解以认知+可选尝试为主。
    /// </summary>
    internal static class OnikiriTutorialFlow
    {
        internal const int Step_HudIntro = 0;
        internal const int Step_Register = 1;
        internal const int Step_Mei = 2;
        internal const int Step_Domain = 3;
        internal const int Step_Wraith = 4;
        internal const int Step_Dismember = 5;
        internal const int Step_Done = 6;

        /// <summary>HUD/簿/台段完成后；重进从鬼域步恢复</summary>
        internal const int Checkpoint_Hud = 1;
        /// <summary>鬼域+鬼影段完成后；重进从肢解介绍恢复</summary>
        internal const int Checkpoint_Field = 2;

        private static int currentStep = -1;
        private static int stepTimer;
        private static bool initialized;
        private static OniMeiSnapshot meiSnapshot;
        private static bool meiOpenedThisStep;

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
            meiOpenedThisStep = false;
            meiSnapshot = null;
            OnikiriTutorialWraith.ClearServerState();
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
            //鬼影步持续确保靶在场（联机确认迟到时补请）
            if (currentStep is Step_Wraith or Step_Dismember
                && stepTimer % 60 == 0
                && OnikiriTutorialWraith.GetLocalTarget() == null)
            {
                OnikiriTutorialNet.RequestEnsureTarget();
            }
            AdvanceIfReady();
        }

        private static void Initialize()
        {
            initialized = true;
            Subscribe();

            var guide = Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            currentStep = guide.Checkpoint switch
            {
                >= Checkpoint_Field => Step_Dismember,
                Checkpoint_Hud => Step_Domain,
                _ => Step_HudIntro,
            };
            //旧档检查点 3（曾表示全通）直接收尾
            if (guide.Checkpoint >= 3 && guide.CompletedVersion < OnikiriTutorialLead.TutorialVersion)
            {
                currentStep = Step_Dismember;
            }
            stepTimer = 0;
            EnterStep(currentStep);
        }

        private static void Subscribe()
        {
            OnikiriTutorialEvents.OnDomainPhaseSettled += HandleDomainPhaseSettled;
            OnikiriTutorialEvents.OnDismemberLanded += HandleDismemberLanded;
        }

        private static void Unsubscribe()
        {
            OnikiriTutorialEvents.OnDomainPhaseSettled -= HandleDomainPhaseSettled;
            OnikiriTutorialEvents.OnDismemberLanded -= HandleDismemberLanded;
        }

        private static void EnterStep(int step)
        {
            stepTimer = 0;

            switch (step)
            {
                case Step_Mei:
                    meiOpenedThisStep = false;
                    break;

                case Step_Wraith:
                    OnikiriTutorialNet.RequestEnsureTarget();
                    break;

                case Step_Dismember:
                    OnikiriTutorialNet.RequestEnsureTarget();
                    //面影错位姿态，方便认清肢解靶
                    if (OnikiriTutorialWraith.GetLocalTarget()?.ModNPC is OnikiriTutorialWraith w)
                        w.SetPose(OnikiriTutorialWraith.WraithPose.PaperOffset);
                    break;

                case Step_Done:
                    FinishTutorial();
                    break;
            }
        }

        private static void HandleDomainPhaseSettled(OniDomainPhase phase)
        {
            //鬼域步：玩家亲自展域也算完成认知
            if (currentStep == Step_Domain
                && phase is OniDomainPhase.Omote or OniDomainPhase.Ura)
            {
                AdvanceStep();
            }
        }

        private static void HandleDismemberLanded(NPC target)
        {
            if (currentStep != Step_Dismember) return;
            if (target == null || !target.active) return;
            if (OnikiriTutorialWraith.GetLocalTarget()?.whoAmI != target.whoAmI) return;
            AdvanceStep();
        }

        private static void AdvanceIfReady()
        {
            if (currentStep == Step_HudIntro && stepTimer > 60 * 20)
            {
                AdvanceStep();
                return;
            }

            if (currentStep == Step_Register && (OniRegisterUI.Instance?.IsOpen ?? false))
            {
                AdvanceStep();
                return;
            }

            if (currentStep == Step_Mei)
            {
                if (OniMeiUI.Instance?.IsOpen ?? false) {
                    meiOpenedThisStep = true;
                }
                else if (meiOpenedThisStep) {
                    AdvanceStep();
                }
                return;
            }

            if (currentStep == Step_Domain && stepTimer > 60 * 25)
            {
                AdvanceStep();
                return;
            }

            if (currentStep == Step_Wraith && stepTimer > 60 * 25)
            {
                AdvanceStep();
                return;
            }

            if (currentStep == Step_Dismember && stepTimer > 60 * 30)
            {
                AdvanceStep();
            }
        }

        private static void AdvanceStep()
        {
            if (currentStep == Step_Mei) WriteCheckpoint(Checkpoint_Hud);
            if (currentStep == Step_Wraith) WriteCheckpoint(Checkpoint_Field);

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
            OnikiriTutorialNet.RequestReleaseTarget();
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
