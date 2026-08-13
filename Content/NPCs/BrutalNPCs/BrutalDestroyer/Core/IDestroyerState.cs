using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
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
        /// <summary>低血量大招，轨道绞杀</summary>
        OrbitalStrike = 10,
        /// <summary>普攻俯冲贯穿</summary>
        DiveStrike = 11,
        /// <summary>普攻钻地伏击</summary>
        BurrowAmbush = 12,
        /// <summary>普攻回旋绞杀</summary>
        LoopLash = 13,
        /// <summary>投技前置，锁环收缩预警</summary>
        CoilLock = 14,
        /// <summary>投技本体，钢环绞缠连段</summary>
        CoilCrush = 15,
    }

    /// <summary>状态接口</summary>
    internal interface IDestroyerState : IVaultState<DestroyerStateContext>
    {
        DestroyerStateIndex StateIndex { get; }
        void OnEnter(DestroyerStateContext context);
        IDestroyerState OnUpdate(DestroyerStateContext context);
        void OnExit(DestroyerStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class DestroyerStateBase : VaultState<DestroyerStateContext>, IDestroyerState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract DestroyerStateIndex StateIndex { get; }

        /// <summary>远距回归瞬移阀，俯冲/钻地/轨道应关</summary>
        public virtual bool AllowFarSnap => true;

        public virtual void OnEnter(DestroyerStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IDestroyerState OnUpdate(DestroyerStateContext context);

        public virtual void OnExit(DestroyerStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<DestroyerStateContext> machine, DestroyerStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<DestroyerStateContext> OnUpdate(VaultStateMachine<DestroyerStateContext> machine, DestroyerStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<DestroyerStateContext> machine, DestroyerStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>移动参数，UpdateMovement 消费</summary>
        protected void SetMovement(DestroyerStateContext context, Vector2 targetPos, float speed, float turnSpeed) {
            context.TargetPosition = targetPos;
            context.MoveSpeed = speed;
            context.TurnSpeed = turnSpeed;
        }

        /// <summary>平滑转向</summary>
        protected void FaceTarget(NPC npc, Vector2 target, float lerpFactor = 0.15f) {
            float targetAngle = (target - npc.Center).ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetAngle, lerpFactor);
        }

        /// <summary>到玩家方向</summary>
        protected Vector2 DirectionToTarget(DestroyerStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
