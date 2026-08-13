using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum EmpressStateIndex : int
    {
        Intro = 0,
        /// <summary>衔接：归位滑翔+攻击选择</summary>
        Connector = 1,
        /// <summary>旋转棱彩环，缺口进动</summary>
        PrismRings = 2,
        /// <summary>以太枪骑网格，错拍执行</summary>
        LanceGrid = 3,
        /// <summary>剑雨阵，编队悬停瞄准齐射</summary>
        SwordRain = 4,
        /// <summary>日舞，径向光束旋扇</summary>
        RadiantDance = 5,
        /// <summary>收缩笼，旋转缺口</summary>
        ConvergingCage = 6,
        /// <summary>干涉织网，双手反向双螺旋</summary>
        InterferenceWeave = 7,
        /// <summary>弦月突进，冲刺+垂直弹幕尾迹</summary>
        CrescentDash = 8,
        /// <summary>永恒绽放(P2)，虹瓣螺旋+绽放核心</summary>
        EverlastingBloom = 9,
        /// <summary>半血变身，全屏棱彩爆发</summary>
        PhaseTransition = 10,
        /// <summary>低血大招，棱彩过驱三重奏</summary>
        PrismOverdrive = 11,
        Despawn = 12,
        /// <summary>死亡演出，光之消散</summary>
        Death = 13,
        /// <summary>光绫缚舞投技：缚定悬空→三段交叉剑舞→辐光爆绽掷出</summary>
        LightBindWaltz = 14,
    }

    /// <summary>状态接口</summary>
    internal interface IEmpressState : IVaultState<EmpressStateContext>
    {
        EmpressStateIndex StateIndex { get; }
        void OnEnter(EmpressStateContext context);
        IEmpressState OnUpdate(EmpressStateContext context);
        void OnExit(EmpressStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class EmpressStateBase : VaultState<EmpressStateContext>, IEmpressState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract EmpressStateIndex StateIndex { get; }

        public virtual void OnEnter(EmpressStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IEmpressState OnUpdate(EmpressStateContext context);

        public virtual void OnExit(EmpressStateContext context) {
            context.ResetChargeState();
        }

        public override void OnEnter(VaultStateMachine<EmpressStateContext> machine, EmpressStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<EmpressStateContext> OnUpdate(VaultStateMachine<EmpressStateContext> machine, EmpressStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<EmpressStateContext> machine, EmpressStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>阻尼弹簧滑翔悬停</summary>
        protected static void GlideTo(NPC npc, Vector2 target, float stiffness = 0.016f, float damping = 0.085f, float maxSpeed = 26f) {
            EmpressMotion.SpringGlide(npc, target, stiffness, damping, maxSpeed);
        }

        /// <summary>到玩家方向</summary>
        protected static Vector2 DirectionToTarget(EmpressStateContext context) {
            return (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>本端播放音效（服务器空放安全，仍显式挡掉）</summary>
        protected static void PlayLocal(Terraria.Audio.SoundStyle style, Vector2 pos) {
            if (!VaultUtils.isServer) {
                Terraria.Audio.SoundEngine.PlaySound(style, pos);
            }
        }

        #endregion
    }
}
