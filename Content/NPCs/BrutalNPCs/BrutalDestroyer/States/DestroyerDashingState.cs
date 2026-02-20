using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 冲刺中状态：高速移动+轻微追踪
    /// </summary>
    internal class DestroyerDashingState : DestroyerStateBase
    {
        public override string StateName => "Dashing";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Dashing;

        private const int DashDuration = 60;

        private int currentDashCount;
        private int maxDashCount;

        public DestroyerDashingState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //轻微追踪
            float dashSpeed = context.IsEnraged ? 55f : 42f;
            float trackingFactor = context.IsEnraged ? 0.02f : 0.01f;
            Vector2 toPlayer = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            npc.velocity = Vector2.Lerp(npc.velocity, toPlayer * dashSpeed, trackingFactor);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            Timer++;

            if (Timer >= DashDuration) {
                currentDashCount++;
                npc.netUpdate = true;
                //进入冷却
                return new DestroyerDashCooldownState(currentDashCount, maxDashCount);
            }

            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }

    /// <summary>
    /// 冲刺冷却状态：减速回转，决定继续冲刺还是回归巡空
    /// </summary>
    internal class DestroyerDashCooldownState : DestroyerStateBase
    {
        public override string StateName => "DashCooldown";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.DashCooldown;

        private int currentDashCount;
        private int maxDashCount;

        public DestroyerDashCooldownState(int dashCount, int maxCount) {
            currentDashCount = dashCount;
            maxDashCount = maxCount;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //平滑减速
            npc.velocity *= 0.95f;

            //缓慢回转朝向玩家
            FaceTarget(npc, player.Center, 0.05f);

            //以玩家上方为回归点
            SetMovement(context, player.Center + new Vector2(0, -500), 8f, 0.3f);

            int cooldownTime = context.IsEnraged ? 40 : 55;
            Timer++;

            if (Timer >= cooldownTime) {
                if (currentDashCount >= maxDashCount) {
                    return new DestroyerPatrolState();
                }
                else {
                    return new DestroyerDashPrepareState(currentDashCount);
                }
            }

            return null;
        }
    }
}
