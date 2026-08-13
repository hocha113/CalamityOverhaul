using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum EocStateIndex : int
    {
        /// <summary>入场演出，血雾凝聚成眼</summary>
        Intro = 0,
        /// <summary>悬停压场连接段，血弹点射+选招</summary>
        VeilHover = 1,
        /// <summary>变轨假动作冲刺，中途拐折+谎言残影</summary>
        FeintDash = 2,
        /// <summary>血雾播场+雾中伏击</summary>
        FogAmbush = 3,
        /// <summary>仆从血枪列，纵队逐发</summary>
        ServantLance = 4,
        /// <summary>仆从血环合围，二阶段</summary>
        ServantEncircle = 5,
        /// <summary>溢血喷泉，旋喷重力血弹</summary>
        BloodFountain = 6,
        /// <summary>撕皮转阶段演出</summary>
        PhaseTransition = 7,
        /// <summary>口器狂化锯齿撕咬连冲，二阶段</summary>
        MawFrenzy = 8,
        /// <summary>盲侧横贯，假高位坠压→横线暴冲，二阶段</summary>
        BlindsideCross = 9,
        /// <summary>猩红血漩涡，低血大招</summary>
        Maelstrom = 10,
        /// <summary>无目标撤离</summary>
        Despawn = 11,
        /// <summary>死亡演出</summary>
        Death = 12,
        /// <summary>撕咬拖曳投技，二阶段</summary>
        MawDrag = 13,
    }

    /// <summary>状态接口</summary>
    internal interface IEocState : IVaultState<EocStateContext>
    {
        EocStateIndex StateIndex { get; }
        void OnEnter(EocStateContext context);
        /// <returns>下一态，null=保持</returns>
        IEocState OnUpdate(EocStateContext context);
        void OnExit(EocStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class EocStateBase : VaultState<EocStateContext>, IEocState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract EocStateIndex StateIndex { get; }

        /// <summary>远距雾步回归阀，演出/伏击/大招关</summary>
        public virtual bool AllowFogStep => true;

        public virtual void OnEnter(EocStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IEocState OnUpdate(EocStateContext context);

        public virtual void OnExit(EocStateContext context) {
            context.ResetChargeState();
            context.LaneIntensity = 0f;
        }

        public override void OnEnter(VaultStateMachine<EocStateContext> machine, EocStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<EocStateContext> OnUpdate(VaultStateMachine<EocStateContext> machine, EocStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<EocStateContext> machine, EocStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>瞳孔指向目标，rotation=朝向-PiOver2</summary>
        protected static void FaceTarget(NPC npc, Vector2 targetCenter, float lerpFactor = 1f) {
            float targetRot = (targetCenter - npc.Center).ToRotation() - MathHelper.PiOver2;
            npc.rotation = lerpFactor >= 1f ? targetRot : npc.rotation.AngleLerp(targetRot, lerpFactor);
        }

        /// <summary>朝向速度方向</summary>
        protected static void FaceVelocity(NPC npc) {
            if (npc.velocity.Length() > 0.1f) {
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
            }
        }

        protected static Vector2 DirectionToTarget(EocStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>仅高速开接触伤，窗口对齐视觉冲刺</summary>
        protected static void EnableContactDamageIfFast(NPC npc, float minSpeed = 24f, float mult = 1f) {
            npc.damage = npc.velocity.Length() >= minSpeed ? (int)(npc.defDamage * mult) : 0;
        }

        protected static void DisableContactDamage(NPC npc) {
            npc.damage = 0;
        }

        #endregion
    }
}
