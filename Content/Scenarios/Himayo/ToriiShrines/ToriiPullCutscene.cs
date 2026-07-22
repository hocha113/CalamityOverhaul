using InnoVault.Actors;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 拔刀仪式运镜，主体=鸟居Actor.WhoAmI<br/>
    /// 刀动画由 <see cref="ToriiShrineActor"/> PullRite 驱动，本片段只管镜头与输入锁，时长共享 <see cref="ToriiShrineActor.RiteCutsceneFrames"/>
    /// </summary>
    internal sealed class ToriiPullCutscene : CutsceneClip<int>
    {
        public override int Priority => 40;

        public override bool CanPlay(Player player, int whoAmI) => ResolveActor(whoAmI) != null;

        private static ToriiShrineActor ResolveActor(int whoAmI) {
            foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                if (actor.WhoAmI == whoAmI) {
                    return actor;
                }
            }
            return null;
        }

        private static Vector2 SwordFocus(CutsceneContext ctx) {
            if (ctx.TryGetSubject(out int whoAmI) && ResolveActor(whoAmI) is ToriiShrineActor actor) {
                return actor.SwordAnchor;
            }
            return ctx.PlayerCenter;
        }

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = ToriiShrineActor.RiteCutsceneFrames;
            timeline.Duration = total;

            timeline
                //全程锁输入+短无敌
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //刀-玩家中点，上抬收横梁
                .Add(CameraFocusTrack.Midpoint(0, total, SwordFocus, c => c.PlayerCenter, new Vector2(0f, -30f), 0.06f))
                //拉近后回拉，驻留看黄昏渐入
                .Add(new CameraZoomTrack(0, 50, 1f, 1.35f, 0.045f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(ToriiShrineActor.RiteFrames + 10, total - ToriiShrineActor.RiteFrames - 10,
                    1.35f, 1f, 0.05f, CutsceneEase.CubicOut));
        }
    }
}
