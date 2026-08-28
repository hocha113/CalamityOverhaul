using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts
{
    /// <summary>
    /// 大范围重启的运镜：全程锁输入、吞没段轻推近盯住潮水漫顶、落定回拉，
    /// 潮水漫顶与结算各一记震屏。仅施术者本机播放；演出本体由
    /// <see cref="HalibutReset"/> 的时间轴驱动，运镜失败不致命。
    /// 被波及的其他玩家不锁镜，他们的位置被倒放接管，镜头自然跟人走
    /// </summary>
    internal sealed class HalibutResetCutscene : CutsceneClip
    {
        public override int Priority => 45;

        public override bool CanPlay(Player player)
            => base.CanPlay(player)
                && HalibutReset.Active != null
                && HalibutReset.Active.OwnerWho == player.whoAmI;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = HalibutReset.TotalFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入：倒带里玩家的手不该插得进去
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //吞没段推近一格盯住漫顶，落定后回拉
                .Add(new CameraZoomTrack(0, HalibutReset.FloodEnd,
                    1f, 1.07f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(HalibutReset.RewindEnd,
                    total - HalibutReset.RewindEnd,
                    1.07f, 1f, 0.05f, CutsceneEase.CubicOut))
                //潮水漫顶一记闷震，结算白闪一记重些
                .Add(new CameraShakeTrack(HalibutReset.FloodEnd - 8,
                    Vector2.Zero, 4.5f, 0.85f, 14))
                .Add(new CameraShakeTrack(HalibutReset.RewindEnd,
                    Vector2.Zero, 6f, 0.9f, 24));
        }
    }
}
