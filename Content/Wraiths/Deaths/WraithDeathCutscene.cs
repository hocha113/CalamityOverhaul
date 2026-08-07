using CalamityOverhaul.Common;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 夺身死亡演出运镜，仅死者本机播放。<br/>
    /// 焦点、变焦与震屏逐帧读当前 <see cref="WraithDeathPerformance"/>；
    /// 时长由状态机结束时 <see cref="CutsceneDirector.Stop"/> 收尾。
    /// </summary>
    internal sealed class WraithDeathCutscene : CutsceneClip<WraithRevivalDeathPlayer>
    {
        //保护上限，实际由状态机主动 Stop
        private const int MaxFrames = 60 * 12;

        public override int Priority => 100;

        public override bool CanPlay(Player player, WraithRevivalDeathPlayer subject)
            => base.CanPlay(player, subject) && subject != null && subject.Active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = MaxFrames;
            timeline.Add(new DynamicCameraTrack(0, MaxFrames, DriveCamera));
        }

        private static void DriveCamera(CutsceneContext context) {
            Player player = context.Player;
            if (player == null || !player.active
                || !context.TryGetSubject(out WraithRevivalDeathPlayer seizure)
                || !seizure.Active) {
                return;
            }
            WraithDeathPerformance performance = seizure.CurrentPerformance;
            if (performance == null) {
                context.SetCameraFocus(player.Center, 0.12f);
                return;
            }
            context.SetCameraFocus(performance.CameraFocus, performance.CameraFocusLerp);
            context.SetCameraZoom(performance.CameraZoom, 0.045f);
            float shake = performance.ShakeIntensity;
            if (shake > 0.5f) {
                context.Shake(Vector2.Zero, shake, 0.9f, 3);
            }
        }
    }
}
