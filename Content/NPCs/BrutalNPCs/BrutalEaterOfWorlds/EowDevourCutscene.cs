using CalamityOverhaul.Common;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds
{
    /// <summary>
    /// 生吞入腹运镜：仅被吞玩家本机播放。咬合急推→随头入地(转暗由滤镜负责)→
    /// 腹内紧贴跟随→喷出后镜头交还给飞行中的玩家，由 EowDevourPlayer 延迟收束
    /// </summary>
    internal sealed class EowDevourCutscene : CutsceneClip<NPC>
    {
        /// <summary>时长上限(实际由 EowDevourPlayer 按释放时机提前停止)</summary>
        internal const int TotalTime = 240;

        //低于死亡演出运镜(死亡演出可顶掉投技镜头)
        public override int Priority => 80;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = TotalTime;

            //全程动态轨：被衔期贴头，释放后交还玩家；锁输入只在被衔期请求
            timeline.Add(new DynamicCameraTrack(0, TotalTime, PerFrame));

            //咬合急推→腹内缓推→喷出回拉
            timeline
                .Add(new CameraZoomTrack(0, 30, 1f, 1.3f, 0.06f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(30, 130, 1.3f, 1.42f, 0.03f))
                .Add(new CameraZoomTrack(160, 80, 1.42f, 1.08f, 0.05f, CutsceneEase.QuadInOut));
        }

        private static void PerFrame(CutsceneContext context) {
            EowDevourPlayer devour = Main.LocalPlayer.GetModPlayer<EowDevourPlayer>();
            if (devour.Pinned && context.TryGetSubject(out NPC head) && head.active) {
                context.SetCameraFocus(head.Center, 0.16f);
                context.RequestInputLock(CutsceneInputLockFlags.All);
            }
            else {
                //释放后：跟随被喷飞的玩家，不再锁任何输入
                context.SetCameraFocus(context.PlayerCenter, 0.12f);
            }
        }
    }
}
