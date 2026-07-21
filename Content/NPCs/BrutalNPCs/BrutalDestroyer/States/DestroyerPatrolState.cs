using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>巡空，椭圆盘旋+高度起伏</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Patrol, typeof(DestroyerStateContext))]
    internal class DestroyerPatrolState : DestroyerStateBase
    {
        public override string StateName => "Patrol";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Patrol;

        public DestroyerPatrolState() {
        }

        private int PatrolDuration(DestroyerStateContext ctx) => ctx.IsEnraged ? 130 : 170;

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

            Vector2 patrolTarget = player.Center + new Vector2(offsetX, offsetY);
            SetMovement(context, patrolTarget, speed, turnSpeed);
            //巡航蛇形
            context.SlitherStrength = 1f;

            Timer++;

            int duration = PatrolDuration(context);
            //就位提前出招，视野内起手
            bool positioned = Timer > duration * 0.55f
                && npc.WithinRange(patrolTarget, 240f)
                && npc.Distance(player.Center) < 1500f;

            if (Timer > duration || positioned) {
                //只服务端/单人选招
                if (!VaultUtils.isClient) {
                    return ChooseNextAttack(context);
                }
            }

            return null;
        }

        private IDestroyerState ChooseNextAttack(DestroyerStateContext context) {
            //过半血归零出招，激怒首招轨道绞杀
            if (context.IsEnraged && !context.EnrageCycleStarted) {
                context.EnrageCycleStarted = true;
                context.AttackPhaseIndex = 0;
            }

            IDestroyerState[] normalSequence = [
                new DestroyerLaserBarrageState(),
                new DestroyerDiveStrikeState(),

                new DestroyerEncircleState(),
                new DestroyerDashPrepareState(),
                new DestroyerBurrowAmbushState(),
                new DestroyerProbeMatrixState(),
            ];

            IDestroyerState[] enragedSequence = [
                new DestroyerOrbitalStrikeState(),
                new DestroyerLaserBarrageState(),
                new DestroyerLoopLashState(),
                new DestroyerBurrowAmbushState(),
                new DestroyerDashPrepareState(),
                new DestroyerDiveStrikeState(),
                new DestroyerProbeMatrixState(),
                new DestroyerEncircleState(),
            ];

            IDestroyerState[] sequence = context.IsEnraged ? enragedSequence : normalSequence;
            IDestroyerState next = sequence[context.AttackPhaseIndex % sequence.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
