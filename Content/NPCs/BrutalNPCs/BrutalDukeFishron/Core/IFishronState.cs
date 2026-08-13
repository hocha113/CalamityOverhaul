using InnoVault.StateMachines;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum FishronStateIndex : int
    {
        Intro = 0,
        Hover = 1,
        TidalDashPrepare = 2,
        TidalDashing = 3,
        TidalDashCooldown = 4,
        /// <summary>气泡迷宫，封锁走位</summary>
        BubbleMaze = 5,
        /// <summary>鲨鱼龙卷地形物召唤</summary>
        TornadoSummon = 6,
        /// <summary>潮汐平扫，拖行海啸墙</summary>
        TsunamiSweep = 7,
        /// <summary>鲨群空袭，斜线俯冲航道</summary>
        SharkronStrafe = 8,
        /// <summary>环舞爆发，气泡环径向齐射</summary>
        RingSpin = 9,
        /// <summary>雷暴领域，黑暗闪电雨</summary>
        LightningRain = 10,
        /// <summary>风暴连突，雨幕短冲链</summary>
        StormChainDash = 11,
        /// <summary>低血大招，灭世潮漩</summary>
        Maelstrom = 12,
        PhaseTwoTransition = 13,
        PhaseThreeTransition = 14,
        Despawn = 15,
        Death = 16,
    }

    /// <summary>状态接口</summary>
    internal interface IFishronState : IVaultState<FishronStateContext>
    {
        FishronStateIndex StateIndex { get; }
        void OnEnter(FishronStateContext context);
        IFishronState OnUpdate(FishronStateContext context);
        void OnExit(FishronStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class FishronStateBase : VaultState<FishronStateContext>, IFishronState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract FishronStateIndex StateIndex { get; }

        /// <summary>远距回归瞬移阀，演出/大招期应关</summary>
        public virtual bool AllowFarSnap => true;

        public virtual void OnEnter(FishronStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IFishronState OnUpdate(FishronStateContext context);

        public virtual void OnExit(FishronStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<FishronStateContext> machine, FishronStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<FishronStateContext> OnUpdate(VaultStateMachine<FishronStateContext> machine, FishronStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<FishronStateContext> machine, FishronStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>悬停移动参数，主控 UpdateMovement 消费</summary>
        protected void SetMovement(FishronStateContext context, Vector2 targetPos, float speed, float accel) {
            context.TargetPosition = targetPos;
            context.MoveSpeed = speed;
            context.Accel = accel;
        }

        /// <summary>原版式朝向：direction/spriteDirection/rotation 三件套</summary>
        protected static void FaceBody(NPC npc, Vector2 focus, float rotRate = 0.08f) {
            int dir = Math.Sign(focus.X - npc.Center.X);
            if (dir != 0) {
                npc.direction = dir;
                if (npc.spriteDirection != -npc.direction) {
                    npc.rotation += MathHelper.Pi;
                    npc.spriteDirection = -npc.direction;
                }
            }
            float targetRot = (focus - npc.Center).ToRotation();
            if (npc.spriteDirection == 1) {
                targetRot += MathHelper.Pi;
            }
            npc.rotation = npc.rotation.AngleTowards(targetRot, rotRate);
        }

        /// <summary>冲刺姿态：身体锁定速度方向</summary>
        protected static void AimBodyAlongVelocity(NPC npc) {
            if (npc.velocity.LengthSquared() < 0.01f) {
                return;
            }
            int dir = Math.Sign(npc.velocity.X);
            if (dir != 0) {
                npc.direction = dir;
                npc.spriteDirection = -npc.direction;
            }
            npc.rotation = npc.velocity.ToRotation();
            if (npc.spriteDirection == 1) {
                npc.rotation += MathHelper.Pi;
            }
        }

        /// <summary>到目标方向</summary>
        protected static Vector2 DirectionToTarget(FishronStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
