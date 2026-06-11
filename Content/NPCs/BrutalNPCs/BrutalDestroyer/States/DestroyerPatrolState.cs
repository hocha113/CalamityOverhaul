using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 巡空状态：椭圆轨迹盘旋，带高度起伏
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Patrol, typeof(DestroyerStateContext))]
    internal class DestroyerPatrolState : DestroyerStateBase
    {
        public override string StateName => "Patrol";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Patrol;

        public DestroyerPatrolState() {
        }

        private int PatrolDuration(DestroyerStateContext ctx) => ctx.IsEnraged ? 200 : 250;

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
            //巡航蛇形摆动：机械蠕虫"游动"姿态
            context.SlitherStrength = 1f;

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
            //普通: 侧舷齐射 → 合围电牢(→冲刺) → 蛇形连突 → 探针矩阵 → 轨道绞杀
            //激怒: 轨道绞杀打头阵（50%转阶段后第一招即大招），随后高压循环
            IDestroyerState[] normalSequence = [
                new DestroyerLaserBarrageState(),
                new DestroyerEncircleState(),
                new DestroyerDashPrepareState(),
                new DestroyerProbeMatrixState(),
                new DestroyerOrbitalStrikeState()
            ];
            IDestroyerState[] enragedSequence = [
                new DestroyerOrbitalStrikeState(),
                new DestroyerLaserBarrageState(),
                new DestroyerDashPrepareState(),
                new DestroyerEncircleState(),
                new DestroyerProbeMatrixState()
            ];

            IDestroyerState[] sequence = context.IsEnraged ? enragedSequence : normalSequence;
            IDestroyerState next = sequence[context.AttackPhaseIndex % sequence.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
