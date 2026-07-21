using CalamityOverhaul.Content.HackTimes.Targets;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>厉鬼扫描，可扫不可骇，(whoAmI, generation) 防槽位复用</summary>
    internal class WraithScannable : IHackTarget
    {
        public int ActorWho { get; }
        public ushort ActorGeneration { get; }

        public WraithScannable(int actorWho, ushort generation) {
            ActorWho = actorWho;
            ActorGeneration = generation;
        }

        private WraithActor Resolve() {
            if (ActorWho < 0 || ActorWho >= ActorLoader.MaxActorCount) {
                return null;
            }
            return ActorLoader.Actors[ActorWho] is WraithActor wraith && wraith.Active
                && wraith.Generation == ActorGeneration ? wraith : null;
        }

        /// <summary>高亮层比对用</summary>
        public bool Matches(WraithActor wraith)
            => wraith != null && wraith.WhoAmI == ActorWho && wraith.Generation == ActorGeneration;

        #region IScannable

        public Vector2 WorldCenter => Resolve()?.Center ?? Vector2.Zero;

        public bool IsValid => Resolve() != null;

        public bool IsHackable => false;

        public int ScanRowCount => 6;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            WraithActor wraith = Resolve();
            if (wraith == null) {
                return;
            }

            labels[0] = HackTime.WraithScanName.Value;
            values[0] = HackTime.WraithScanNameValue.Value;
            colors[0] = HackTheme.TextBright;

            labels[1] = HackTime.TypeLabel.Value;
            values[1] = HackTime.WraithScanType.Value;
            colors[1] = HackTheme.Danger;

            labels[2] = HackTime.ThreatLabel.Value;
            values[2] = HackTime.WraithScanThreat.Value;
            colors[2] = HackTheme.Danger;

            labels[3] = HackTime.WraithScanStatus.Value;
            values[3] = ResolveStatus(wraith, out Color statusColor);
            colors[3] = statusColor;

            labels[4] = HackTime.WraithScanIntegrity.Value;
            values[4] = HackTime.WraithScanIntegrityValue.Value;
            colors[4] = HackTheme.TextDim;

            labels[5] = HackTime.WraithScanOrigin.Value;
            values[5] = HackTime.WraithScanOriginValue.Value;
            colors[5] = HackTheme.TextDim;
        }

        //死机 → 凝视 → 裂解/成形 → 追猎
        private static string ResolveStatus(WraithActor wraith, out Color color) {
            if (wraith.IsHalted) {
                color = HackTheme.Accent;
                return HackTime.WraithScanStatusHalt.Value;
            }
            Player local = Main.LocalPlayer;
            if (local != null && local.active && !local.dead
                && WraithSensors.IsGazedBy(local, wraith, wraith.Definition?.GazeRange ?? 900f)) {
                color = HackTheme.AccentAlt;
                return HackTime.WraithScanStatusWatched.Value;
            }
            if (wraith.Presence == WraithPresence.Dematerializing) {
                color = HackTheme.TextDim;
                return HackTime.WraithScanStatusDismember.Value;
            }
            if (wraith.Presence == WraithPresence.Materializing) {
                color = HackTheme.Uploading;
                return HackTime.WraithScanStatusMemory.Value;
            }
            color = HackTheme.Uploading;
            return HackTime.WraithScanStatusStalking.Value;
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<WraithTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                WraithActor wraith = Resolve();
                if (wraith == null) {
                    return Vector2.Zero;
                }
                return new Vector2(
                    Math.Max(wraith.Width, 32) * 0.6f + 28f,
                    Math.Max(wraith.Height, 32) * 0.6f + 28f);
            }
        }

        public string LockFrameTitle => IsValid ? HackTime.WraithScanNameValue.Value : string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            WraithActor wraith = Resolve();
            if (wraith == null) {
                return false;
            }
            text = ResolveStatus(wraith, out color);
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) => false;

        public bool TargetEquals(IHackTarget other) {
            return other is WraithScannable w && w.ActorWho == ActorWho && w.ActorGeneration == ActorGeneration;
        }

        #endregion
    }
}
