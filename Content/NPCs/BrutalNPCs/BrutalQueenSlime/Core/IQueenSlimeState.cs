using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum QueenSlimeStateIndex : int
    {
        /// <summary>入场演出，光柱降临+晶茧破碎</summary>
        Intro = 0,
        /// <summary>一阶段枢纽，芭蕾步跳跃(水晶芭蕾预备段)</summary>
        BallroomStep = 1,
        /// <summary>棱镜齐射，光束打在棱晶节点上折射成碎晶弹</summary>
        PrismVolley = 2,
        /// <summary>水晶圆舞，闪转腾挪+珍珠环收放</summary>
        CrystalWaltz = 3,
        /// <summary>凝胶陨雨，扇形三波空中弹幕编排</summary>
        GelMeteorRain = 4,
        /// <summary>阶段转换演出，升空展翅</summary>
        PhaseTransition = 5,
        /// <summary>二阶段枢纽，8字空中芭蕾巡航</summary>
        AerialBallet = 6,
        /// <summary>翼压风场，掠过铺设风道位移压制</summary>
        WingGaleWaltz = 7,
        /// <summary>折射牢笼，棱晶环+跑马灯光束网</summary>
        RefractionCage = 8,
        /// <summary>空降回压，足尖俯冲+尖塔波</summary>
        CrystalDiveStomp = 9,
        /// <summary>水晶吊灯，空中晶体蓄能坠落</summary>
        ChandelierFall = 10,
        /// <summary>低血大招，水晶圣殿</summary>
        CrystalCathedral = 11,
        /// <summary>无目标撤离</summary>
        Despawn = 12,
        /// <summary>死亡演出</summary>
        Death = 13,
        /// <summary>水晶囚舞(投技)：御晶吊灯压中→封晶→华尔兹连踢→碎晶掷飞</summary>
        CrystalPrisonWaltz = 14,
    }

    /// <summary>状态接口</summary>
    internal interface IQueenSlimeState : IVaultState<QueenSlimeStateContext>
    {
        QueenSlimeStateIndex StateIndex { get; }
        void OnEnter(QueenSlimeStateContext context);
        IQueenSlimeState OnUpdate(QueenSlimeStateContext context);
        void OnExit(QueenSlimeStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class QueenSlimeStateBase : VaultState<QueenSlimeStateContext>, IQueenSlimeState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract QueenSlimeStateIndex StateIndex { get; }

        public virtual void OnEnter(QueenSlimeStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IQueenSlimeState OnUpdate(QueenSlimeStateContext context);

        public virtual void OnExit(QueenSlimeStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<QueenSlimeStateContext> machine, QueenSlimeStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<QueenSlimeStateContext> OnUpdate(VaultStateMachine<QueenSlimeStateContext> machine, QueenSlimeStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<QueenSlimeStateContext> machine, QueenSlimeStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>朝向目标(仅贴图方向)</summary>
        protected static void FaceTarget(NPC npc, Vector2 target) {
            npc.spriteDirection = npc.direction = target.X > npc.Center.X ? 1 : -1;
        }

        /// <summary>开接触伤</summary>
        protected static void EnableContactDamage(NPC npc) => npc.damage = npc.defDamage;

        /// <summary>仅高速开接触伤，伤害窗口对齐视觉</summary>
        protected static void EnableContactDamageIfFast(NPC npc, float minSpeed = 12f) {
            npc.damage = npc.velocity.Length() >= minSpeed ? npc.defDamage : 0;
        }

        /// <summary>关接触伤</summary>
        protected static void DisableContactDamage(NPC npc) => npc.damage = 0;

        /// <summary>到目标方向</summary>
        protected static Vector2 DirectionToTarget(QueenSlimeStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
