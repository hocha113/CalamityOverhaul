using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼一阶段冲刺中状态：
    /// 全速段微弧追踪→末段急停甩头回正，配合速度拉伸残影
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismDashing, typeof(TwinsStateContext))]
    internal class SpazmatismDashingState : TwinsStateBase
    {
        public override string StateName => "SpazmatismDashing";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismDashing;

        private const int FullSpeedTime = 30;
        private const int BrakeTime = 12;
        private const int DashDuration = FullSpeedTime + BrakeTime;

        private int currentDashCount;
        private int maxDashCount;
        private int comboStep;

        public SpazmatismDashingState() : this(0, 2, 0) {
        }

        public SpazmatismDashingState(int dashCount, int maxCount, int currentComboStep = 0) {
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
            Player player = context.Target;

            Timer++;

            if (Timer <= FullSpeedTime) {
                //全速段:保持速度大小，带极小转向率的微弧追踪(擦身而过的压迫感)
                float speed = npc.velocity.Length();
                TwinsMotion.CurveChase(npc, player.Center, speed, 0.012f);
                FaceVelocity(npc);
                context.PushDashVisuals(1f, 1f);

                //火焰拖尾
                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 30f + Main.rand.NextVector2Circular(12, 12),
                        -npc.velocity * 0.15f, Color.White, Main.rand.NextFloat(1f, 1.6f))?.Configure(14, 1);
                }
            }
            else {
                //刹车段:急停甩头回正面向玩家，关闭碰撞伤害避免赖皮贴脸
                DisableContactDamage(npc);
                TwinsMotion.BrakeAndWhip(npc, player.Center, 0.8f, 0.3f);
                context.PushDashVisuals(0.3f, 0.6f);
            }

            //冲刺结束
            if (Timer >= DashDuration) {
                currentDashCount++;

                if (currentDashCount >= maxDashCount) {
                    //冲刺次数用完，回到悬停继续套路循环
                    return new SpazmatismHoverShootState(comboStep);
                }
                else {
                    //继续下一次冲刺准备
                    return new SpazmatismDashPrepareState(currentDashCount, comboStep);
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
