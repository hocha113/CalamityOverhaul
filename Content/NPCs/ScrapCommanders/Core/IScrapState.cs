using InnoVault.StateMachines;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Core
{
    /// <summary>状态索引，写入 npc.ai[3] 网络同步。已为全战斗预留位，M1 只实装其中子集</summary>
    internal enum ScrapStateIndex : int
    {
        /// <summary>零件雨自组装进场</summary>
        Intro = 0,
        /// <summary>悬停选招 hub</summary>
        Hub = 1,
        /// <summary>锯轮放犬</summary>
        SawLaunch = 2,
        /// <summary>废钢迫击</summary>
        Mortar = 3,
        /// <summary>钳爪绞刑</summary>
        ViceSnatch = 4,
        /// <summary>镭射双拍</summary>
        LaserSweep = 5,
        /// <summary>头锤摆荡</summary>
        HeadSwing = 6,
        /// <summary>甩壳重构转阶段</summary>
        PhaseTransition = 7,
        /// <summary>拼装军团</summary>
        Legion = 8,
        /// <summary>磁暴收束</summary>
        MagnetStorm = 9,
        /// <summary>总攻指令</summary>
        AllOutCommand = 10,
        /// <summary>过载熔断连接段</summary>
        OverloadConnector = 11,
        /// <summary>脱战离场</summary>
        Despawn = 12,
        /// <summary>死亡演出</summary>
        Death = 13,
        /// <summary>链锤十字旋</summary>
        CrossSpin = 14,
        /// <summary>废钢瀑布</summary>
        Waterfall = 15,
        /// <summary>锯炮协奏</summary>
        SawCannonCombo = 16,
        /// <summary>镭射矩阵</summary>
        LaserMatrix = 17,
        /// <summary>熔断全械（过载终局）</summary>
        FusedFrenzy = 18,
    }

    /// <summary>废钢统帅状态接口</summary>
    internal interface IScrapState : IVaultState<ScrapStateContext>
    {
        ScrapStateIndex StateIndex { get; }
        void OnEnter(ScrapStateContext context);
        IScrapState OnUpdate(ScrapStateContext context);
        void OnExit(ScrapStateContext context);
    }

    /// <summary>状态基类：桥接 VaultState 泛型签名，并集中悬停/倾头等公共小件</summary>
    internal abstract class ScrapStateBase : VaultState<ScrapStateContext>, IScrapState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract ScrapStateIndex StateIndex { get; }

        public virtual void OnEnter(ScrapStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IScrapState OnUpdate(ScrapStateContext context);

        public virtual void OnExit(ScrapStateContext context) { }

        public sealed override void OnEnter(VaultStateMachine<ScrapStateContext> machine, ScrapStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<ScrapStateContext> OnUpdate(VaultStateMachine<ScrapStateContext> machine, ScrapStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<ScrapStateContext> machine, ScrapStateContext ctx) {
            OnExit(ctx);
        }

        #region 公共小件

        /// <summary>朝锚点滑行：比例趋近 + 限速 + 惯性混合，指数衰减不匀速</summary>
        protected static void GlideToward(ScrapStateContext ctx, Vector2 anchor, float approach, float maxSpeed, float inertia = 0.1f) {
            NPC npc = ctx.Npc;
            Vector2 desired = (anchor - npc.Center) * approach;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, inertia);
        }

        /// <summary>头随横速轻微倾斜，绝不旋颅</summary>
        protected static void LeanByVelocity(NPC npc, float rate = 0.12f) {
            float wantTilt = MathHelper.Clamp(npc.velocity.X * 0.014f, -0.16f, 0.16f);
            npc.rotation = npc.rotation.AngleLerp(wantTilt, rate);
        }

        /// <summary>目标预测点：当前位置 + 速度外推</summary>
        protected static Vector2 PredictTarget(ScrapStateContext ctx, float leadFrames) {
            Player target = ctx.Target;
            return target.Center + target.velocity * leadFrames;
        }

        /// <summary>结束攻击：有连击队列就直接接招（收招帧即后招蓄力帧），否则回 hub 挂冷却</summary>
        protected static IScrapState EndAttack(ScrapStateContext ctx, int cooldown) {
            if (ctx.QueuedChainState >= 0 && !ctx.Owner.TargetInvalid()) {
                int next = ctx.QueuedChainState;
                ctx.QueuedChainState = -1;
                IVaultState<ScrapStateContext> chained = VaultStateRegistry<ScrapStateContext>.Create(next);
                if (chained is IScrapState scrap) {
                    return scrap;
                }
            }
            ctx.QueuedChainState = -1;
            ctx.AttackCooldown = ScrapDirector.ScaleCooldown(cooldown, ctx.Phase);
            return new States.ScrapHubState();
        }

        /// <summary>就近震屏：只震看得见战斗的本地玩家，带距离衰减门</summary>
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
