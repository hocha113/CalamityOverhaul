using CalamityOverhaul.Common;
using InnoVault.Cinematics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 深渊复苏死亡演出运镜——基于 InnoVault 演出系统（替代旧的手写 ModifyScreenPosition 抖动）。
    /// <para>以 <see cref="ResurrectionDeath"/> 为演出主体：镜头跟随被深渊吞噬下坠的玩家，
    /// 屏幕震动强度逐帧读取演出状态机推导出的 <see cref="ResurrectionDeath.ShakeIntensity"/> 并叠加。
    /// 演出不定长，由死亡状态机在结束/重置时主动 <see cref="CutsceneDirector.Stop"/> 收尾。</para>
    /// <para>玩家控制锁定仍由 <see cref="ResurrectionDeath"/> 自身的 DisablePlayerControls（含 noItems/noBuilding）处理，
    /// 故本演出不再额外请求输入锁定。</para>
    /// </summary>
    internal sealed class ResurrectionDeathCutscene : CutsceneClip<ResurrectionDeath>
    {
        //死亡演出（警告 + 死亡动画 + 执行 + 冷却）的保护上限，实际由状态机主动 Stop
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

            //镜头跟随下坠的玩家（等价原版镜头，但纳入演出统一管理）
            context.SetCameraFocus(player.Center, 0.15f);

            //屏幕震动跟随状态机强度——每帧刷新短脉冲，形成与原随机抖动一致的持续抖动
            if (context.TryGetSubject(out ResurrectionDeath death) && death.ShakeIntensity > 0.5f) {
                context.Shake(Vector2.Zero, death.ShakeIntensity, 0.9f, 3);
            }
        }
    }
}
