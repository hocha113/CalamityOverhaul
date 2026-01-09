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

        private int currentDashCount;
        private int maxDashCount;

        public SpazmatismDashingState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            //冲刺状态启用碰撞伤害
            EnableContactDamage(context.Npc);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;

            //朝向速度方向
            FaceVelocity(npc);

            Timer++;

            //冲刺结束
            if (Timer >= DashDuration) {
                npc.velocity *= 0.5f;
                currentDashCount++;

                if (currentDashCount >= maxDashCount) {
                    //冲刺次数用完，回到悬停
                    return new SpazmatismHoverShootState();
                }
                else {
                    //继续下一次冲刺准备
                    return new SpazmatismDashPrepareState(currentDashCount);
                }
            }

            return null;
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            context.Npc.velocity *= 0.5f;
            //离开冲刺状态禁用碰撞伤害
            DisableContactDamage(context.Npc);
        }
    }
}
