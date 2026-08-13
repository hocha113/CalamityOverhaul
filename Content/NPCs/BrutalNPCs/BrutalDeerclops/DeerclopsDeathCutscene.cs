using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops
{
    /// <summary>死亡运镜，对齐 DeerclopsDeathState 节拍常量</summary>
    internal sealed class DeerclopsDeathCutscene : CutsceneClip<NPC>
    {
        //死亡演出运镜优先级，高于普通演出
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = DeerclopsDeathState.TotalTime;

            int staggerLen = DeerclopsDeathState.StaggerEnd;
            int gazeLen = DeerclopsDeathState.GazeEnd - DeerclopsDeathState.StaggerEnd;
            int fallLen = DeerclopsDeathState.CollapseEnd - DeerclopsDeathState.GazeEnd;
            int dissolveLen = DeerclopsDeathState.TotalTime - DeerclopsDeathState.CollapseEnd;

            //聚焦躯干，坠地后压向足下
            timeline
                .Add(CameraFocusTrack.Follow(0, staggerLen, BodyCenter, new Vector2(0f, -20f), 0.05f))
                .Add(CameraFocusTrack.Follow(DeerclopsDeathState.StaggerEnd, gazeLen, BodyCenter, new Vector2(0f, -40f), 0.06f))
                .Add(CameraFocusTrack.Follow(DeerclopsDeathState.GazeEnd, fallLen, BodyCenter, new Vector2(0f, 20f), 0.08f))
                .Add(CameraFocusTrack.Follow(DeerclopsDeathState.CollapseEnd, dissolveLen, BodyCenter, new Vector2(0f, 30f), 0.05f));

            //推近至凝视顶点，坠地一瞬最紧，消散时缓缓退开
            timeline
                .Add(new CameraZoomTrack(0, staggerLen, 1f, 1.25f, 0.035f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(DeerclopsDeathState.StaggerEnd, gazeLen, 1.25f, 1.5f, 0.045f))
                .Add(new CameraZoomTrack(DeerclopsDeathState.GazeEnd, fallLen, 1.5f, 1.72f, 0.06f))
                .Add(new CameraZoomTrack(DeerclopsDeathState.CollapseEnd, dissolveLen, 1.72f, 1.2f, 0.04f, CutsceneEase.CubicOut));

            //全程锁定本地玩家操作
            timeline.Add(new InputLockTrack(0, DeerclopsDeathState.TotalTime,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        //演出主体失效时回退玩家中心，避免镜头瞬移世界原点
        private static Vector2 BodyCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC deer) && deer.active ? deer.Center : context.PlayerCenter;
    }
}
