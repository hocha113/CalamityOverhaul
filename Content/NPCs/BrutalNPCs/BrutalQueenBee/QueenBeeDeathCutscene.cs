using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee
{
    /// <summary>死亡运镜，对齐 QBDeathState 阶段帧</summary>
    internal sealed class QueenBeeDeathCutscene : CutsceneClip<NPC>
    {
        //死亡演出运镜优先级，高于普通演出
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = QBDeathState.TotalTime;

            //跟拍女王：痉挛紧跟→爬升略仰→坠落放松→志哀环收束
            timeline
                .Add(CameraFocusTrack.Follow(0, QBDeathState.ConvulseEnd,
                    QueenCenter, new Vector2(0f, 0f), 0.06f))
                .Add(CameraFocusTrack.Follow(QBDeathState.ConvulseEnd, ClimbLen,
                    QueenCenter, new Vector2(0f, -40f), 0.05f))
                .Add(CameraFocusTrack.Follow(QBDeathState.ClimbEnd, StallLen,
                    QueenCenter, new Vector2(0f, -20f), 0.09f))
                .Add(CameraFocusTrack.Follow(QBDeathState.StallEnd, FallLen,
                    QueenCenter, new Vector2(0f, 60f), 0.045f))
                .Add(CameraFocusTrack.Follow(QBDeathState.FallEnd, MournLen,
                    QueenCenter, new Vector2(0f, -60f), 0.06f))
                .Add(CameraFocusTrack.Follow(QBDeathState.MournEnd, FinaleLen,
                    QueenCenter, new Vector2(0f, -80f), 0.05f));

            //推拉节奏：痉挛渐入→爬升跟推→失速顶点最紧→坠落拉开→志哀半紧→散场退回
            timeline
                .Add(new CameraZoomTrack(0, QBDeathState.ConvulseEnd, 1f, 1.28f, 0.035f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(QBDeathState.ConvulseEnd, ClimbLen, 1.28f, 1.42f, 0.04f))
                .Add(new CameraZoomTrack(QBDeathState.ClimbEnd, StallLen, 1.42f, 1.6f, 0.09f))
                .Add(new CameraZoomTrack(QBDeathState.StallEnd, FallLen, 1.6f, 1.26f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(QBDeathState.FallEnd, MournLen, 1.26f, 1.46f, 0.045f))
                .Add(new CameraZoomTrack(QBDeathState.MournEnd, FinaleLen, 1.46f, 1.02f, 0.05f, CutsceneEase.CubicOut));

            //全程锁操作
            timeline.Add(new InputLockTrack(0, QBDeathState.TotalTime,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        private static int ClimbLen => QBDeathState.ClimbEnd - QBDeathState.ConvulseEnd;
        private static int StallLen => QBDeathState.StallEnd - QBDeathState.ClimbEnd;
        private static int FallLen => QBDeathState.FallEnd - QBDeathState.StallEnd;
        private static int MournLen => QBDeathState.MournEnd - QBDeathState.FallEnd;
        private static int FinaleLen => QBDeathState.TotalTime - QBDeathState.MournEnd;

        //主体失效时回退玩家中心，避免镜头瞬移
        private static Vector2 QueenCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC queen) && queen.active ? queen.Center : context.PlayerCenter;
    }
}
