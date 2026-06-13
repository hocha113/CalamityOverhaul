using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 机械骷髅王死亡演出运镜——基于 InnoVault 演出时间轴 <see cref="CutsceneClip{TSubject}"/> 实现。
    /// <para>以死亡演出头部 NPC 作为演出主体，时间轴严格对齐 <see cref="PrimeDeathState"/> 的阶段常量：
    /// 镜头始终聚焦头部及其正下方（玩家最终被举到此处），缩放单调推进至怒吼顶点、仅终爆才拉开看全景，
    /// 杜绝中途回拉的"呼吸感"；全程锁定本地玩家操作（围观这场处决）。</para>
    /// <para>屏幕震动由 <see cref="PrimeDeathPerformancePlayer.RequestShake"/> 按演出事件请求，转交本演出叠加。</para>
    /// </summary>
    internal sealed class PrimeDeathCutscene : CutsceneClip<NPC>
    {
        //死亡演出运镜优先级——高于普通演出，处决过场不应被其它运镜打断
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = PrimeDeathState.PhaseFinaleEnd;

            //聚焦点始终围绕头部及其正下方，不去追远处玩家，避免镜头来回甩动；
            //偏移随阶段单调下移（玩家被逐步举到头部正前方），由运镜运行时平滑插值吸收突变
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

        //各阶段持续帧数（由 PrimeDeathState 的累计结束帧推导）
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
