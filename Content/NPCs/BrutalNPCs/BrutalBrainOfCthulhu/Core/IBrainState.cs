using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum BrainStateIndex : int
    {
        Intro = 0,
        /// <summary>连接拍，选下一招</summary>
        Hover = 1,
        /// <summary>瞬移预兆欺诈：多裂隙一真多假</summary>
        MirrorFeint = 2,
        /// <summary>真假镜像同步进攻：点对称假体协同冲刺</summary>
        MirrorStrike = 3,
        /// <summary>飞眼轨道阵：收缩牢笼</summary>
        OrbitCage = 4,
        /// <summary>飞眼轨道阵：辐条扫压</summary>
        LanceWaves = 5,
        /// <summary>心跳弹环：露心搏动射环</summary>
        BloodPulse = 6,
        /// <summary>阶段转换演出：护壳崩裂</summary>
        PhaseTransition = 7,
        /// <summary>二阶段狂化追猎+闪现</summary>
        FrenzyChase = 8,
        /// <summary>二阶段镜像环阵轮舞</summary>
        MirrorMaze = 9,
        /// <summary>二阶段高空血雨抛射</summary>
        BloodRain = 10,
        /// <summary>低血大招：心搏骤停</summary>
        HeartAttack = 11,
        Despawn = 12,
        Death = 13,
        /// <summary>二阶段投技：摄心镜狱（镜像收环→念力定身→穿刺连击→真身撞散掷飞）</summary>
        MindSeize = 14,
    }

    /// <summary>状态接口</summary>
    internal interface IBrainState : IVaultState<BrainStateContext>
    {
        BrainStateIndex StateIndex { get; }
        void OnEnter(BrainStateContext context);
        IBrainState OnUpdate(BrainStateContext context);
        void OnExit(BrainStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class BrainStateBase : VaultState<BrainStateContext>, IBrainState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract BrainStateIndex StateIndex { get; }

        /// <summary>远距回归瞬移阀，演出类状态应关</summary>
        public virtual bool AllowFarSnap => true;

        public virtual void OnEnter(BrainStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IBrainState OnUpdate(BrainStateContext context);

        public virtual void OnExit(BrainStateContext context) {
            context.ResetTelegraph();
        }

        public override void OnEnter(VaultStateMachine<BrainStateContext> machine, BrainStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<BrainStateContext> OnUpdate(VaultStateMachine<BrainStateContext> machine, BrainStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<BrainStateContext> machine, BrainStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>到玩家方向</summary>
        protected static Vector2 DirectionToTarget(BrainStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        #endregion
    }
}
