using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Overlay
{
    /// <summary>
    /// 送出演出运镜：锁输入、镜头落在被送走的人身上、合幕段轻压镜、排水段回拉、结算震屏。<br/>
    /// 演出本体由 <see cref="OniRainExitTransition"/> 驱动，本片段只管镜头；运镜失败不致命。
    /// </summary>
    internal sealed class OniRainExitCutscene : CutsceneClip
    {
        public override int Priority => 45;

        public override bool CanPlay(Player player)
            => base.CanPlay(player) && OniRainExitTransition.Active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = OniRainExitTransition.TotalFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //镜头落在人身上：这次被送走的门是人自己
                .Add(CameraFocusTrack.Follow(0, total,
                    _ => OniRainExitTransition.FocusWorld, default, 0.085f))
                //合幕轻压镜；排水段回拉
                .Add(new CameraZoomTrack(0, OniRainExitTransition.SurgeEnd,
                    1f, 1.08f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(OniRainExitTransition.DrainStart,
                    total - OniRainExitTransition.DrainStart,
                    1.08f, 1f, 0.05f, CutsceneEase.CubicOut))
                //结算雷闪震屏
                .Add(new CameraShakeTrack(OniRainExitTransition.CommitFrame,
                    Vector2.Zero, 6f, 0.9f, 26));
        }
    }
}
