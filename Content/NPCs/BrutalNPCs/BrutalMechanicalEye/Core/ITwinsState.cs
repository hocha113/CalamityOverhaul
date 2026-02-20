using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>
    /// 双子魔眼状态索引，用于网络同步
    /// 魔焰眼和激光眼共用同一套索引
    /// </summary>
    internal enum TwinsStateIndex : int
    {
        //魔焰眼一阶段
        SpazmatismHoverShoot = 0,
        SpazmatismDashPrepare = 1,
        SpazmatismDashing = 2,
        SpazmatismFireVortex = 3,
        //魔焰眼二阶段
        SpazmatismFlameChase = 4,
        SpazmatismPhase2DashPrepare = 5,
        SpazmatismPhase2Dashing = 6,
        SpazmatismShadowDash = 7,
        SpazmatismFlameStorm = 8,
        SpazmatismSoloRage = 9,
        //激光眼一阶段
        RetinazerHoverShoot = 10,
        RetinazerRepositionState = 11,
        RetinazerFocusedBeam = 12,
        //激光眼二阶段
        RetinazerVerticalBarrage = 13,
        RetinazerHorizontalBarrage = 14,
        RetinazerLaserSweep = 15,
        RetinazerLaserMatrix = 16,
        RetinazerPrecisionSniper = 17,
        RetinazerSoloRage = 18,
        //公共状态
        TwinsPhaseTransition = 19,
        TwinsCombinedAttack = 20,
    }

    /// <summary>
    /// 双子魔眼状态接口
    /// 定义状态的基本行为
    /// </summary>
    internal interface ITwinsState
    {
        /// <summary>
        /// 状态名称，用于调试
        /// </summary>
        string StateName { get; }

        /// <summary>
        /// 状态索引，用于网络同步
        /// </summary>
        TwinsStateIndex StateIndex { get; }

        /// <summary>
        /// 进入状态时调用
        /// </summary>
        void OnEnter(TwinsStateContext context);

        /// <summary>
        /// 状态更新，每帧调用
        /// </summary>
        /// <returns>返回下一个状态，返回null表示保持当前状态</returns>
        ITwinsState OnUpdate(TwinsStateContext context);

        /// <summary>
        /// 离开状态时调用
        /// </summary>
        void OnExit(TwinsStateContext context);
    }

    /// <summary>
    /// 双子魔眼状态基类
    /// 提供状态的通用实现
    /// </summary>
    internal abstract class TwinsStateBase : ITwinsState
    {
        public abstract string StateName { get; }
        public abstract TwinsStateIndex StateIndex { get; }

        /// <summary>
        /// 状态内部计时器
        /// </summary>
        protected int Timer { get; set; }

        /// <summary>
        /// 状态内部计数器
        /// </summary>
        protected int Counter { get; set; }

        public virtual void OnEnter(TwinsStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract ITwinsState OnUpdate(TwinsStateContext context);

        public virtual void OnExit(TwinsStateContext context) {
            context.ResetChargeState();
        }

        #region 工具方法

        /// <summary>
        /// 平滑移动到目标点
        /// </summary>
        protected void MoveTo(NPC npc, Vector2 target, float speed, float inertia) {
            Vector2 direction = target - npc.Center;
            if (direction.Length() > 0.01f) {
                direction.Normalize();
            }
            Vector2 desiredVelocity = direction * speed;
            npc.velocity = (npc.velocity * (1f - inertia)) + (desiredVelocity * inertia);
        }

        /// <summary>
        /// 朝向目标旋转
        /// </summary>
        protected void FaceTarget(NPC npc, Vector2 targetCenter) {
            npc.rotation = (targetCenter - npc.Center).ToRotation() - MathHelper.PiOver2;
        }

        /// <summary>
        /// 朝向速度方向旋转
        /// </summary>
        protected void FaceVelocity(NPC npc) {
            if (npc.velocity.Length() > 0.1f) {
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
            }
        }

        /// <summary>
        /// 获取到玩家的方向向量
        /// </summary>
        protected Vector2 GetDirectionToTarget(TwinsStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>
        /// 启用碰撞伤害，用于冲刺等体术攻击
        /// </summary>
        protected void EnableContactDamage(NPC npc) {
            npc.damage = npc.defDamage;
        }

        /// <summary>
        /// 禁用碰撞伤害，用于非冲刺状态
        /// </summary>
        protected void DisableContactDamage(NPC npc) {
            npc.damage = 0;
        }

        #endregion
    }
}
