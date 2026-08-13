using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>核心状态索引，写入核心 npc.ai[2] 同步</summary>
    internal enum MLordStateIndex : int
    {
        /// <summary>日蚀降临登场</summary>
        Intro = 0,
        /// <summary>幻影协奏（基线火力网/连接拍）</summary>
        Concerto = 1,
        /// <summary>潮汐掌击</summary>
        TidalPalms = 2,
        /// <summary>死光扫描线</summary>
        DeathrayScan = 3,
        /// <summary>弦月合拢</summary>
        CrescentClose = 4,
        /// <summary>引力坍缩</summary>
        GravityCollapse = 5,
        /// <summary>星陨召唤</summary>
        Starfall = 6,
        /// <summary>月蚀噬咬</summary>
        MoonBite = 7,
        /// <summary>部件破坏事件演出</summary>
        PartBreak = 8,
        /// <summary>核心裸露转换演出</summary>
        CoreExposure = 9,
        /// <summary>虚空撕裂（低血大招）</summary>
        VoidRupture = 10,
        /// <summary>脱战离场</summary>
        Despawn = 11,
        /// <summary>终焉时刻死亡演出</summary>
        Death = 12,
    }

    /// <summary>核心状态接口</summary>
    internal interface IMLordState : IVaultState<MLordContext>
    {
        MLordStateIndex StateIndex { get; }
        void OnEnter(MLordContext context);
        IMLordState OnUpdate(MLordContext context);
        void OnExit(MLordContext context);
    }

    /// <summary>核心状态基类</summary>
    internal abstract class MLordStateBase : VaultState<MLordContext>, IMLordState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract MLordStateIndex StateIndex { get; }

        public virtual void OnEnter(MLordContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IMLordState OnUpdate(MLordContext context);

        public virtual void OnExit(MLordContext context) {
            context.ResetChargeState();
            context.HoldAllParts = false;
            context.StaggerVulnerable = false;
        }

        public sealed override void OnEnter(VaultStateMachine<MLordContext> machine, MLordContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<MLordContext> OnUpdate(VaultStateMachine<MLordContext> machine, MLordContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<MLordContext> machine, MLordContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>按出招表进入下一攻击状态（仅权威端调用产生实际切换）</summary>
        protected static IMLordState NextAttack(MLordContext context) {
            if (VaultUtils.isClient) {
                return null;
            }
            return CreateState(context.NextAttackIndex());
        }

        /// <summary>按索引建状态实例，未注册返回协奏兜底</summary>
        protected static IMLordState CreateState(MLordStateIndex index) {
            IVaultState<MLordContext> state = VaultStateRegistry<MLordContext>.Create((int)index);
            return state as IMLordState ?? new States.MLordConcertoState();
        }

        /// <summary>核心弹簧悬停：目标点 + 平滑速度进给</summary>
        protected static void HoverTo(NPC npc, Vector2 toPoint, float maxSpeed, float gain = 0.055f) {
            Vector2 want = (toPoint - npc.Center) * gain;
            if (want.Length() > maxSpeed) {
                want = want.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, want, 0.14f);
        }

        /// <summary>核心随速倾斜</summary>
        protected static void UpdateLean(MLordContext context) {
            NPC npc = context.Npc;
            context.LeanAngle = MathHelper.Clamp(npc.velocity.X * 0.012f, -0.09f, 0.09f);
            npc.rotation = npc.rotation.AngleLerp(context.LeanAngle, 0.1f);
        }

        /// <summary>到玩家方向</summary>
        protected static Vector2 DirectionToTarget(MLordContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>难度伤害修正</summary>
        protected static int ScaleDamage(MLordContext context, int damage)
            => MLordDirector.ScaleDamage(damage, context.DeathMode);

        /// <summary>节奏帧数压缩</summary>
        protected static int Frames(MLordContext context, int baseFrames)
            => MLordDirector.Frames(baseFrames, context.DeathMode);

        #endregion
    }
}
