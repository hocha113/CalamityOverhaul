using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.Arbiters
{
    internal readonly record struct ArbiterActorRef(int Slot, ushort Generation);

    internal static class ArbiterCutsceneTarget
    {
        internal static ArbiterManifestationActor Resolve(ArbiterActorRef subject) {
            return ArbiterManifestationSystem.TryResolveActor(
                subject.Slot, subject.Generation, out ArbiterManifestationActor actor)
                ? actor : null;
        }

        internal static Vector2 Focus(CutsceneContext context) {
            if (context.TryGetSubject(out ArbiterActorRef subject)
                && Resolve(subject) is ArbiterManifestationActor actor) {
                return actor.CameraFocusPoint;
            }

            context.Stop();
            return context.PlayerCenter;
        }
    }

    /// <summary>熔铸坠地运镜,远端玩家不参与</summary>
    internal sealed class ArbiterManifestCutscene : CutsceneClip<ArbiterActorRef>
    {
        public override int Priority => 33;

        public override bool CanPlay(Player player, ArbiterActorRef subject)
            => ArbiterCutsceneTarget.Resolve(subject) is ArbiterManifestationActor actor
                && player.Center.DistanceSQ(actor.CameraFocusPoint) < 1800f * 1800f;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = ArbiterManifestationActor.ForgingFrames
                + ArbiterManifestationActor.PoisingFrames
                + ArbiterManifestationActor.MaximumFallingFrames
                + ArbiterManifestationActor.ManifestAftermathFrames + 60;
            timeline.Duration = total;
            timeline
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                .Add(CameraFocusTrack.Follow(0, total, ArbiterCutsceneTarget.Focus,
                    new Vector2(0f, 20f), 0.24f))
                .Add(new CameraZoomTrack(0, 42, 1f, 1.26f, 0.05f, CutsceneEase.CubicOut));
        }
    }

    /// <summary>本地拔斧运镜</summary>
    internal sealed class ArbiterPullCutscene : CutsceneClip<ArbiterActorRef>
    {
        public override int Priority => 39;

        public override bool CanPlay(Player player, ArbiterActorRef subject)
            => ArbiterCutsceneTarget.Resolve(subject)?.IsPlanted == true;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = ArbiterManifestationActor.PullCutsceneFrames;
            timeline.Duration = total;
            timeline
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                .Add(CameraFocusTrack.Midpoint(0, total, ArbiterCutsceneTarget.Focus,
                    context => context.PlayerCenter, new Vector2(0f, -16f), 0.075f))
                .Add(new CameraZoomTrack(0, 18, 1f, 1.22f, 0.06f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(46, total - 46, 1.22f, 1f, 0.07f, CutsceneEase.CubicOut));
        }
    }
}
