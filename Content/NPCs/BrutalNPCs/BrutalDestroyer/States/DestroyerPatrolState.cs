using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 巡空状态：椭圆轨迹盘旋，带高度起伏
    /// </summary>
    internal class DestroyerPatrolState : DestroyerStateBase
    {
        public override string StateName => "Patrol";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Patrol;

        private int PatrolDuration(DestroyerStateContext ctx) => ctx.IsEnraged ? 240 : 300;

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            float patrolTime = Timer * 0.015f;
            float horizontalRadius = 900f;
            float verticalRadius = 400f;
            float offsetX = (float)Math.Cos(patrolTime) * horizontalRadius;
            float offsetY = (float)Math.Sin(patrolTime * 1.3f) * verticalRadius - 300f;

            //渐进加速
            float accelProgress = Math.Min(Timer / 90f, 1f);
            float speed = MathHelper.Lerp(10f, context.IsEnraged ? 22f : 18f, accelProgress);
            float turnSpeed = MathHelper.Lerp(0.2f, 0.5f, accelProgress);

            SetMovement(context, player.Center + new Vector2(offsetX, offsetY), speed, turnSpeed);

            Timer++;

            if (Timer > PatrolDuration(context)) {
                //只在服务端/单人端进行随机选择，避免多端desync
                if (!VaultUtils.isClient) {
                    return ChooseNextAttack(context);
                }
            }

            return null;
        }

        private IDestroyerState ChooseNextAttack(DestroyerStateContext context) {
            //加权随机，根据阶段调整权重
            int roll = Main.rand.Next(100);

            if (context.IsEnraged) {
                //半血后探针阵列概率大幅提高
                if (roll < 20) return new DestroyerLaserBarrageState();
                if (roll < 35) return new DestroyerEncircleState();
                if (roll < 55) return new DestroyerDashPrepareState();
                return new DestroyerProbeMatrixState(); //45%
            }

            if (roll < 25) return new DestroyerLaserBarrageState();
            if (roll < 45) return new DestroyerEncircleState();
            if (roll < 65) return new DestroyerDashPrepareState();
            return new DestroyerProbeMatrixState(); //35%
        }
    }
}
