using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 鬼雨异化翻转的运镜：锁输入、聚焦湖面缝线（水线压到屏幕中线，倒转枢轴恒等的前提）、
    /// 沸腾段压镜变焦、结算震屏。仅施术者本机播放；演出本体由
    /// <see cref="KikasaDomainPlayer"/> 的翻转状态机驱动，运镜失败不致命。
    /// </summary>
    internal sealed class KikasaFlipCutscene : CutsceneClip
    {
        public override int Priority => 45;

        public override bool CanPlay(Player player)
            => base.CanPlay(player)
                && player.GetModPlayer<KikasaDomainPlayer>().Phase == KikasaDomainPhase.Flipping;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = KikasaDomain.FlipTotalFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //聚焦湖面缝线，倒转枢轴由此落在屏幕中线
                .Add(CameraFocusTrack.Follow(0, total, _ => FlipFocusWorld(), default, 0.085f))
                //沸腾段压镜拉近，看着湖变色翻滚；落定后回拉
                .Add(new CameraZoomTrack(0, KikasaDomain.FlipBoilEnd,
                    1f, 1.12f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(KikasaDomain.FlipRollEnd,
                    total - KikasaDomain.FlipRollEnd,
                    1.12f, 1f, 0.05f, CutsceneEase.CubicOut))
                //结算白闪震屏
                .Add(new CameraShakeTrack(KikasaDomain.FlipCommitFrame,
                    Vector2.Zero, 7f, 0.9f, 30));
        }

        //缝线焦点：施术者本机的湖面线略上抬，玩家横漂时跟随其 X
        private static Vector2 FlipFocusWorld() {
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            }
            KikasaDomainPlayer kdp = player.GetModPlayer<KikasaDomainPlayer>();
            float lakeY = kdp.AnyActive ? kdp.LakeWorldY : player.Bottom.Y;
            return new Vector2(player.Center.X, lakeY - 8f);
        }
    }
}
