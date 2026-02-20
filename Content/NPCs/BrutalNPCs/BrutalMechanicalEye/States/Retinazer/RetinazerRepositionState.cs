using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼一阶段调整位置状态
    /// </summary>
    internal class RetinazerRepositionState : TwinsStateBase
    {
        public override string StateName => "RetinazerReposition";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerRepositionState;

        private const int MaxDuration = 70;

        private float MoveSpeed => Context.IsMachineRebellion ? 14f : 10f;

        /// <summary>
        /// 基于comboStep的固定位置偏移角度表，确保确定性行为
        /// </summary>
        private static readonly float[] PositionAngles =
        [
            MathHelper.PiOver4,                  // 右上 45°
            MathHelper.Pi - MathHelper.PiOver4,   // 左上 135°
            -MathHelper.PiOver4,                  // 右下 -45°
            MathHelper.Pi + MathHelper.PiOver4    // 左下 225°
        ];

        private TwinsStateContext Context;
        private Vector2 targetPosition;
        private int comboStep;

        public RetinazerRepositionState(int currentComboStep = 0) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;

            //基于comboStep选择固定位置，确保多人模式同步
            float angle = PositionAngles[comboStep % PositionAngles.Length];
            targetPosition = context.Target.Center + angle.ToRotationVector2() * 400f;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            MoveTo(npc, targetPosition, MoveSpeed * 1.3f, 0.08f);
            FaceTarget(npc, player.Center);

            Timer++;

            //到达目标或超时，回到悬停射击继续套路循环
            if (Timer >= MaxDuration || Vector2.Distance(npc.Center, targetPosition) < 50) {
                return new RetinazerHoverShootState(comboStep);
            }

            return null;
        }
    }
}
