using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera
{
    /// <summary>
    /// 绞藤飨宴运镜：只在被抓玩家客户端播放(tick0=拖拽开始)。
    /// 拖拽段镜头取双方中点缓推近；咀嚼段贴巨口最紧；
    /// 吐飞段追被抛者并松镜。锁输入到弹射帧为止，与玩家侧控制交还同拍
    /// </summary>
    internal sealed class PlanteraVineFeastCutscene : CutsceneClip<NPC>
    {
        //低于死亡运镜(100)：死亡演出可打断投技运镜
        public override int Priority => 60;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int drag = PlanteraVineFeastState.DragTime;
            int chew = PlanteraVineFeastState.ChewTime;
            int spit = PlanteraVineFeastState.SpitTime;
            //锁输入止于弹射帧：控制权与玩家侧钉身同拍交还
            int lockLen = drag + chew + PlanteraVineFeastState.SpitYeetTick;

            timeline.Duration = drag + chew + spit;

            timeline
                .Add(CameraFocusTrack.Midpoint(0, drag, BossCenter, PreyCenter, default, 0.08f))
                .Add(CameraFocusTrack.Follow(drag, chew, MawFocus, default, 0.07f))
                .Add(CameraFocusTrack.Follow(drag + chew, spit, PreyCenter, new Vector2(0f, -30f), 0.09f));

            timeline
                .Add(new CameraZoomTrack(0, drag, 1f, 1.2f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(drag, chew, 1.2f, 1.46f, 0.04f))
                .Add(new CameraZoomTrack(drag + chew, spit, 1.46f, 1.05f, 0.06f, CutsceneEase.CubicOut));

            timeline.Add(new InputLockTrack(0, lockLen, CutsceneInputLockFlags.All));
        }

        //主体失效时回退玩家中心，避免镜头瞬移世界原点
        private static Vector2 BossCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC boss) && boss.active ? boss.Center : context.PlayerCenter;

        private static Vector2 PreyCenter(CutsceneContext context) => context.PlayerCenter;

        //咀嚼段焦点：巨口偏向被抓者一点，让双方都在画面里
        private static Vector2 MawFocus(CutsceneContext context)
            => Vector2.Lerp(BossCenter(context), context.PlayerCenter, 0.3f);
    }
}
