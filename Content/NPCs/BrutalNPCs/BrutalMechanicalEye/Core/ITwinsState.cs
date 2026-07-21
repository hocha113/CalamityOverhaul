using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>状态索引，网络同步，双眼共用</summary>
    internal enum TwinsStateIndex : int
    {
        //魔焰一阶段
        SpazmatismHoverShoot = 0,
        SpazmatismDashPrepare = 1,
        SpazmatismDashing = 2,
        SpazmatismFireVortex = 3,
        //魔焰二阶段
        SpazmatismFlameChase = 4,
        SpazmatismPhase2DashPrepare = 5,
        SpazmatismPhase2Dashing = 6,
        SpazmatismShadowDash = 7,
        SpazmatismFlameStorm = 8,
        SpazmatismSoloRage = 9,
        //激光一阶段
        RetinazerHoverShoot = 10,
        RetinazerRepositionState = 11,
        RetinazerFocusedBeam = 12,
        //激光二阶段
        RetinazerVerticalBarrage = 13,
        RetinazerHorizontalBarrage = 14,
        RetinazerLaserSweep = 15,
        RetinazerLaserMatrix = 16,
        RetinazerPrecisionSniper = 17,
        RetinazerSoloRage = 18,
        //公共
        TwinsPhaseTransition = 19,
        TwinsCombinedAttack = 20,
        //死亡演出，每眼独立
        TwinsDeath = 21,
        //合击
        TwinsCrossDash = 22,
        TwinsTetherSweep = 23,
        TwinsScissorRay = 24,
    }

    internal interface ITwinsState : IVaultState<TwinsStateContext>
    {
        /// <summary>状态索引，同步</summary>
        TwinsStateIndex StateIndex { get; }

        void OnEnter(TwinsStateContext context);

        /// <returns>下一态，null=保持</returns>
        ITwinsState OnUpdate(TwinsStateContext context);

        void OnExit(TwinsStateContext context);
    }

    internal abstract class TwinsStateBase : VaultState<TwinsStateContext>, ITwinsState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract TwinsStateIndex StateIndex { get; }

        public virtual void OnEnter(TwinsStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract ITwinsState OnUpdate(TwinsStateContext context);

        public virtual void OnExit(TwinsStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<TwinsStateContext> machine, TwinsStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<TwinsStateContext> OnUpdate(VaultStateMachine<TwinsStateContext> machine, TwinsStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<TwinsStateContext> machine, TwinsStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        protected void MoveTo(NPC npc, Vector2 target, float speed, float inertia) {
            Vector2 direction = target - npc.Center;
            if (direction.Length() > 0.01f) {
                direction.Normalize();
            }
            Vector2 desiredVelocity = direction * speed;
            npc.velocity = (npc.velocity * (1f - inertia)) + (desiredVelocity * inertia);
        }

        protected void FaceTarget(NPC npc, Vector2 targetCenter) {
            npc.rotation = (targetCenter - npc.Center).ToRotation() - MathHelper.PiOver2;
        }

        protected void FaceVelocity(NPC npc) {
            if (npc.velocity.Length() > 0.1f) {
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
            }
        }

        protected Vector2 GetDirectionToTarget(TwinsStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>开接触伤</summary>
        protected void EnableContactDamage(NPC npc) {
            npc.damage = npc.defDamage;
        }

        /// <summary>关接触伤</summary>
        protected void DisableContactDamage(NPC npc) {
            npc.damage = 0;
        }

        #endregion
    }
}
