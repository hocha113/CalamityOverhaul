using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>终焉时刻运镜：坍缩缓推近→内爆紧咬→死寂定格→超新星急拉远→余烬缓释</summary>
    internal sealed class MLordDeathCutscene : CutsceneClip<NPC>
    {
        //死亡演出运镜优先级，高于普通演出
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = MLordDeathState.PhaseEmbersEnd;

            //聚焦核心，各阶段焦点缓缓下压再回中
            timeline
                .Add(CameraFocusTrack.Follow(0, MLordDeathState.PhaseCollapseEnd,
                    CoreCenter, new Vector2(0f, -40f), 0.05f))
                .Add(CameraFocusTrack.Follow(MLordDeathState.PhaseCollapseEnd, ImplosionLen,
                    CoreCenter, new Vector2(0f, 0f), 0.06f))
                .Add(CameraFocusTrack.Follow(MLordDeathState.PhaseImplosionEnd, SilenceLen,
                    CoreCenter, new Vector2(0f, 0f), 0.1f))
                .Add(CameraFocusTrack.Follow(MLordDeathState.PhaseSilenceEnd, SupernovaLen,
                    CoreCenter, new Vector2(0f, 0f), 0.08f))
                .Add(CameraFocusTrack.Follow(MLordDeathState.PhaseSupernovaEnd, EmbersLen,
                    CoreCenter, new Vector2(0f, 30f), 0.045f));

            //缩放：坍缩 1→1.35，内爆咬到 1.6，死寂定格，超新星急退 1.15，余烬缓释回 1
            timeline
                .Add(new CameraZoomTrack(0, MLordDeathState.PhaseCollapseEnd, 1f, 1.35f, 0.04f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(MLordDeathState.PhaseCollapseEnd, ImplosionLen, 1.35f, 1.6f, 0.05f))
                .Add(new CameraZoomTrack(MLordDeathState.PhaseImplosionEnd, SilenceLen, 1.6f, 1.62f, 0.08f))
                .Add(new CameraZoomTrack(MLordDeathState.PhaseSilenceEnd, SupernovaLen, 1.62f, 1.15f, 0.09f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(MLordDeathState.PhaseSupernovaEnd, EmbersLen, 1.15f, 1f, 0.03f, CutsceneEase.CubicOut));

            //锁移动与用物，保留观演视角
            timeline.Add(new InputLockTrack(0, MLordDeathState.PhaseEmbersEnd,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        private static int ImplosionLen => MLordDeathState.PhaseImplosionEnd - MLordDeathState.PhaseCollapseEnd;
        private static int SilenceLen => MLordDeathState.PhaseSilenceEnd - MLordDeathState.PhaseImplosionEnd;
        private static int SupernovaLen => MLordDeathState.PhaseSupernovaEnd - MLordDeathState.PhaseSilenceEnd;
        private static int EmbersLen => MLordDeathState.PhaseEmbersEnd - MLordDeathState.PhaseSupernovaEnd;

        //演出主体失效时回退玩家中心，避免镜头瞬移世界原点
        private static Vector2 CoreCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC core) && core.active ? core.Center : context.PlayerCenter;
    }
}
