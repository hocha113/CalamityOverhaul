using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals
{
    internal static class VoidReturnSession
    {
        internal static VoidReturnPortalActor Portal { get; private set; }
        internal static bool IsOpen => Portal != null && Phase != PanelPhase.Closed;
        internal static PanelPhase Phase { get; private set; }
        internal static float OpenProgress { get; private set; }

        internal enum PanelPhase
        {
            Closed,
            Open,
            Closing,
        }

        internal static void Open(VoidReturnPortalActor portal) {
            if (portal == null) return;
            Portal = portal;
            Phase = PanelPhase.Open;
        }

        internal static void RequestClose() {
            if (!IsOpen || Phase == PanelPhase.Closing) return;
            Phase = PanelPhase.Closing;
        }

        internal static void Close() {
            Portal = null;
            Phase = PanelPhase.Closed;
            OpenProgress = 0f;
        }

        internal static void Update() {
            if (Phase == PanelPhase.Closed) return;
            if (Portal == null || !Portal.Active || Main.gameMenu || !VoidColony.Active) {
                Close();
                return;
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
