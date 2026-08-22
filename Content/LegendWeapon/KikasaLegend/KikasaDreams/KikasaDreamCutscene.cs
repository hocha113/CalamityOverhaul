using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦运镜共用件：锁输入、聚焦湖面缝线（水线压到屏幕中线，倒转枢轴恒等的前提）、
    /// 沸腾段压镜变焦、结算震屏。仅施术者本机播放；演出本体由
    /// <see cref="KikasaDreamDirector"/> 驱动，运镜失败不致命。<br/>
    /// 拉入与归返节拍不同长，拆成两个片段，InnoVault 的时间轴在 VaultSetup
    /// 构建一次即缓存，<c>BuildTimeline</c> 里只允许常量，不得读玩家状态
    /// </summary>
    internal static class KikasaDreamCutsceneShared
    {
        /// <summary>缝线焦点：施术者本机的湖面线略上抬，玩家横漂时跟随其 X</summary>
        internal static Vector2 DreamFocusWorld() {
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            }
            KikasaDomainPlayer kdp = player.GetModPlayer<KikasaDomainPlayer>();
            float lakeY = kdp.AnyActive ? kdp.LakeWorldY : player.Bottom.Y;
            return new Vector2(player.Center.X, lakeY - 8f);
        }

        /// <summary>两段共用的轨道拼装，节拍全由调用方以常量喂入</summary>
        internal static void Build(CutsceneTimeline timeline, int total, int zoomInEnd,
            int rollEnd, int commit) {
            timeline.Duration = total;
            timeline
                //全程锁输入
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //聚焦湖面缝线，倒转枢轴由此落在屏幕中线
                .Add(CameraFocusTrack.Follow(0, total, _ => DreamFocusWorld(), default, 0.085f))
                //沸腾段压镜拉近，看着湖滚成黑红；落定后回拉
                .Add(new CameraZoomTrack(0, zoomInEnd, 1f, 1.14f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(rollEnd, total - rollEnd, 1.14f, 1f, 0.05f, CutsceneEase.CubicOut))
                //结算闪震屏
                .Add(new CameraShakeTrack(commit, Vector2.Zero, 7.5f, 0.9f, 30));
        }
    }

    /// <summary>鬼梦拉入的运镜，仅 DreamPull 相位可播</summary>
    internal sealed class KikasaDreamPullCutscene : CutsceneClip
    {
        public override int Priority => 46;

        public override bool CanPlay(Player player)
            => base.CanPlay(player)
                && player.GetModPlayer<KikasaDomainPlayer>().Phase == KikasaDomainPhase.DreamPull;

        protected override void BuildTimeline(CutsceneTimeline timeline)
            => KikasaDreamCutsceneShared.Build(timeline,
                KikasaDream.PullTotalFrames, KikasaDream.PullBoilEnd,
                KikasaDream.PullRollEnd, KikasaDream.PullCommitFrame);
    }

    /// <summary>鬼梦归返的运镜，仅 DreamReturn 相位可播；比拉入利落</summary>
    internal sealed class KikasaDreamReturnCutscene : CutsceneClip
    {
        public override int Priority => 46;

        public override bool CanPlay(Player player)
            => base.CanPlay(player)
                && player.GetModPlayer<KikasaDomainPlayer>().Phase == KikasaDomainPhase.DreamReturn;

        protected override void BuildTimeline(CutsceneTimeline timeline)
            => KikasaDreamCutsceneShared.Build(timeline,
                KikasaDream.ReturnTotalFrames, KikasaDream.ReturnDwellEnd,
                KikasaDream.ReturnRollEnd, KikasaDream.ReturnCommitFrame);
    }
}
