using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States;
using InnoVault.Cinematics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 阿波利娅登场演出运镜：基于 InnoVault 演出系统。 降落时聚焦落点、引路行走时聚焦角色与玩家中点并按水平距离动态缩放、到达后拉近定格； 全程锁定本地玩家操作（围观登场）。着陆震动经 <see cref="CutsceneDirector.Shake"/> 由降落状态触发。</para> 或离开子世界时主动 <see cref="CutsceneDirector.Stop"/> 收尾并平滑恢复镜头。</para>
    /// </summary>
    internal sealed class ApolliaCutscene : CutsceneClip<ApolliaActor>
    {
        //不定长演出的保护上限（约 30 分钟），实际由外部 Stop 收尾
        internal const int MaxFrames = 60 * 60 * 30;

        public override int Priority => 50;

        public override bool CanPlay(Player player, ApolliaActor subject)
            => base.CanPlay(player, subject) && subject != null && subject.Active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = MaxFrames;
            timeline.Add(new DynamicCameraTrack(0, MaxFrames, DriveCamera));
            timeline.Add(new InputLockTrack(0, MaxFrames,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        //复刻旧 CutsceneCamera.UpdateFocus：运镜逻辑集中于此，状态类无需感知镜头
        private static void DriveCamera(CutsceneContext context) {
            if (!context.TryGetSubject(out ApolliaActor actor) || !actor.Active) {
                return;
            }
            Player player = context.Player;
            if (player == null || !player.active) {
                return;
            }

            switch (actor.CurrentState) {
                case ApolliaDescendingState:
                    context.SetCameraFocus(actor.Center, 0.03f);
                    context.SetCameraZoom(1f, 0.02f);
                    break;

                case ApolliaWalkingState: {
                    Vector2 midPoint = (actor.Center + player.Center) * 0.5f;
                    context.SetCameraFocus(midPoint, 0.025f);

                    float distX = Math.Abs(actor.Center.X - player.Center.X);
                    float zoomFactor = MathHelper.Clamp(1f - (distX - 60f) / 400f, 0f, 1f);
                    float eased = zoomFactor < 0.5f
                        ? 2f * zoomFactor * zoomFactor
                        : 1f - MathF.Pow(-2f * zoomFactor + 2f, 2f) / 2f;
                    context.SetCameraZoom(MathHelper.Lerp(1f, 1.5f, eased), 0.015f);
                    break;
                }

                case ApolliaArrivedState:
                    context.SetCameraFocus((actor.Center + player.Center) * 0.5f + new Vector2(0, -20), 0.04f);
                    context.SetCameraZoom(2f, 0.02f);
                    break;
            }
        }
    }
}
