using InnoVault.StateMachines;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Core
{
    /// <summary>状态索引，写入 npc.ai[3] 网络同步。全战斗预留位，按里程碑逐个实装</summary>
    internal enum SeaShrimpStateIndex : int
    {
        /// <summary>破沙而出入场演出</summary>
        Intro = 0,
        /// <summary>爬行索敌 hub（选招枢纽）</summary>
        Hub = 1,
        /// <summary>单螯刺击</summary>
        ClawJab = 2,
        /// <summary>空泡拳</summary>
        CavitationPunch = 3,
        /// <summary>尾扇水弹三连</summary>
        WaterVolley = 4,
        /// <summary>尾弹突袭（P2）</summary>
        TailFlipStrike = 5,
        /// <summary>背晶齐射→晶刺阵（P2）</summary>
        CrystalSpikes = 6,
        /// <summary>上升泡幕（P2）</summary>
        BubbleCurtain = 7,
        /// <summary>蜕壳转阶段（40%）</summary>
        MoltTransition = 8,
        /// <summary>超空泡终拳（P3）</summary>
        SuperCavitation = 9,
        /// <summary>脱战离场</summary>
        Despawn = 10,
        /// <summary>死亡演出</summary>
        Death = 11,
        /// <summary>双渊柱封场（P2 进场事件 / 蜕壳后刷新）</summary>
        VortexWall = 12,
        /// <summary>渊喉水炮（蓄力口吐巨型水柱，P2+）</summary>
        AbyssJet = 13,
        /// <summary>间歇泉行军（P1+）</summary>
        GeyserMarch = 14,
        /// <summary>甩尾涡旋（行走小龙卷，P2+）</summary>
        VortexToss = 15,
        /// <summary>合钳水刃（P1+）</summary>
        CrescentClap = 16,
        /// <summary>犁浪冲锋（头先行贴地冲刺，P1+）</summary>
        PlowCharge = 17,
        /// <summary>泡泡大炮：张钳聚出巨型雷泡拍向玩家，链爆电网（P1+）</summary>
        BubbleCannon = 18,
        /// <summary>泡泡棒球：挥尾甩泡，双钳交替连拍出击（P1+）</summary>
        BubbleBat = 19,
        /// <summary>跃空大跳：腾空砸落，两侧掀巨浪（P1+）</summary>
        SkyLeap = 20,
    }

    /// <summary>渊晶海虾状态接口</summary>
    internal interface ISeaShrimpState : IVaultState<SeaShrimpStateContext>
    {
        SeaShrimpStateIndex StateIndex { get; }
        void OnEnter(SeaShrimpStateContext context);
        ISeaShrimpState OnUpdate(SeaShrimpStateContext context);
        void OnExit(SeaShrimpStateContext context);
    }

    /// <summary>状态基类：桥接 VaultState 泛型签名，集中公共小件</summary>
    internal abstract class SeaShrimpStateBase : VaultState<SeaShrimpStateContext>, ISeaShrimpState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract SeaShrimpStateIndex StateIndex { get; }

        public virtual void OnEnter(SeaShrimpStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract ISeaShrimpState OnUpdate(SeaShrimpStateContext context);

        public virtual void OnExit(SeaShrimpStateContext context) { }

        public sealed override void OnEnter(VaultStateMachine<SeaShrimpStateContext> machine, SeaShrimpStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<SeaShrimpStateContext> OnUpdate(VaultStateMachine<SeaShrimpStateContext> machine, SeaShrimpStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<SeaShrimpStateContext> machine, SeaShrimpStateContext ctx) {
            OnExit(ctx);
        }

        #region 公共小件

        /// <summary>目标预测点：当前位置 + 速度外推</summary>
        protected static Vector2 PredictTarget(SeaShrimpStateContext ctx, float leadFrames) {
            Player target = ctx.Target;
            return target.Center + target.velocity * leadFrames;
        }

        /// <summary>结束攻击：有连击队列直接接招，否则回 hub 挂冷却</summary>
        protected static ISeaShrimpState EndAttack(SeaShrimpStateContext ctx, int cooldown) {
            if (ctx.QueuedChainState >= 0 && !ctx.Owner.TargetInvalid()) {
                int next = ctx.QueuedChainState;
                ctx.QueuedChainState = -1;
                IVaultState<SeaShrimpStateContext> chained = VaultStateRegistry<SeaShrimpStateContext>.Create(next);
                if (chained is ISeaShrimpState shrimp) {
                    return shrimp;
                }
            }
            ctx.QueuedChainState = -1;
            ctx.AttackCooldown = SeaShrimpDirector.ScaleCooldown(cooldown, ctx.Phase);
            return new States.SeaShrimpHubState();
        }

        /// <summary>原地稳身：驻停漂移（攻击蓄力通用，朝向由蓄力姿态自持）</summary>
        protected static void HoldInPlace(SeaShrimpStateContext ctx)
            => ctx.Owner.Locomotion.RequestHold();

        /// <summary>驻停并转身对线（冲刺/水炮蓄力用）：身轴贴向目标角，转率随蓄力衰减</summary>
        protected static void HoldFacing(SeaShrimpStateContext ctx, float heading, float turnRate)
            => ctx.Owner.Locomotion.RequestHoldFacing(heading, turnRate);

        /// <summary>就近震屏：只震看得见战斗的本地玩家，带距离门</summary>
        protected static void ShakeNearby(Vector2 pos, float amount, float range = 1300f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, pos) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        /// <summary>向下扫地：返回第一格实心地面的世界 Y；扫不到给兜底深度</summary>
        protected static float FindGroundY(Vector2 from, float maxDepth = 1600f) {
            int tx = (int)(from.X / 16f);
            int startTy = Math.Max((int)(from.Y / 16f), 10);
            int maxTy = Math.Min((int)((from.Y + maxDepth) / 16f), Main.maxTilesY - 10);
            for (int y = startTy; y <= maxTy; y++) {
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return y * 16f;
                }
            }
            return from.Y + maxDepth;
        }

        #endregion
    }
}
