using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh
{
    /// <summary>
    /// 舌卷回吞运镜：只在被抓玩家客户端播放，时间零点=回卷开始。
    /// 回卷期镜头压在人与口之间，咀嚼期推近口器，吐出后跟随玩家飞出并松开
    /// </summary>
    internal sealed class WofGrabCutscene : CutsceneClip<NPC>
    {
        //低于死亡演出运镜(100)，可被其打断
        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int reel = WofDirector.GrabReelFrames;
            int chew = WofDirector.GrabChewFrames;
            int tail = WofDirector.GrabSpitTail + 26;
            timeline.Duration = reel + chew + tail;

            //焦点：人口中点→口器→飞出的玩家
            timeline
                .Add(CameraFocusTrack.Follow(0, reel, MidPoint, default, 0.09f))
                .Add(CameraFocusTrack.Follow(reel, chew, MouthPoint, new Vector2(0f, -12f), 0.08f))
                .Add(CameraFocusTrack.Follow(reel + chew, tail, PlayerPoint, default, 0.06f));

            //推近：回卷收拢，咀嚼最紧，吐出快速退开
            timeline
                .Add(new CameraZoomTrack(0, reel, 1f, 1.3f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(reel, chew, 1.3f, 1.55f, 0.045f))
                .Add(new CameraZoomTrack(reel + chew, tail, 1.55f, 1.05f, 0.06f, CutsceneEase.CubicOut));

            //控制锁到吐出为止：飞出段立即交还操作(位移接管同帧结束)
            timeline.Add(new InputLockTrack(0, reel + chew, CutsceneInputLockFlags.All));
        }

        /// <summary>口器保持点，墙失效时回退玩家中心防镜头瞬移</summary>
        private static Vector2 MouthPoint(CutsceneContext context)
            => context.TryGetSubject(out NPC wall) && wall.active
                ? WofTongueGrabState.MouthHold(wall) : context.PlayerCenter;

        private static Vector2 MidPoint(CutsceneContext context)
            => (MouthPoint(context) + context.PlayerCenter) * 0.5f;

        private static Vector2 PlayerPoint(CutsceneContext context) => context.PlayerCenter;
    }
}
