using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron
{
    /// <summary>诅咒崩解死亡运镜，对齐 SkeletronDeathState 时间线</summary>
    internal sealed class SkeletronDeathCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = SkeletronDeathState.DeathEnd;

            //跟随坠落的颅骨；剥离段稍抬看诅咒升起
            timeline
                .Add(CameraFocusTrack.Follow(0, SkeletronDeathState.FallEnd,
                    HeadCenter, new Vector2(0f, 30f), 0.05f))
                .Add(CameraFocusTrack.Follow(SkeletronDeathState.FallEnd, LamentLen,
                    HeadCenter, new Vector2(0f, 0f), 0.06f))
                .Add(CameraFocusTrack.Follow(SkeletronDeathState.LamentEnd, CradleLen,
                    HeadCenter, new Vector2(0f, -20f), 0.07f))
                .Add(CameraFocusTrack.Follow(SkeletronDeathState.CradleEnd, StripLen,
                    HeadCenter, new Vector2(0f, -70f), 0.08f))
                .Add(CameraFocusTrack.Follow(SkeletronDeathState.StripEnd, TailLen,
                    HeadCenter, new Vector2(0f, -30f), 0.06f));

            //推近至剥离顶点，新星后拉开看全景
            timeline
                .Add(new CameraZoomTrack(0, SkeletronDeathState.FallEnd, 1f, 1.25f, 0.03f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(SkeletronDeathState.FallEnd, LamentLen, 1.25f, 1.45f, 0.04f))
                .Add(new CameraZoomTrack(SkeletronDeathState.LamentEnd, CradleLen, 1.45f, 1.65f, 0.05f))
                .Add(new CameraZoomTrack(SkeletronDeathState.CradleEnd, StripLen, 1.65f, 1.95f, 0.06f))
                .Add(new CameraZoomTrack(SkeletronDeathState.StripEnd, TailLen, 1.95f, 1.35f, 0.05f, CutsceneEase.CubicOut));

            //全程锁定本地玩家操作（沿基准做法）
            timeline.Add(new InputLockTrack(0, SkeletronDeathState.DeathEnd,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        private static int LamentLen => SkeletronDeathState.LamentEnd - SkeletronDeathState.FallEnd;
        private static int CradleLen => SkeletronDeathState.CradleEnd - SkeletronDeathState.LamentEnd;
        private static int StripLen => SkeletronDeathState.StripEnd - SkeletronDeathState.CradleEnd;
        private static int TailLen => SkeletronDeathState.DeathEnd - SkeletronDeathState.StripEnd;

        //演出主体失效时回退玩家中心，防镜头瞬移世界原点
        private static Vector2 HeadCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC head) && head.active ? head.Center : context.PlayerCenter;
    }
}
