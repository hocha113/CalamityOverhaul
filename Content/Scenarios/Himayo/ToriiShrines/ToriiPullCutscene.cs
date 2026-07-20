using InnoVault.Actors;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 拔刀仪式运镜（InnoVault Cinematics）：主体为鸟居 Actor 的 WhoAmI。
    /// 镜头聚焦刀与玩家的中点并拉近，仪式收尾后驻留一段看着黄昏渐入再交还控制权；
    /// 世界侧的刀动画/交付/迸发全部由 <see cref="ToriiShrineActor"/> 的 PullRite 相位驱动，
    /// 本片段只负责镜头与输入锁，两边共享 <see cref="ToriiShrineActor.RiteCutsceneFrames"/> 时长
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
                //全程锁输入：仪式很短，配合逐帧短无敌不给锁死留机会
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //镜头跟刀-玩家中点，上抬一点把鸟居横梁收进画面
                .Add(CameraFocusTrack.Midpoint(0, total, SwordFocus, c => c.PlayerCenter, new Vector2(0f, -30f), 0.06f))
                //拉近对准拔刀，仪式收尾后回拉，驻留段正好看着天色转入黄昏
                .Add(new CameraZoomTrack(0, 50, 1f, 1.35f, 0.045f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(ToriiShrineActor.RiteFrames + 10, total - ToriiShrineActor.RiteFrames - 10,
                    1.35f, 1f, 0.05f, CutsceneEase.CubicOut));
        }
    }
}
