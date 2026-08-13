using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States.Fists;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>壁咚研磨投技运镜：只在被抓玩家本端播放，跟拳推近 → 束烙特写 → 研磨下行 → 终结震出
    /// 时间轴对齐 GolemFistGrabState 相位帧</summary>
    internal sealed class GolemGrabCutscene : CutsceneClip<NPC>
    {
        //低于死亡运镜(100)：死亡演出可顶替投技镜头
        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject) {
            if (!base.CanPlay(player, subject) || subject == null || !subject.active) {
                return false;
            }
            //只许被抓者本人起播
            GolemFistAI fistOverride = GolemFacts.FindOverride<GolemFistAI>(subject);
            return fistOverride != null
                && (int)fistOverride.ai[GolemAiSlots.FistGrabTarget] - 1 == player.whoAmI;
        }

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            //抓取 166 帧 + 释放余韵
            timeline.Duration = GolemFistGrabState.GrindEnd + 26;

            //跟拳：入镜快咬，连段稳跟，研磨贴身下行
            timeline
                .Add(CameraFocusTrack.Follow(0, 30, FistCenter, new Vector2(0f, -20f), 0.16f))
                .Add(CameraFocusTrack.Follow(30, GolemFistGrabState.PinEnd - 30, FistCenter, new Vector2(0f, -14f), 0.085f))
                .Add(CameraFocusTrack.Follow(GolemFistGrabState.PinEnd, timeline.Duration - GolemFistGrabState.PinEnd,
                    FistCenter, new Vector2(0f, 10f), 0.1f));

            //缩放：抓住猛推近 → 束烙再咬一口 → 终结后放回
            timeline
                .Add(new CameraZoomTrack(0, 26, 1f, 1.32f, 0.09f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(26, 84, 1.32f, 1.38f, 0.04f))
                .Add(new CameraZoomTrack(110, 32, 1.38f, 1.46f, 0.06f))
                .Add(new CameraZoomTrack(142, 24, 1.46f, 1.34f, 0.05f))
                .Add(new CameraZoomTrack(GolemFistGrabState.GrindEnd, 24, 1.34f, 1.1f, 0.06f));

            //锁定操作直至释放帧（释放后立刻归还操控接飞行姿态）
            timeline.Add(new InputLockTrack(0, GolemFistGrabState.GrindEnd + 4, CutsceneInputLockFlags.All));

            //节拍震：撞墙 / 束烙命中 / 终结重砸
            timeline
                .Add(new CameraShakeTrack(GolemFistGrabState.DragEnd - 2, Vector2.UnitY, 7f, 0.88f, 18))
                .Add(new CameraShakeTrack(118, Vector2.Zero, 4.5f, 0.9f, 14))
                .Add(new CameraShakeTrack(GolemFistGrabState.GrindEnd - 2, Vector2.UnitY, 10f, 0.88f, 22));
        }

        //演出主体失效时回退玩家中心
        private static Vector2 FistCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC fist) && fist.active ? fist.Center : context.PlayerCenter;
    }
}
