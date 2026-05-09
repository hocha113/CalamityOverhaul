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
            //固定出招循环顺序
            //普通: 激光弹幕 → 包围 → 冲刺 → 探针阵列
            //激怒: 激光弹幕 → 冲刺 → 包围 → 冲刺 → 探针阵列
            IDestroyerState[] normalSequence = [
                new DestroyerLaserBarrageState(),
                new DestroyerEncircleState(),
                new DestroyerDashPrepareState(),
                new DestroyerProbeMatrixState()
            ];
            IDestroyerState[] enragedSequence = [
                new DestroyerLaserBarrageState(),
                new DestroyerDashPrepareState(),
                new DestroyerEncircleState(),
                new DestroyerDashPrepareState(),
                new DestroyerProbeMatrixState()
            ];

            IDestroyerState[] sequence = context.IsEnraged ? enragedSequence : normalSequence;
            IDestroyerState next = sequence[context.AttackPhaseIndex % sequence.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
