using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 入雨演出运镜：锁输入、聚焦缝线焦点（脚底线压到屏幕中线）、压镜变焦、结算震屏。<br/>
    /// 演出本体由 <see cref="OniRainWorldTransition"/> 驱动，本片段只管镜头；运镜失败不致命。
    /// </summary>
    internal sealed class OniRainWorldCutscene : CutsceneClip
    {
        public override int Priority => 45;

        public override bool CanPlay(Player player)
            => base.CanPlay(player) && OniRainWorldTransition.Active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = OniRainWorldTransition.TotalFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //聚焦缝线焦点，镜面枢轴由此落在屏幕中线
                .Add(CameraFocusTrack.Follow(0, total,
                    _ => OniRainWorldTransition.FocusWorld, default, 0.085f))
                //压镜拉近，驻留看镜；落定后回拉
                .Add(new CameraZoomTrack(0, OniRainWorldTransition.ApproachEnd,
                    1f, 1.12f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(OniRainWorldTransition.RollEnd,
                    total - OniRainWorldTransition.RollEnd,
                    1.12f, 1f, 0.05f, CutsceneEase.CubicOut))
                //结算白闪震屏
                .Add(new CameraShakeTrack(OniRainWorldTransition.CommitFrame,
                    Vector2.Zero, 7f, 0.9f, 30));
        }
    }
}
