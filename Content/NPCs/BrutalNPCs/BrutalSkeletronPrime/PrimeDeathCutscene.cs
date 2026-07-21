using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>死亡运镜，对齐PrimeDeathState阶段帧</summary>
    internal sealed class PrimeDeathCutscene : CutsceneClip<NPC>
    {
        //死亡演出运镜优先级，高于普通演出
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = PrimeDeathState.PhaseFinaleEnd;

            //聚焦头部正下方，偏移随阶段单调下移
            timeline
                .Add(CameraFocusTrack.Follow(0, PrimeDeathState.PhaseFakeDeathEnd,
                    HeadCenter, new Vector2(0f, 0f), 0.045f))
                .Add(CameraFocusTrack.Follow(PrimeDeathState.PhaseFakeDeathEnd, SummonLen,
                    HeadCenter, new Vector2(0f, 20f), 0.05f))
                .Add(CameraFocusTrack.Follow(PrimeDeathState.PhaseSummonEnd, LungeLen,
                    HeadCenter, new Vector2(0f, 45f), 0.07f))
                .Add(CameraFocusTrack.Follow(PrimeDeathState.PhaseLungeEnd, DragLen,
                    HeadCenter, new Vector2(0f, PrimeDeathState.DeathLiftDistance * 0.5f), 0.08f))
                .Add(CameraFocusTrack.Follow(PrimeDeathState.PhaseDragEnd, RoarLen,
                    HeadCenter, new Vector2(0f, PrimeDeathState.DeathLiftDistance * 0.45f), 0.1f))
                .Add(CameraFocusTrack.Follow(PrimeDeathState.PhaseRoarEnd, FinaleLen,
                    HeadCenter, new Vector2(0f, PrimeDeathState.DeathLiftDistance * 0.25f), 0.06f));

            //缩放单调推进至怒吼顶点（2.1x），终爆拉开看全景（1.4x）
            timeline
                .Add(new CameraZoomTrack(0, PrimeDeathState.PhaseFakeDeathEnd, 1f, 1.3f, 0.03f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(PrimeDeathState.PhaseFakeDeathEnd, SummonLen, 1.3f, 1.45f, 0.045f))
                .Add(new CameraZoomTrack(PrimeDeathState.PhaseSummonEnd, LungeLen, 1.45f, 1.6f, 0.05f))
                .Add(new CameraZoomTrack(PrimeDeathState.PhaseLungeEnd, DragLen, 1.6f, 1.8f, 0.055f))
                .Add(new CameraZoomTrack(PrimeDeathState.PhaseDragEnd, RoarLen, 1.8f, 2.1f, 0.07f))
                .Add(new CameraZoomTrack(PrimeDeathState.PhaseRoarEnd, FinaleLen, 2.1f, 1.4f, 0.05f, CutsceneEase.CubicOut));

            //全程锁定本地玩家操作
            timeline.Add(new InputLockTrack(0, PrimeDeathState.PhaseFinaleEnd,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        //各阶段持续帧
        private static int SummonLen => PrimeDeathState.PhaseSummonEnd - PrimeDeathState.PhaseFakeDeathEnd;
        private static int LungeLen => PrimeDeathState.PhaseLungeEnd - PrimeDeathState.PhaseSummonEnd;
        private static int DragLen => PrimeDeathState.PhaseDragEnd - PrimeDeathState.PhaseLungeEnd;
        private static int RoarLen => PrimeDeathState.PhaseRoarEnd - PrimeDeathState.PhaseDragEnd;
        private static int FinaleLen => PrimeDeathState.PhaseFinaleEnd - PrimeDeathState.PhaseRoarEnd;

        //演出主体（头部 NPC）失效时回退到玩家中心，避免镜头瞬移到世界原点
        private static Vector2 HeadCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC head) && head.active ? head.Center : context.PlayerCenter;
    }
}
