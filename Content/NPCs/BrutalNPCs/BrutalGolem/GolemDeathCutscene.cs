using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>石像崩解死亡运镜，对齐 GolemDeathState 阶段帧</summary>
    internal sealed class GolemDeathCutscene : CutsceneClip<NPC>
    {
        //死亡演出运镜优先级，高于普通演出
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = GolemDeathState.FinaleEnd;

            //聚焦躯干，谢幕段轻微上移看宝石
            timeline
                .Add(CameraFocusTrack.Follow(0, GolemDeathState.StaggerEnd,
                    BodyCenter, new Vector2(0f, 0f), 0.045f))
                .Add(CameraFocusTrack.Follow(GolemDeathState.StaggerEnd, CrackLen,
                    BodyCenter, new Vector2(0f, -10f), 0.055f))
                .Add(CameraFocusTrack.Follow(GolemDeathState.CrackEnd, CollapseLen,
                    BodyCenter, new Vector2(0f, 10f), 0.07f))
                .Add(CameraFocusTrack.Follow(GolemDeathState.CollapseEnd, FinaleLen,
                    BodyCenter, new Vector2(0f, -70f), 0.06f));

            //缩放单调推进至宝石谢幕（1.8x）
            timeline
                .Add(new CameraZoomTrack(0, GolemDeathState.StaggerEnd, 1f, 1.25f, 0.03f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(GolemDeathState.StaggerEnd, CrackLen, 1.25f, 1.45f, 0.045f))
                .Add(new CameraZoomTrack(GolemDeathState.CrackEnd, CollapseLen, 1.45f, 1.6f, 0.05f))
                .Add(new CameraZoomTrack(GolemDeathState.CollapseEnd, FinaleLen, 1.6f, 1.8f, 0.055f));

            //全程锁定本地玩家操作
            timeline.Add(new InputLockTrack(0, GolemDeathState.FinaleEnd,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        private static int CrackLen => GolemDeathState.CrackEnd - GolemDeathState.StaggerEnd;
        private static int CollapseLen => GolemDeathState.CollapseEnd - GolemDeathState.CrackEnd;
        private static int FinaleLen => GolemDeathState.FinaleEnd - GolemDeathState.CollapseEnd;

        //演出主体失效时回退玩家中心
        private static Vector2 BodyCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC body) && body.active ? body.Center : context.PlayerCenter;
    }
}
