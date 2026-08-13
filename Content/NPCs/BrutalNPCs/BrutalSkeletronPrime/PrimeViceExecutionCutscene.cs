using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 老虎钳处刑运镜：镜头黏在被抓玩家身上随连段推拉，仅被抓玩家客户端播放，
    /// 由 PrimeVicePerformancePlayer 本地启停
    /// </summary>
    internal sealed class PrimeViceExecutionCutscene : CutsceneClip<NPC>
    {
        //低于死亡演出(100)：投技中触发死亡演出时让位
        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = PrimeViceExecutionState.TotalFrames;
            timeline.Duration = total;

            //镜头全程跟随被抓玩家，下砸段跟手
            timeline.Add(CameraFocusTrack.Follow(0, total, PlayerFocus, new Vector2(0f, -20f), 0.14f));

            //缩放：抓握推近→研磨最近→齐射微退→砸地拉开全景
            timeline
                .Add(new CameraZoomTrack(0, PrimeViceExecutionState.ClampEnd, 1f, 1.25f, 0.06f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(PrimeViceExecutionState.ClampEnd,
                    PrimeViceExecutionState.HoistEnd - PrimeViceExecutionState.ClampEnd, 1.25f, 1.5f, 0.05f))
                .Add(new CameraZoomTrack(PrimeViceExecutionState.HoistEnd,
                    PrimeViceExecutionState.GrindEnd - PrimeViceExecutionState.HoistEnd, 1.5f, 1.85f, 0.045f))
                .Add(new CameraZoomTrack(PrimeViceExecutionState.GrindEnd,
                    PrimeViceExecutionState.VolleyEnd - PrimeViceExecutionState.GrindEnd, 1.85f, 1.7f, 0.05f))
                .Add(new CameraZoomTrack(PrimeViceExecutionState.VolleyEnd,
                    PrimeViceExecutionState.ImpactTick - PrimeViceExecutionState.VolleyEnd, 1.7f, 1.45f, 0.06f))
                .Add(new CameraZoomTrack(PrimeViceExecutionState.ImpactTick,
                    total - PrimeViceExecutionState.ImpactTick, 1.45f, 1f, 0.05f, CutsceneEase.CubicOut));

            //锁操作到释放弹出后一拍，运镜尾段恢复自由；Utility 同锁防钩爪/坐骑挣脱
            timeline.Add(new InputLockTrack(0, PrimeViceExecutionState.PinEnd + 14,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump
                | CutsceneInputLockFlags.UseItem | CutsceneInputLockFlags.Utility));
        }

        private static Vector2 PlayerFocus(CutsceneContext context) => context.PlayerCenter;
    }
}
