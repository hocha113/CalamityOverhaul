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
        private int comboStep;

        public SpazmatismPhase2DashingState(int dashCount, int maxCount, int currentComboStep = 0) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            //冲刺状态启用碰撞伤害
            EnableContactDamage(context.Npc);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            //朝向速度方向
            FaceVelocity(npc);

            Timer++;

            //冲刺结束
            if (Timer >= DashDuration) {
                npc.velocity *= 0.4f;
                currentDashCount++;

                if (currentDashCount >= maxDashCount) {
                    //独眼模式下切换到狂暴状态
                    if (context.IsSoloRageMode) {
                        return new SpazmatismSoloRageState();
                    }

                    //冲刺次数用完，回到喷火追击继续套路循环
                    return new SpazmatismFlameChaseState(comboStep);
                }
                else {
                    //继续下一次冲刺
                    return new SpazmatismPhase2DashPrepareState(currentDashCount, comboStep);
                }
            }

            return null;
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            //离开冲刺状态禁用碰撞伤害
            DisableContactDamage(context.Npc);
        }
    }
}
