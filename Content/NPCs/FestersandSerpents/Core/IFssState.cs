using InnoVault.StateMachines;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Core
{
    /// <summary>状态索引，写入 npc.ai[3] 网络同步</summary>
    internal enum FssStateIndex : int
    {
        /// <summary>污染扩散 + 双弧破土入场</summary>
        Intro = 0,
        /// <summary>爬行巡曳选招 hub</summary>
        Hub = 1,
        /// <summary>灵液扫喷：行进齐射痰弹，落点留存脓池</summary>
        IchorSpit = 2,
        /// <summary>掠地毒冲：假动作爆冲 + 尾迹灵液滴落</summary>
        VenomSkim = 3,
        /// <summary>黏疮布点：抛黏疮贴附砖面，鼓胀后喷竖直灵液泉</summary>
        StickyCyst = 4,
        /// <summary>破土脓泉：钻沙突袭 + 破口引燃近旁脓池</summary>
        BreachFount = 5,
        /// <summary>蜕变生长转阶段（62%）：撕皮甩壳 + 当场长节</summary>
        MoltGrowth = 6,
        /// <summary>吞沙炮：吞沙鼓包沿身蠕动到口，喷巨型空爆炮弹</summary>
        SwallowMortar = 7,
        /// <summary>环卷瀑洗：围玩家画大圈，绕圈中向心呕吐灵液管流（地形无关）</summary>
        CoilCascade = 8,
        /// <summary>疮爆掠航：高速掠过玩家，囊肿沿身链式爆裂</summary>
        FesterRipple = 9,
        /// <summary>满溢怒放连接段（28%）</summary>
        Overflow = 10,
        /// <summary>满场引爆：立身怒吼，全场脓池按距离次序喷发成行波</summary>
        FieldDetonate = 11,
        /// <summary>脱战钻沙遁走</summary>
        Despawn = 12,
        /// <summary>死亡演出</summary>
        Death = 13,
        /// <summary>灵液门冲：开门隐身传送，从玩家侧的出口门爆冲而出（地形无关）</summary>
        PortalRush = 14,
        /// <summary>裂躯交叉：中段撕裂成双蛇，同帧交叉冲刺编舞后合体（P3）</summary>
        SunderCross = 15,
        /// <summary>疮杵夯地：双杵合砸 + 灵液喷泉爆发播池</summary>
        ClawSlam = 16,
        /// <summary>长镰自刈：双镰剪切自体囊肿喷扇 + 镰尖甩痰（消耗囊肿充能）</summary>
        ClawReap = 17,
        /// <summary>夯地泉列：双杵合砸，灵液冲击柱自夯点双向行军喷发</summary>
        ClawQuake = 18,
    }

    /// <summary>脓蕾沙蟒状态接口</summary>
    internal interface IFssState : IVaultState<FssStateContext>
    {
        FssStateIndex StateIndex { get; }
        void OnEnter(FssStateContext context);
        IFssState OnUpdate(FssStateContext context);
        void OnExit(FssStateContext context);
    }

    /// <summary>状态基类：桥接 VaultState 泛型签名，并集中公共小件</summary>
    internal abstract class FssStateBase : VaultState<FssStateContext>, IFssState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract FssStateIndex StateIndex { get; }

        public virtual void OnEnter(FssStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IFssState OnUpdate(FssStateContext context);

        public virtual void OnExit(FssStateContext context) { }

        public sealed override void OnEnter(VaultStateMachine<FssStateContext> machine, FssStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<FssStateContext> OnUpdate(VaultStateMachine<FssStateContext> machine, FssStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<FssStateContext> machine, FssStateContext ctx) {
            OnExit(ctx);
        }

        #region 公共小件

        /// <summary>结束攻击：有连击队列直接接招，否则回 hub 挂冷却</summary>
        protected static IFssState EndAttack(FssStateContext ctx) {
            if (ctx.QueuedChainState >= 0 && !ctx.Owner.TargetInvalid()) {
                int next = ctx.QueuedChainState;
                ctx.QueuedChainState = -1;
                IVaultState<FssStateContext> chained = VaultStateRegistry<FssStateContext>.Create(next);
                if (chained is IFssState fss) {
                    return fss;
                }
            }
            ctx.QueuedChainState = -1;
            ctx.AttackCooldown = FssDirector.AttackCooldown(ctx.Phase);
            return new States.FssHubState();
        }

        /// <summary>目标预测点</summary>
        protected static Vector2 PredictTarget(FssStateContext ctx, float leadFrames)
            => ctx.Target.Center + ctx.Target.velocity * leadFrames;

        /// <summary>朝向目标的水平方向（避免抖动，差距过小时保持原向）</summary>
        protected static float FacingToTarget(FssStateContext ctx, float keepBand = 120f) {
            float dx = ctx.Target.Center.X - ctx.Npc.Center.X;
            if (Math.Abs(dx) < keepBand) {
                return ctx.CrawlDirX != 0f ? ctx.CrawlDirX : 1f;
            }
            return Math.Sign(dx);
        }

        /// <summary>口部位置（rotation − FacingRot = 实际朝向）</summary>
        protected static Vector2 MouthPos(NPC npc)
            => npc.Center + (npc.rotation - FssHead.FacingRot).ToRotationVector2() * 34f * npc.scale;

        /// <summary>
        /// 撕咬意图（纯表现）：冲势伤害窗内玩家贴嘴时鳌足急伸合围（镰钩压 + 杵砸托）。
        /// 冲刺类状态在飞行段每帧调用，接触伤害机制不变。
        /// </summary>
        protected static void DeclareSnatchIfClose(FssStateContext ctx, NPC npc, float speedGate) {
            if (!ctx.Target.Alives() || npc.velocity.Length() <= speedGate) {
                return;
            }
            if (Vector2.Distance(MouthPos(npc), ctx.Target.Center) < 210f) {
                ctx.ClawCommand = FssClawCommand.Snatch;
                ctx.ClawAim = ctx.Target.Center;
            }
        }

        #endregion
    }
}
