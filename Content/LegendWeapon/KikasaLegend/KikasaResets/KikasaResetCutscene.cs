using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启的运镜：全程锁输入、快门段轻推近、落定回拉，
    /// 定格与结算各一记震屏。仅施术者本机播放；演出本体由
    /// <see cref="KikasaReset"/> 的时间轴驱动，运镜失败不致命。
    /// 被波及的其他玩家不锁镜——他们的位置被倒放接管，镜头自然跟人走
    /// </summary>
    internal sealed class KikasaResetCutscene : CutsceneClip
    {
        public override int Priority => 46;

        public override bool CanPlay(Player player)
            => base.CanPlay(player)
                && KikasaReset.Active != null
                && KikasaReset.Active.OwnerWho == player.whoAmI;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = KikasaReset.TotalFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入：倒带里玩家的手不该插得进去
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //快门推近一格盯住定格，落定后回拉
                .Add(new CameraZoomTrack(0, KikasaReset.SnapshotEnd,
                    1f, 1.06f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(KikasaReset.RewindEnd,
                    total - KikasaReset.RewindEnd,
                    1.06f, 1f, 0.05f, CutsceneEase.CubicOut))
                //快门一记轻震，结算白闪一记重些
                .Add(new CameraShakeTrack(0, Vector2.Zero, 4f, 0.85f, 12))
                .Add(new CameraShakeTrack(KikasaReset.RewindEnd,
                    Vector2.Zero, 6f, 0.9f, 24));
        }
    }
}
