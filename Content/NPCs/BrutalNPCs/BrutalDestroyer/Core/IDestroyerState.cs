using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>毁灭者状态索引，写入 npc.ai[2] 网络同步</summary>
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
        /// <summary>低血量大招：轨道绞杀（撤离高空→交叉俯冲→终结贯穿）</summary>
        OrbitalStrike = 10,
        /// <summary>普攻：俯冲贯穿（短整备+2~3趟预警线俯冲，无撤离静默幕）</summary>
        DiveStrike = 11,
        /// <summary>普攻：钻地伏击（入土潜行→地表尘迹→喷发预警→破土直射）</summary>
        BurrowAmbush = 12,
        /// <summary>普攻：回旋绞杀（迟滞后撤→突入→环绕绞索→环心贯穿冲出）</summary>
        LoopLash = 13,
    }

    /// <summary>毁灭者状态接口</summary>
    internal interface IDestroyerState : IVaultState<DestroyerStateContext>
    {
        DestroyerStateIndex StateIndex { get; }
        void OnEnter(DestroyerStateContext context);
        IDestroyerState OnUpdate(DestroyerStateContext context);
        void OnExit(DestroyerStateContext context);
    }

    /// <summary>毁灭者状态基类</summary>
    internal abstract class DestroyerStateBase : VaultState<DestroyerStateContext>, IDestroyerState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract DestroyerStateIndex StateIndex { get; }

        /// <summary>远距回归瞬移阀：头部远离玩家超阈值时瞬移到视野边缘；俯冲/钻地/轨道等状态应关闭</summary>
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

        /// <summary>蠕虫移动参数，由主控制器 UpdateMovement 消费</summary>
        protected void SetMovement(DestroyerStateContext context, Vector2 targetPos, float speed, float turnSpeed) {
            context.TargetPosition = targetPos;
            context.MoveSpeed = speed;
            context.TurnSpeed = turnSpeed;
        }

        /// <summary>平滑转向对准目标</summary>
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
