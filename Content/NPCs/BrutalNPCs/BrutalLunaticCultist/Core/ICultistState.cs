using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum CultistStateIndex : int
    {
        Intro = 0,
        /// <summary>连接态：走位/选招/元素轮转</summary>
        Weave = 1,
        /// <summary>元素弹幕（火弧团/冰枪阵/雷柱列）</summary>
        ElementBarrage = 2,
        /// <summary>悬空法阵齐射</summary>
        SigilVolley = 3,
        /// <summary>真假瞬移环阵博弈</summary>
        MirrorBlink = 4,
        /// <summary>三元素轮盘</summary>
        ElementWheel = 5,
        /// <summary>远古光/厄运编排（P2）</summary>
        AncientAssault = 6,
        /// <summary>大仪式召龙（可打断）</summary>
        GrandRitual = 7,
        /// <summary>50% 转阶段演出</summary>
        PhaseTransition = 8,
        /// <summary>低血大招 三相灾变</summary>
        Cataclysm = 9,
        Despawn = 10,
        Death = 11,
    }

    /// <summary>状态接口</summary>
    internal interface ICultistState : IVaultState<CultistStateContext>
    {
        CultistStateIndex StateIndex { get; }
        void OnEnter(CultistStateContext context);
        ICultistState OnUpdate(CultistStateContext context);
        void OnExit(CultistStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class CultistStateBase : VaultState<CultistStateContext>, ICultistState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract CultistStateIndex StateIndex { get; }

        public virtual void OnEnter(CultistStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract ICultistState OnUpdate(CultistStateContext context);

        public virtual void OnExit(CultistStateContext context) {
            context.SkipDefaultHover = false;
        }

        public override void OnEnter(VaultStateMachine<CultistStateContext> machine, CultistStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<CultistStateContext> OnUpdate(VaultStateMachine<CultistStateContext> machine, CultistStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<CultistStateContext> machine, CultistStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>悬浮参数，主控 UpdateHover 消费</summary>
        protected static void SetHover(CultistStateContext context, Vector2 anchor) {
            context.HoverAnchor = anchor;
        }

        /// <summary>面向目标</summary>
        protected static void FaceTarget(CultistStateContext context) {
            NPC npc = context.Npc;
            if (context.Target == null) {
                return;
            }
            int sign = System.Math.Sign(context.Target.Center.X - npc.Center.X);
            if (sign != 0) {
                npc.direction = npc.spriteDirection = sign;
            }
        }

        /// <summary>手部施法点（原版弹幕出手位）</summary>
        protected static Vector2 HandPos(NPC npc) {
            return npc.Center + new Vector2(npc.direction * 30f, 12f);
        }

        /// <summary>玩家带前瞻的瞄准向量</summary>
        protected static Vector2 AimWithLead(NPC npc, Player player, float lead = 16f) {
            Vector2 vec = player.Center + player.velocity * lead - npc.Center;
            return vec.SafeNormalize(new Vector2(npc.direction, 0f));
        }

        /// <summary>弹幕伤害（难度感知，走原版公式；狂暴期 1.5 倍）</summary>
        protected static int ProjDamage(NPC npc, float normal, float expert) {
            int damage = npc.GetAttackDamage_ForProjectiles(normal, expert);
            if (npc.TryGetOverride(out CultistBossAI bossOverride) && bossOverride.Context?.Enraged == true) {
                damage = (int)(damage * 1.5f);
            }
            return damage;
        }

        #endregion
    }
}
