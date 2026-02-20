using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>
    /// 毁灭者状态索引，用于网络同步
    /// </summary>
    internal enum DestroyerStateIndex : int
    {
        Intro = 0,
        Patrol = 1,
        DashPrepare = 2,
        Dashing = 3,
        DashCooldown = 4,
        LaserBarrage = 5,
        Encircle = 6,
        ProbeMatrix = 7,
        Despawn = 8,
        Death = 9,
    }

    /// <summary>
    /// 毁灭者状态接口
    /// </summary>
    internal interface IDestroyerState
    {
        string StateName { get; }
        DestroyerStateIndex StateIndex { get; }
        void OnEnter(DestroyerStateContext context);
        IDestroyerState OnUpdate(DestroyerStateContext context);
        void OnExit(DestroyerStateContext context);
    }

    /// <summary>
    /// 毁灭者状态基类
    /// </summary>
    internal abstract class DestroyerStateBase : IDestroyerState
    {
        public abstract string StateName { get; }
        public abstract DestroyerStateIndex StateIndex { get; }
        protected int Timer { get; set; }
        protected int Counter { get; set; }

        public virtual void OnEnter(DestroyerStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IDestroyerState OnUpdate(DestroyerStateContext context);

        public virtual void OnExit(DestroyerStateContext context) {
            context.ResetChargeState();
        }

        #region 工具方法

        /// <summary>
        /// 蠕虫式移动：设置目标点和速度参数，由主控制器的UpdateMovement统一执行
        /// </summary>
        protected void SetMovement(DestroyerStateContext context, Vector2 targetPos, float speed, float turnSpeed) {
            context.TargetPosition = targetPos;
            context.MoveSpeed = speed;
            context.TurnSpeed = turnSpeed;
        }

        /// <summary>
        /// 平滑转向对准目标
        /// </summary>
        protected void FaceTarget(NPC npc, Vector2 target, float lerpFactor = 0.15f) {
            float targetAngle = (target - npc.Center).ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetAngle, lerpFactor);
        }

        /// <summary>
        /// 获取到玩家的方向
        /// </summary>
        protected Vector2 DirectionToTarget(DestroyerStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
