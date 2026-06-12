using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段冲刺中状态：
    /// 更快的弧线追踪冲刺与更猛的急停甩头
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismPhase2Dashing, typeof(TwinsStateContext))]
    internal class SpazmatismPhase2DashingState : TwinsStateBase
    {
        public override string StateName => "SpazmatismPhase2Dashing";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismPhase2Dashing;

        private const int FullSpeedTime = 24;
        private const int BrakeTime = 10;
        private const int DashDuration = FullSpeedTime + BrakeTime;

        private int currentDashCount;
        private int maxDashCount;
        private int comboStep;

        public SpazmatismPhase2DashingState() : this(0, 4, 0) {
        }

        public SpazmatismPhase2DashingState(int dashCount, int maxCount, int currentComboStep = 0) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            //冲刺状态启用碰撞伤害
            EnableContactDamage(context.Npc);
            //冲刺启动帧天空闪雷
            MachineEffect.TriggerSkyFlash(context.Npc.Center, 0.6f);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            Timer++;

            if (Timer <= FullSpeedTime) {
                //全速段:微弧追踪
                float speed = npc.velocity.Length();
                TwinsMotion.CurveChase(npc, player.Center, speed, 0.016f);
                FaceVelocity(npc);
                context.PushDashVisuals(1f, 1f);

                //炽热拖尾
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 34f + Main.rand.NextVector2Circular(14, 14),
                        -npc.velocity * 0.18f, Color.White, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(15, 1);
                }
            }
            else {
                //急停甩头
                DisableContactDamage(npc);
                TwinsMotion.BrakeAndWhip(npc, player.Center, 0.76f, 0.34f);
                context.PushDashVisuals(0.35f, 0.65f);
            }

            //冲刺结束
            if (Timer >= DashDuration) {
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
