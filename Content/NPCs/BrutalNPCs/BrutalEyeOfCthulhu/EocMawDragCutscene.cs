using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    /// <summary>
    /// 撕咬拖曳运镜：只在被抓者本机播放（由 <see cref="EocGrabPerformancePlayer"/> 启停）。<br/>
    /// 咬合推近→拖行放宽读地速→抬升收紧→砸地后退镜看陨坑；输入锁不走运镜轨道，
    /// 由 PerformancePlayer 的 SetControls 精确随抓取态启停
    /// </summary>
    internal sealed class EocMawDragCutscene : CutsceneClip<NPC>
    {
        //低于死亡运镜(100)，投技撞上死亡演出时让位
        public override int Priority => 60;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = 320;

            //全程跟拍：抓取中压在眼与玩家之间并向行进方向提前，释放后回落玩家看陨坑
            timeline.Add(CameraFocusTrack.Follow(0, 320, FocusPoint, Vector2.Zero, 0.16f));

            //咬合猛推近→拖行微放宽→上扬砸地收紧→释放缓缓退开
            timeline
                .Add(new CameraZoomTrack(0, 18, 1f, 1.4f, 0.09f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(18, 86, 1.4f, 1.28f, 0.04f))
                .Add(new CameraZoomTrack(104, 44, 1.28f, 1.52f, 0.05f))
                .Add(new CameraZoomTrack(148, 172, 1.52f, 1.12f, 0.045f, CutsceneEase.CubicOut));
        }

        private static Vector2 FocusPoint(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC eye) || !eye.active) {
                return context.PlayerCenter;
            }
            bool holding = (int)eye.ai[2] == (int)EocStateIndex.MawDrag && (int)eye.ai[3] != 0;
            if (holding) {
                return Vector2.Lerp(context.PlayerCenter, eye.Center, 0.45f) + eye.velocity * 5f;
            }
            return context.PlayerCenter;
        }
    }
}
