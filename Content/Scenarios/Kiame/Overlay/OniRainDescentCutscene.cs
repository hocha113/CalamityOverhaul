using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Overlay
{
    /// <summary>
    /// 深潜演出运镜：锁输入、聚焦伞盖、起势段轻压镜、排墨段回拉、结算震屏。<br/>
    /// 演出本体由 <see cref="OniRainDescentTransition"/> 驱动，本片段只管镜头；运镜失败不致命。
    /// </summary>
    internal sealed class OniRainDescentCutscene : CutsceneClip
    {
        public override int Priority => 45;

        public override bool CanPlay(Player player)
            => base.CanPlay(player) && OniRainDescentTransition.Active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = OniRainDescentTransition.TotalFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //聚焦伞盖，深潜的门就是这把伞
                .Add(CameraFocusTrack.Follow(0, total,
                    _ => OniRainDescentTransition.FocusWorld, default, 0.085f))
                //起势轻压镜；排墨段回拉
                .Add(new CameraZoomTrack(0, OniRainDescentTransition.SurgeEnd,
                    1f, 1.10f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(OniRainDescentTransition.DrainStart,
                    total - OniRainDescentTransition.DrainStart,
                    1.10f, 1f, 0.05f, CutsceneEase.CubicOut))
                //结算雷闪震屏
                .Add(new CameraShakeTrack(OniRainDescentTransition.CommitFrame,
                    Vector2.Zero, 7f, 0.9f, 30));
        }
    }
}
