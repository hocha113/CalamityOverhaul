using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using Terraria;
using Terraria.ID;

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

                    //冲刺次数用完，随机切换到特殊招式
                    int choice = Main.rand.Next(4);
                    return choice switch {
                        0 => new SpazmatismShadowDashState(),
                        1 => new SpazmatismFlameStormState(),
                        2 => HasPartner() ? new TwinsCombinedAttackState() : new SpazmatismFlameChaseState(),
                        _ => new SpazmatismFlameChaseState()
                    };
                }
                else {
                    //继续下一次冲刺
                    return new SpazmatismPhase2DashPrepareState(currentDashCount);
                }
            }

            return null;
        }

        /// <summary>
        /// 检查是否有另一只眼睛存活
        /// </summary>
        private bool HasPartner() {
            foreach (var n in Main.npc) {
                if (n.active && n.type == NPCID.Retinazer) {
                    return true;
                }
            }
            return false;
        }
    }
}
