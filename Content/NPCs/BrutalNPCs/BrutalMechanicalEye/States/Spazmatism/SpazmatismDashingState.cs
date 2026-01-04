using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼一阶段冲刺中状态
    /// </summary>
    internal class SpazmatismDashingState : TwinsStateBase
    {
        public override string StateName => "SpazmatismDashing";

        private const int DashDuration = 40;
        private const int MaxDashCount = 2;

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;

            //朝向速度方向
            FaceVelocity(npc);

            Timer++;

            //冲刺结束
            if (Timer >= DashDuration) {
                npc.velocity *= 0.5f;
                Counter++;

                if (Counter >= MaxDashCount) {
                    //冲刺次数用完，回到悬停
                    return new SpazmatismHoverShootState();
                }
                else {
                    //继续下一次冲刺
                    return new SpazmatismDashPrepareState();
                }
            }

            return null;
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            context.Npc.velocity *= 0.5f;
        }
    }
}
