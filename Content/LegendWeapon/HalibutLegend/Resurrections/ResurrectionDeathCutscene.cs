using CalamityOverhaul.Common;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 复苏死亡演出运镜，基于 InnoVault CutsceneDirector
    /// 镜头跟随 <see cref="ResurrectionDeath"/> 主体，震动读 <see cref="ResurrectionDeath.ShakeIntensity"/>
    /// 时长不定，由死亡状态机结束时 <see cref="CutsceneDirector.Stop"/> 收尾
    /// 输入锁定仍由 <see cref="ResurrectionDeath"/> 的 DisablePlayerControls 承担
    /// </summary>
    internal sealed class ResurrectionDeathCutscene : CutsceneClip<ResurrectionDeath>
    {
        //死亡全流程保护上限，实际由状态机主动 Stop
        private const int MaxFrames = 60 * 12;

        public override int Priority => 100;

        public override bool CanPlay(Player player, ResurrectionDeath subject)
            => base.CanPlay(player, subject) && subject != null;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = MaxFrames;
            timeline.Add(new DynamicCameraTrack(0, MaxFrames, DriveCamera));
        }

        private static void DriveCamera(CutsceneContext context) {
            Player player = context.Player;
            if (player == null || !player.active) {
                return;
            }

            //镜头跟随下坠玩家
            context.SetCameraFocus(player.Center, 0.15f);

            //按状态机强度叠加屏幕震动
            if (context.TryGetSubject(out ResurrectionDeath death) && death.ShakeIntensity > 0.5f) {
                context.Shake(Vector2.Zero, death.ShakeIntensity, 0.9f, 3);
            }
        }
    }
}
