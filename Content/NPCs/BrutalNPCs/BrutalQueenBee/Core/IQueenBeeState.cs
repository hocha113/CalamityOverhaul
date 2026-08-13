using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum QueenBeeStateIndex : int
    {
        Intro = 0,
        /// <summary>连接段，走位重整+服务端选招</summary>
        Reposition = 1,
        /// <summary>蜂群箭矢，编队拼箭后分波掷镖</summary>
        SwarmArrow = 2,
        /// <summary>多段折线俯冲扫射，沿途撒毒刺幕</summary>
        DiveStrafe = 3,
        /// <summary>蜂蜜迫击炮，砸出黏滞蜜洼</summary>
        HoneyMortar = 4,
        /// <summary>毒刺扇，机动压制型喘息招</summary>
        StingerFan = 5,
        /// <summary>蜂墙横扫，留缝的活体墙(二阶段)</summary>
        SwarmWall = 6,
        /// <summary>蜂群漩涡，收缩围笼+女王穿心冲(二阶段)</summary>
        SwarmVortex = 7,
        /// <summary>蜂巢炮台布设(二阶段)</summary>
        WaxTurret = 8,
        /// <summary>二阶段转换演出，蜂盾蜕变</summary>
        PhaseTransition = 9,
        /// <summary>低血大招，蜂潮终曲长矛冲锋</summary>
        RoyalTide = 10,
        /// <summary>无有效目标撤离</summary>
        Despawn = 11,
        /// <summary>死亡演出，蜂群失控</summary>
        Death = 12,
        /// <summary>投技：蜜牢收网，蜂茧裹人垂直抬升+三轮穿刺+爆散坠落(二阶段)</summary>
        SwarmLift = 13,
    }

    /// <summary>状态接口</summary>
    internal interface IQueenBeeState : IVaultState<QueenBeeStateContext>
    {
        QueenBeeStateIndex StateIndex { get; }
        void OnEnter(QueenBeeStateContext context);
        IQueenBeeState OnUpdate(QueenBeeStateContext context);
        void OnExit(QueenBeeStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class QueenBeeStateBase : VaultState<QueenBeeStateContext>, IQueenBeeState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract QueenBeeStateIndex StateIndex { get; }

        public virtual void OnEnter(QueenBeeStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IQueenBeeState OnUpdate(QueenBeeStateContext context);

        public virtual void OnExit(QueenBeeStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<QueenBeeStateContext> machine, QueenBeeStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<QueenBeeStateContext> OnUpdate(VaultStateMachine<QueenBeeStateContext> machine, QueenBeeStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<QueenBeeStateContext> machine, QueenBeeStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>朝速度方向定帧朝向(女王贴图水平朝向)</summary>
        protected static void FaceByVelocity(NPC npc, float deadZone = 0.35f) {
            if (npc.velocity.X > deadZone) {
                npc.direction = 1;
            }
            else if (npc.velocity.X < -deadZone) {
                npc.direction = -1;
            }
            npc.spriteDirection = npc.direction;
        }

        /// <summary>面向目标</summary>
        protected static void FaceTarget(NPC npc, Vector2 target) {
            npc.direction = npc.Center.X < target.X ? 1 : -1;
            npc.spriteDirection = npc.direction;
        }

        /// <summary>到目标方向</summary>
        protected static Vector2 DirectionToTarget(QueenBeeStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>开接触伤</summary>
        protected static void EnableContactDamage(NPC npc) {
            npc.damage = npc.defDamage;
        }

        /// <summary>仅高速开接触伤，伤害窗口贴合视觉冲刺</summary>
        protected static void EnableContactDamageIfFast(NPC npc, float minSpeed = 17f) {
            npc.damage = npc.velocity.Length() >= minSpeed ? npc.defDamage : 0;
        }

        /// <summary>关接触伤</summary>
        protected static void DisableContactDamage(NPC npc) {
            npc.damage = 0;
        }

        #endregion
    }
}
