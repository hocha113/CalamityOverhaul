using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core
{
    /// <summary>拳状态索引，写入 npc.ai[2] 网络同步</summary>
    internal enum GolemFistStateIndex : int
    {
        /// <summary>观察者哨兵：拳缺失/失效（不入注册表，不会被写入同步槽）</summary>
        Invalid = -1,
        /// <summary>锚点跟随</summary>
        Anchor = 0,
        /// <summary>出拳蓄力</summary>
        Windup = 1,
        /// <summary>出拳飞行（含反弹）</summary>
        Punch = 2,
        /// <summary>回收归位</summary>
        Return = 3,
        /// <summary>护卫环绕</summary>
        Guard = 4,
        /// <summary>坠地崩解（死亡演出）</summary>
        DeathFall = 5,
        /// <summary>投技抓取（钉墙→连段→研磨收尾）</summary>
        Grab = 6,
    }

    /// <summary>拳状态上下文，每拳一份</summary>
    internal class GolemFistStateContext : INpcStateContext
    {
        public NPC Npc { get; set; }
        public NPC Body { get; set; }
        public Player Target { get; set; }
        public GolemFistAI Owner { get; set; }

        public bool AsuraMode { get; set; }
        public bool Enraged { get; set; }
        /// <summary>拳侧 -1左 / 1右</summary>
        public int Side { get; set; }

        #region 表现数据
        /// <summary>蓄力进度 0~1（发光/汇聚粒子）</summary>
        public float WindupGlow { get; set; }
        /// <summary>本帧视觉速度（客户端傀儡清零速度前缓存，残影门控用）</summary>
        public float VisualSpeed { get; set; }
        /// <summary>本帧视觉速度向量（傀儡清零前缓存，推进器喷焰方向用）</summary>
        public Vector2 ThrustVel { get; set; }
        /// <summary>反弹侧向修正喷余帧（本地检测速度方向突变触发）</summary>
        public int BounceBurst { get; set; }
        /// <summary>肩口发射闪余帧（出拳点火）</summary>
        public int MuzzleFlash { get; set; }
        /// <summary>肩口发射点</summary>
        public Vector2 MuzzlePos { get; set; }
        /// <summary>弹簧速度（锚点跟随的滞后感）</summary>
        public Vector2 SpringVelocity { get; set; }
        /// <summary>剩余反弹预算（飞行期）</summary>
        public int BounceBudget { get; set; }
        /// <summary>本次指令序号快照，检测新指令</summary>
        public int LastCmdSeq { get; set; }
        #endregion

        /// <summary>读取当前指令类型</summary>
        public GolemFistCommand CmdKind => (GolemFistCommand)(int)Owner.ai[GolemAiSlots.FistCmdKind];
        /// <summary>指令目标点</summary>
        public Vector2 CmdPoint => new(Owner.ai[GolemAiSlots.FistCmdX], Owner.ai[GolemAiSlots.FistCmdY]);
        /// <summary>指令蓄力帧</summary>
        public int CmdWindup => (int)System.Math.Max(Owner.ai[GolemAiSlots.FistWindup], 10f);
        /// <summary>指令速度</summary>
        public float CmdSpeed => System.Math.Max(Owner.ai[GolemAiSlots.FistSpeed], 12f);
        /// <summary>指令反弹预算</summary>
        public int CmdBounce => (int)Owner.ai[GolemAiSlots.FistBounce];
    }

    /// <summary>拳状态接口</summary>
    internal interface IGolemFistState : IVaultState<GolemFistStateContext>
    {
        GolemFistStateIndex StateIndex { get; }
        void OnEnter(GolemFistStateContext context);
        IGolemFistState OnUpdate(GolemFistStateContext context);
        void OnExit(GolemFistStateContext context);
    }

    /// <summary>拳状态基类</summary>
    internal abstract class GolemFistStateBase : VaultState<GolemFistStateContext>, IGolemFistState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract GolemFistStateIndex StateIndex { get; }

        public virtual void OnEnter(GolemFistStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IGolemFistState OnUpdate(GolemFistStateContext context);

        public virtual void OnExit(GolemFistStateContext context) {
            context.WindupGlow = 0f;
        }

        public sealed override void OnEnter(VaultStateMachine<GolemFistStateContext> machine, GolemFistStateContext ctx) {
            OnEnter(ctx);
        }

        public sealed override IVaultState<GolemFistStateContext> OnUpdate(VaultStateMachine<GolemFistStateContext> machine, GolemFistStateContext ctx) {
            return OnUpdate(ctx);
        }

        public sealed override void OnExit(VaultStateMachine<GolemFistStateContext> machine, GolemFistStateContext ctx) {
            OnExit(ctx);
        }

        /// <summary>弹簧式追锚点：软追+滞后，重量感来源</summary>
        protected static void SpringToAnchor(GolemFistStateContext ctx, float stiffness = 0.075f, float damping = 0.82f) {
            NPC npc = ctx.Npc;
            Vector2 anchor = GolemFacts.FistAnchor(ctx.Body, ctx.Side);
            Vector2 toAnchor = anchor - npc.Center;

            //距离近时直接吸附，防低频抖动
            if (toAnchor.LengthSquared() < 9f * 9f && ctx.SpringVelocity.LengthSquared() < 4f) {
                npc.Center = anchor;
                npc.velocity = ctx.Body.velocity;
                ctx.SpringVelocity = Vector2.Zero;
                npc.rotation = 0f;
                return;
            }

            Vector2 spring = ctx.SpringVelocity + toAnchor * stiffness;
            spring *= damping;
            ctx.SpringVelocity = spring;
            npc.velocity = spring + ctx.Body.velocity * 0.5f;
            //远离锚点时朝向锚点，重现原版拳的甩尾感
            npc.rotation = npc.rotation.AngleLerp(toAnchor.X * 0.004f, 0.2f);
        }
    }
}
