using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>
    /// 海虾运镜静态口：入场/死亡两段演出各自敞开一个 20 帧窗口（状态推进时上膛），
    /// 窗口关闭后不重播。锚点每帧由状态刷新，时间轴构建期只吃常量（InnoVault 契约）。
    /// 纯本机表现，不锁输入
    /// </summary>
    internal static class SeaShrimpCutscenes
    {
        internal static Vector2 IntroAnchor;
        internal static Vector2 DeathAnchor;
        private static uint introArmTick;
        private static uint deathArmTick;

        internal static void ArmIntro(Vector2 anchor) {
            IntroAnchor = anchor;
            introArmTick = Main.GameUpdateCount;
        }

        internal static void ArmDeath(Vector2 anchor) {
            DeathAnchor = anchor;
            deathArmTick = Main.GameUpdateCount;
        }

        internal static bool IntroArmed => Main.GameUpdateCount - introArmTick < 6u;
        internal static bool DeathArmed => Main.GameUpdateCount - deathArmTick < 6u;
    }

    /// <summary>入场运镜：聚焦破沙点，缓推缓收，炸沙拍带定向震（84f 与状态同拍）</summary>
    internal sealed class SeaShrimpIntroCutscene : CutsceneClip
    {
        public override int Priority => 42;

        public override bool CanPlay(Player player)
            => base.CanPlay(player) && SeaShrimpGate.Enabled && SeaShrimpCutscenes.IntroArmed
                && Vector2.Distance(player.Center, SeaShrimpCutscenes.IntroAnchor) < 2000f;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            const int total = 240;
            timeline.Duration = total;
            timeline
                .Add(CameraFocusTrack.Follow(0, total, _ => SeaShrimpCutscenes.IntroAnchor, default, 0.075f))
                .Add(new CameraZoomTrack(0, 110, 1f, 1.11f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(170, total - 170, 1.11f, 1f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraShakeTrack(84, Vector2.Zero, 8f, 0.9f, 26));
        }
    }

    /// <summary>死亡运镜：慢推向濒死的躯壳，内爆拍全屏震收尾</summary>
    internal sealed class SeaShrimpDeathCutscene : CutsceneClip
    {
        public override int Priority => 44;

        public override bool CanPlay(Player player)
            => base.CanPlay(player) && SeaShrimpGate.Enabled && SeaShrimpCutscenes.DeathArmed
                && Vector2.Distance(player.Center, SeaShrimpCutscenes.DeathAnchor) < 2200f;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            const int total = 330;
            timeline.Duration = total;
            timeline
                .Add(CameraFocusTrack.Follow(0, total, _ => SeaShrimpCutscenes.DeathAnchor, default, 0.06f))
                .Add(new CameraZoomTrack(0, 250, 1f, 1.16f, 0.04f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(292, total - 292, 1.16f, 1f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraShakeTrack(270, Vector2.Zero, 12f, 0.88f, 30));
        }
    }
}
