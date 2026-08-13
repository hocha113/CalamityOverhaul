using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron
{
    /// <summary>
    /// 合掌拍捉运镜：只在被抓玩家的客户端播放（SkeletronSnatchPlayer 启停）<br/>
    /// 时间轴自受害端观察到夹持起算：推近凝笼→环轰凝视→蓄势再压→砸地拉开<br/>
    /// 优先级低于死亡演出，可被其抢占；提前释放由玩家侧 Stop 平滑收尾
    /// </summary>
    internal sealed class SkeletronSnatchCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 60;

        /// <summary>覆盖夹持→恢复全程的兜底时长（提前释放走 Stop）</summary>
        internal const int ClipLength = 270;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = ClipLength;

            //全程跟随囚笼（双掌中点），偏上留出颅骨入画
            timeline.Add(CameraFocusTrack.Follow(0, ClipLength, CageCenter, new Vector2(0f, -40f), 0.1f));

            //夹持推近急punch→举升缓推→环轰凝视→蓄势再压→砸地急拉开→回稳
            timeline
                .Add(new CameraZoomTrack(0, 14, 1f, 1.3f, 0.22f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(14, 46, 1.3f, 1.34f, 0.05f))
                .Add(new CameraZoomTrack(60, 120, 1.34f, 1.38f, 0.03f))
                .Add(new CameraZoomTrack(180, 26, 1.38f, 1.46f, 0.06f))
                .Add(new CameraZoomTrack(206, 30, 1.46f, 1.1f, 0.12f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(236, 34, 1.1f, 1f, 0.06f));

            //全程锁常用输入（含物品与钩爪坐骑类辅助动作）
            timeline.Add(new InputLockTrack(0, ClipLength, CutsceneInputLockFlags.All));
        }

        //演出主体失效时回退玩家中心，防镜头瞬移世界原点
        private static Vector2 CageCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC head) && head.active
                ? SkeletronPalmSnatchState.GetCageCenter(head)
                : context.PlayerCenter;
    }
}
