using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum PlanteraStateIndex : int
    {
        /// <summary>入场演出，钩爪破土拽出花苞→绽放</summary>
        Intro = 0,
        /// <summary>悬吊巡航连接态，选下一招</summary>
        Canopy = 1,
        /// <summary>种子加特林弹幕压制</summary>
        SeedGatling = 2,
        /// <summary>钩爪锚定+藤蔓弹弓猛扑</summary>
        GrapplePounce = 3,
        /// <summary>藤蔓格栅重塑战场</summary>
        VineLattice = 4,
        /// <summary>孢子云播撒，漂浮地雷生态</summary>
        SporeSow = 5,
        /// <summary>一阶段→二阶段蜕壳演出</summary>
        PhaseTransition = 6,
        /// <summary>二阶段连环狂扑</summary>
        FrenzyPounce = 7,
        /// <summary>二阶段触手绽放处刑圈</summary>
        TentacleRing = 8,
        /// <summary>二阶段触手鞭刑连段</summary>
        WhipBarrage = 9,
        /// <summary>低血大招，凋零绽放新星</summary>
        BloomNova = 10,
        /// <summary>无目标撤离</summary>
        Despawn = 11,
        /// <summary>死亡演出，钩爪逐根断裂→坠落</summary>
        Death = 12,
    }

    /// <summary>状态接口</summary>
    internal interface IPlanteraState : IVaultState<PlanteraStateContext>
    {
        PlanteraStateIndex StateIndex { get; }
        void OnEnter(PlanteraStateContext context);
        IPlanteraState OnUpdate(PlanteraStateContext context);
        void OnExit(PlanteraStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class PlanteraStateBase : VaultState<PlanteraStateContext>, IPlanteraState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract PlanteraStateIndex StateIndex { get; }

        public virtual void OnEnter(PlanteraStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IPlanteraState OnUpdate(PlanteraStateContext context);

        public virtual void OnExit(PlanteraStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<PlanteraStateContext> machine, PlanteraStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<PlanteraStateContext> OnUpdate(VaultStateMachine<PlanteraStateContext> machine, PlanteraStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<PlanteraStateContext> machine, PlanteraStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>悬吊移动参数，主控 UpdateSuspension 消费</summary>
        protected static void SetSuspension(PlanteraStateContext context, Vector2 anchorOffset, float speed, float accel) {
            context.SuspendOffset = anchorOffset;
            context.MoveSpeed = speed;
            context.AccelRate = accel;
        }

        /// <summary>朝目标平滑转体</summary>
        protected static void FaceTarget(NPC npc, Vector2 target, float lerpFactor = 0.14f) {
            float targetAngle = (target - npc.Center).ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetAngle, lerpFactor);
        }

        /// <summary>到玩家单位方向</summary>
        protected static Vector2 DirectionToTarget(PlanteraStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
