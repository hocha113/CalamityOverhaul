using InnoVault.StateMachines;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core
{
    /// <summary>状态索引，写入 npc.ai[3] 网络同步</summary>
    internal enum BssStateIndex : int
    {
        /// <summary>破土入场</summary>
        Intro = 0,
        /// <summary>爬行巡曳选招 hub</summary>
        Hub = 1,
        /// <summary>破土突袭</summary>
        BurrowLunge = 2,
        /// <summary>喷沙</summary>
        SandSpit = 3,
        /// <summary>仙人掌刺球抛掷</summary>
        CactusBall = 4,
        /// <summary>沙暴转阶段（60%）</summary>
        StormTransition = 5,
        /// <summary>针刺涟漪</summary>
        NeedleRipple = 6,
        /// <summary>抖擞花瓣</summary>
        PetalShake = 7,
        /// <summary>繁花怒放连接段（25%）</summary>
        ApexBloom = 8,
        /// <summary>脱战钻沙遁走</summary>
        Despawn = 9,
        /// <summary>死亡演出</summary>
        Death = 10,
        /// <summary>沙面掠冲</summary>
        SandDash = 11,
        /// <summary>天游：空中长时间蛇形游荡 + 俯冲砸地</summary>
        SkyWeave = 12,
        /// <summary>盘天环猎：绕玩家成环收紧 + 穿心突刺</summary>
        CoilOrbit = 13,
        /// <summary>沙爆漩涡冲刺：盘旋搓涡 + 弃涡爆冲，漩涡在身后爆</summary>
        VortexDash = 14,
        /// <summary>回环沙瀑：天上画正圆泻沙成帘 + 离心俯冲</summary>
        LoopCascade = 15,
        /// <summary>沙泉行军：立起砸地 + 冲击波沿地行军喷发</summary>
        GeyserMarch = 16,
        /// <summary>回马甩尾：擦身而过 + 急转离心甩针 + 回马枪连段</summary>
        TailSweep = 17,
    }

    /// <summary>荒花沙蟒状态接口</summary>
    internal interface IBssState : IVaultState<BssStateContext>
    {
        BssStateIndex StateIndex { get; }
        void OnEnter(BssStateContext context);
        IBssState OnUpdate(BssStateContext context);
        void OnExit(BssStateContext context);
    }

    /// <summary>状态基类：桥接 VaultState 泛型签名，并集中公共小件</summary>
    internal abstract class BssStateBase : VaultState<BssStateContext>, IBssState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract BssStateIndex StateIndex { get; }

        public virtual void OnEnter(BssStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IBssState OnUpdate(BssStateContext context);

        public virtual void OnExit(BssStateContext context) { }

        public sealed override void OnEnter(VaultStateMachine<BssStateContext> machine, BssStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<BssStateContext> OnUpdate(VaultStateMachine<BssStateContext> machine, BssStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<BssStateContext> machine, BssStateContext ctx) {
            OnExit(ctx);
        }

        #region 公共小件

        /// <summary>结束攻击：有连击队列直接接招，否则回 hub 挂冷却</summary>
        protected static IBssState EndAttack(BssStateContext ctx) {
            if (ctx.QueuedChainState >= 0 && !ctx.Owner.TargetInvalid()) {
                int next = ctx.QueuedChainState;
                ctx.QueuedChainState = -1;
                IVaultState<BssStateContext> chained = VaultStateRegistry<BssStateContext>.Create(next);
                if (chained is IBssState bss) {
                    return bss;
                }
            }
            ctx.QueuedChainState = -1;
            ctx.AttackCooldown = BssDirector.AttackCooldown(ctx.Phase);
            return new States.BssHubState();
        }

        /// <summary>目标预测点</summary>
        protected static Vector2 PredictTarget(BssStateContext ctx, float leadFrames)
            => ctx.Target.Center + ctx.Target.velocity * leadFrames;

        /// <summary>朝向目标的水平方向（避免抖动，差距过小时保持原向）</summary>
        protected static float FacingToTarget(BssStateContext ctx, float keepBand = 120f) {
            float dx = ctx.Target.Center.X - ctx.Npc.Center.X;
            if (Math.Abs(dx) < keepBand) {
                return ctx.CrawlDirX != 0f ? ctx.CrawlDirX : 1f;
            }
            return Math.Sign(dx);
        }

        #endregion
    }
}
