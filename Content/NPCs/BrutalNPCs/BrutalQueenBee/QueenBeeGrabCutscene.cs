using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee
{
    /// <summary>
    /// 投技·蜜牢收网运镜：只在被抓玩家的客户端播放(QueenBeeGrabPlayer 启停)<br/>
    /// clip tick 0 ≈ 状态 Timer 的成茧帧(CloseEnd)，释放沿由玩家侧立即 Stop
    /// </summary>
    internal sealed class QueenBeeGrabCutscene : CutsceneClip<NPC>
    {
        //低于死亡演出(100)：同屏冲突时死亡运镜优先
        public override int Priority => 60;

        /// <summary>钉位窗全长(成茧→爆散)+释放余韵</summary>
        internal const int ClipDuration = QBSwarmLiftState.DetonateTick - QBSwarmLiftState.CloseEnd + 30;

        //各段在clip空间的换算(clip 0 = 状态CloseEnd)
        private static int LiftEndClip => QBSwarmLiftState.LiftEnd - QBSwarmLiftState.CloseEnd;
        private static int PassEndClip => QBSwarmLiftState.PassEnd - QBSwarmLiftState.CloseEnd;
        private static int DetonateClip => QBSwarmLiftState.DetonateTick - QBSwarmLiftState.CloseEnd;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = ClipDuration;

            //跟拍茧与女王的加权中点(茧为主)，任一失效回退玩家中心
            timeline.Add(CameraFocusTrack.Follow(0, ClipDuration, GrabFocus, new Vector2(0f, -20f), 0.09f));

            //推拉：抓住猛推近→抬升缓推→穿刺期屏息微松→爆散前静默再收紧
            timeline
                .Add(new CameraZoomTrack(0, 18, 1f, 1.32f, 0.1f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(18, LiftEndClip - 18, 1.32f, 1.44f, 0.045f))
                .Add(new CameraZoomTrack(LiftEndClip, PassEndClip - LiftEndClip, 1.44f, 1.38f, 0.05f))
                .Add(new CameraZoomTrack(PassEndClip, DetonateClip - PassEndClip, 1.38f, 1.52f, 0.07f));

            //全程锁常用输入(移动/跳跃/道具/交互/钩爪坐骑)，锁随clip被Stop一并解除
            timeline.Add(new InputLockTrack(0, ClipDuration, CutsceneInputLockFlags.All));
        }

        /// <summary>镜头焦点：茧心70%+女王30%；状态失效时回退玩家自身</summary>
        private static Vector2 GrabFocus(CutsceneContext context) {
            if (context.TryGetSubject(out NPC queen) && queen.active
                && queen.TryGetOverride(out BrutalQueenBeeAI queenAI)
                && queenAI.Machine?.CurrentState is QBSwarmLiftState lift) {
                return Vector2.Lerp(lift.CocoonCenter, queen.Center, 0.3f);
            }
            return context.PlayerCenter;
        }
    }
}
