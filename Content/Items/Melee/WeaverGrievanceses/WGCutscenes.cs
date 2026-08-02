using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    internal readonly record struct WeaverGrievancesActorRef(int Slot, ushort Generation);

    internal static class WeaverGrievancesCutsceneTarget
    {
        internal static WGManifestationActor Resolve(WeaverGrievancesActorRef subject) {
            return WGManifestationSystem.TryResolveActor(
                subject.Slot, subject.Generation, out WGManifestationActor actor)
                ? actor : null;
        }

        internal static Vector2 Focus(CutsceneContext context) {
            if (context.TryGetSubject(out WeaverGrievancesActorRef subject)
                && Resolve(subject) is WGManifestationActor actor) {
                return actor.CameraFocusPoint;
            }

            context.Stop();
            return context.PlayerCenter;
        }
    }

    /// <summary>聚魂坠地运镜，远端玩家不参与</summary>
    internal sealed class WeaverGrievancesManifestCutscene : CutsceneClip<WeaverGrievancesActorRef>
    {
        public override int Priority => 34;

        public override bool CanPlay(Player player, WeaverGrievancesActorRef subject)
            => WeaverGrievancesCutsceneTarget.Resolve(subject) is WGManifestationActor actor
                && player.Center.DistanceSQ(actor.CameraFocusPoint) < 1800f * 1800f;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = WGManifestationActor.GatheringFrames
                + WGManifestationActor.SettlingFrames
                + WGManifestationActor.MaximumFallingFrames
                + WGManifestationActor.ManifestAftermathFrames + 60;
            timeline.Duration = total;
            timeline
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                .Add(CameraFocusTrack.Follow(0, total, WeaverGrievancesCutsceneTarget.Focus,
                    new Vector2(0f, 24f), 0.24f))
                .Add(new CameraZoomTrack(0, 42, 1f, 1.28f, 0.05f, CutsceneEase.CubicOut));
        }
    }

    /// <summary>本地拔刀运镜</summary>
    internal sealed class WeaverGrievancesPullCutscene : CutsceneClip<WeaverGrievancesActorRef>
    {
        public override int Priority => 40;

        public override bool CanPlay(Player player, WeaverGrievancesActorRef subject)
            => WeaverGrievancesCutsceneTarget.Resolve(subject)?.IsPlanted == true;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = WGManifestationActor.PullCutsceneFrames;
            timeline.Duration = total;
            timeline
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                .Add(CameraFocusTrack.Midpoint(0, total, WeaverGrievancesCutsceneTarget.Focus,
                    context => context.PlayerCenter, new Vector2(0f, -18f), 0.075f))
                .Add(new CameraZoomTrack(0, 18, 1f, 1.24f, 0.06f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(44, total - 44, 1.24f, 1f, 0.07f, CutsceneEase.CubicOut));
        }
    }
}
