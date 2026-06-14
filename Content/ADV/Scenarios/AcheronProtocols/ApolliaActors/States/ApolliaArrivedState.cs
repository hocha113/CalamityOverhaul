using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States
{
    /// <summary>
    /// 到达状态：阿波利娅站在目标附近，根据指令决定后续行为： Follow：玩家远离时重新行走；Hold：切换到空闲
    /// </summary>
    internal class ApolliaArrivedState : IApolliaState
    {
        private const float ReWalkDistance = 120f;

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

            if (actor.CurrentCommand == HeroCommand.Hold) {
                return new ApolliaIdleState();
            }

            float distToPlayer = Math.Abs(actor.Center.X - player.Center.X);
            if (distToPlayer > ReWalkDistance) {
                return new ApolliaWalkingState();
            }

            return null;
        }

        public void Exit(ApolliaActor actor) { }
    }
}
