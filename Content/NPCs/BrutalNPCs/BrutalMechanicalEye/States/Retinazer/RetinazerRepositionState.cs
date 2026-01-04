using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼一阶段调整位置状态
    /// </summary>
    internal class RetinazerRepositionState : TwinsStateBase
    {
        public override string StateName => "RetinazerReposition";

        private const int MaxDuration = 70;

        private float MoveSpeed => Context.IsMachineRebellion ? 14f : 10f;

        private TwinsStateContext Context;
        private Vector2 targetPosition;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;

            //随机选择一个位置
            targetPosition = context.Target.Center + Main.rand.NextVector2CircularEdge(400, 400);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            MoveTo(npc, targetPosition, MoveSpeed * 1.3f, 0.08f);
            FaceTarget(npc, player.Center);

            Timer++;

            //到达目标或超时
            if (Timer >= MaxDuration || Vector2.Distance(npc.Center, targetPosition) < 50) {
                return new RetinazerHoverShootState();
            }

            return null;
        }
    }
}
