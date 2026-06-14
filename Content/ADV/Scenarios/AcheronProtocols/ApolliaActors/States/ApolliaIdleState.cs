using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States
{
    /// <summary>
    /// 空闲/驻守状态：阿波利娅站在原地，面向玩家。 当指令切换为Follow且距离足够远时重新进入行走
    /// </summary>
    internal class ApolliaIdleState : IApolliaState
    {
        public void Enter(ApolliaActor actor) {
            actor.FrameIndex = 0;
            actor.UseJumpTexture = false;
        }

        public IApolliaState Update(ApolliaActor actor) {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return null;

            int dir = Math.Sign(player.Center.X - actor.Center.X);
            if (dir == 0) dir = -1;
            actor.WalkDirection = dir;

            actor.SnapToGround();

            if (actor.CurrentCommand == HeroCommand.Follow) {
                float dist = Math.Abs(actor.Center.X - player.Center.X);
                if (dist > 80f) {
                    return new ApolliaWalkingState();
                }
            }

            return null;
        }

        public void Exit(ApolliaActor actor) { }
    }
}
