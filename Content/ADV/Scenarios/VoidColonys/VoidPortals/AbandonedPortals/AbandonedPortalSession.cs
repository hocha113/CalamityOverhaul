using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals.AbandonedPortals
{
    internal static class AbandonedPortalSession
    {
        public const int RepairDurationFrames = 60 * 60 * 3;

        internal static AbandonedPortalTP CurrentPortal { get; set; }
        internal static bool IsOpen => CurrentPortal != null && Phase != PanelPhase.Closed;
        internal static PanelPhase Phase { get; private set; }
        internal static int PhaseTimer { get; private set; }
        internal static float OpenProgress { get; private set; }
        internal static float SessionTime { get; private set; }

        internal enum PanelPhase
        {
            Closed,
            Broken,
            Repairing,
            Repaired,
            Closing,
        }

        internal static void Open(AbandonedPortalTP portal) {
            if (portal == null) return;
            CurrentPortal = portal;
            Phase = portal.State switch {
                AbandonedPortalTP.RepairState.Repairing => PanelPhase.Repairing,
                AbandonedPortalTP.RepairState.Repaired => PanelPhase.Repaired,
                _ => PanelPhase.Broken,
            };
            PhaseTimer = 0;
            SessionTime = 0f;
        }

        internal static void RequestClose() {
            if (!IsOpen || Phase == PanelPhase.Closing) return;
            Phase = PanelPhase.Closing;
            PhaseTimer = 0;
        }

        internal static void Close() {
            CurrentPortal = null;
            Phase = PanelPhase.Closed;
            PhaseTimer = 0;
            OpenProgress = 0f;
            SessionTime = 0f;
        }

        internal static void Update() {
            if (Phase == PanelPhase.Closed) return;

            if (CurrentPortal == null || Main.gameMenu) {
                Close();
                return;
            }

            SessionTime += 1f / 60f;
            PhaseTimer++;

            if (Phase != PanelPhase.Closing) {
                Phase = CurrentPortal.State switch {
                    AbandonedPortalTP.RepairState.Repairing => PanelPhase.Repairing,
                    AbandonedPortalTP.RepairState.Repaired => PanelPhase.Repaired,
                    _ => PanelPhase.Broken,
                };
            }

            float target = Phase == PanelPhase.Closing ? 0f : 1f;
            OpenProgress = MathHelper.Lerp(OpenProgress, target, 0.26f);
            if (Math.Abs(OpenProgress - target) < 0.005f) OpenProgress = target;

            if (Phase == PanelPhase.Closing && OpenProgress <= 0.01f) {
                Close();
            }
        }
    }
}
