using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum KingSlimeStateIndex : int
    {
        Intro = 0,
        /// <summary>连跳压制，兼攻击间连接器</summary>
        Hop = 1,
        /// <summary>王冠天坠</summary>
        CrownSlam = 2,
        /// <summary>凝胶迫击，跳顶泼洒</summary>
        GelMortar = 3,
        /// <summary>压扁成潮汐地面冲刷</summary>
        TideRush = 4,
        /// <summary>立塔倾倒海啸</summary>
        TowerCollapse = 5,
        /// <summary>受控分裂-再聚合</summary>
        SplitSwarm = 6,
        /// <summary>体内忍者影袭</summary>
        NinjaFlurry = 7,
        /// <summary>低血大招，皇权审判</summary>
        RoyalDecree = 8,
        /// <summary>阶段转换演出，王冠离体</summary>
        PhaseShift = 9,
        /// <summary>追击阀，化胶潜地重现</summary>
        PursuitBurrow = 10,
        Despawn = 11,
        Death = 12,
    }

    /// <summary>状态接口</summary>
    internal interface IKingSlimeState : IVaultState<KingSlimeStateContext>
    {
        KingSlimeStateIndex StateIndex { get; }
        void OnEnter(KingSlimeStateContext context);
        IKingSlimeState OnUpdate(KingSlimeStateContext context);
        void OnExit(KingSlimeStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class KingSlimeStateBase : VaultState<KingSlimeStateContext>, IKingSlimeState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract KingSlimeStateIndex StateIndex { get; }

        /// <summary>可被阶段转换/追击阀/大招打断，只有连接器 Hop 开放</summary>
        public virtual bool Interruptible => false;

        public virtual void OnEnter(KingSlimeStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IKingSlimeState OnUpdate(KingSlimeStateContext context);

        public virtual void OnExit(KingSlimeStateContext context) {
            context.ResetPerStateFlags();
        }

        public override void OnEnter(VaultStateMachine<KingSlimeStateContext> machine, KingSlimeStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<KingSlimeStateContext> OnUpdate(VaultStateMachine<KingSlimeStateContext> machine, KingSlimeStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<KingSlimeStateContext> machine, KingSlimeStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>是否站在地上(引擎碰撞已把纵速清零)</summary>
        protected static bool Grounded(NPC npc) => npc.velocity.Y == 0f || npc.collideY;

        /// <summary>一帧爆发起跳，落点预测由调用方给出</summary>
        protected static void LaunchHop(NPC npc, float vx, float vy) {
            npc.velocity = new Vector2(vx, vy);
            npc.netUpdate = true;
        }

        /// <summary>朝目标的水平方向符号</summary>
        protected static int DirToTarget(KingSlimeStateContext context)
            => context.Target.Center.X >= context.Npc.Center.X ? 1 : -1;

        /// <summary>攻击结束回连接器，服务端选择跳数。
        /// 连接拍收紧：P1双跳/P2单跳，死亡模式再减一(下限1，提速规则保持)</summary>
        protected static IKingSlimeState BackToHop(KingSlimeStateContext context) {
            int hops = context.IsPhase2 ? 1 : 2;
            if (context.IsDeathMode && hops > 1) {
                hops--;
            }
            return new States.KingSlimeHopState(hops);
        }

        #endregion
    }
}
