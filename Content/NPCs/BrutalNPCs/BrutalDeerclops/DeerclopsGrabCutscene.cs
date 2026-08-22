using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops
{
    /// <summary>
    /// 投技·凝视擒抱运镜，仅被抓玩家客户端播放，节拍对齐 DeerclopsEyeGrabState 常量。
    /// 优先级低于死亡运镜，boss中途进入死亡演出时被其顶替
    /// </summary>
    internal sealed class DeerclopsGrabCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 60;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = DeerclopsEyeGrabState.TotalTime;

            int dragLen = DeerclopsEyeGrabState.DragEnd;
            int liftLen = DeerclopsEyeGrabState.LiftEnd - DeerclopsEyeGrabState.DragEnd;
            int gazeLen = DeerclopsEyeGrabState.BreathEnd - DeerclopsEyeGrabState.LiftEnd;
            int slamLen = DeerclopsEyeGrabState.ReleaseTick - DeerclopsEyeGrabState.BreathEnd;
            int tailLen = DeerclopsEyeGrabState.TotalTime - DeerclopsEyeGrabState.ReleaseTick;

            //焦点全程咬住爪锚(拖拽段=玩家被拉的轨迹终点侧)，砸落段随爪压向地面
            timeline
                .Add(CameraFocusTrack.Follow(0, dragLen, ClawFocus, new Vector2(0f, -10f), 0.09f))
                .Add(CameraFocusTrack.Follow(DeerclopsEyeGrabState.DragEnd, liftLen, ClawFocus, new Vector2(0f, -20f), 0.07f))
                .Add(CameraFocusTrack.Follow(DeerclopsEyeGrabState.LiftEnd, gazeLen, ClawFocus, new Vector2(0f, -12f), 0.05f))
                .Add(CameraFocusTrack.Follow(DeerclopsEyeGrabState.BreathEnd, slamLen, ClawFocus, new Vector2(0f, 14f), 0.09f))
                .Add(CameraFocusTrack.Follow(DeerclopsEyeGrabState.ReleaseTick, tailLen, ClawFocus, new Vector2(0f, 20f), 0.05f));

            //推近节奏：拖拽收拢→拎起推近→凝视顶点(最紧)→砸落猛然放开→尾段回落
            timeline
                .Add(new CameraZoomTrack(0, dragLen, 1f, 1.28f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(DeerclopsEyeGrabState.DragEnd, liftLen, 1.28f, 1.45f, 0.045f))
                .Add(new CameraZoomTrack(DeerclopsEyeGrabState.LiftEnd, gazeLen, 1.45f, 1.66f, 0.03f))
                .Add(new CameraZoomTrack(DeerclopsEyeGrabState.BreathEnd, slamLen, 1.66f, 1.24f, 0.06f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(DeerclopsEyeGrabState.ReleaseTick, tailLen, 1.24f, 1.02f, 0.04f, CutsceneEase.CubicOut));

            //操控锁到释放拍为止，尾段镜头回落期间玩家已可行动
            timeline.Add(new InputLockTrack(0, DeerclopsEyeGrabState.ReleaseTick,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump
                | CutsceneInputLockFlags.UseItem | CutsceneInputLockFlags.UseTile
                | CutsceneInputLockFlags.Utility));
        }

        /// <summary>爪锚焦点：读演出主体的实时状态计时；主体失效退玩家中心防镜头瞬移</summary>
        private static Vector2 ClawFocus(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC deer) || !deer.active) {
                return context.PlayerCenter;
            }
            if (DeerclopsEyeGrabState.TryGetEyeGrabState(deer, out DeerclopsEyeGrabState state)) {
                return DeerclopsEyeGrabState.ClawAnchor(deer, state.Timer);
            }
            return deer.Center;
        }
    }
}
