using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段冲刺中状态
    /// </summary>
    internal class SpazmatismPhase2DashingState : TwinsStateBase
    {
        public override string StateName => "SpazmatismPhase2Dashing";

        private const int DashDuration = 30;

        private int currentDashCount;
        private int maxDashCount;

        public SpazmatismPhase2DashingState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;

            //朝向速度方向
            FaceVelocity(npc);

            Timer++;

            //冲刺结束
            if (Timer >= DashDuration) {
                npc.velocity *= 0.4f;
                currentDashCount++;

                if (currentDashCount >= maxDashCount) {
                    //冲刺次数用完，回到喷火
                    return new SpazmatismFlameChaseState();
                }
                else {
                    //继续下一次冲刺
                    return new SpazmatismPhase2DashPrepareState(currentDashCount);
                }
            }

            return null;
        }
    }
}
