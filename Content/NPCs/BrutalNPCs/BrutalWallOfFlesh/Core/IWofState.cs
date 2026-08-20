using InnoVault.StateMachines;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core
{
    /// <summary>状态索引，写入 npc.ai[2] 同步</summary>
    internal enum WofStateIndex : int
    {
        Intro = 0,
        /// <summary>推进枢纽，负责选招</summary>
        Advance = 1,
        /// <summary>蓄力短程突进，墙会冲刺</summary>
        SurgeDash = 2,
        /// <summary>口部吸引漩涡</summary>
        MawVortex = 3,
        /// <summary>双眼激光扫描协议</summary>
        EyeScan = 4,
        /// <summary>饥饿者系绳网</summary>
        HungryNet = 5,
        /// <summary>水蛭浪</summary>
        LeechWave = 6,
        /// <summary>血肉尖刺场</summary>
        FleshSpike = 7,
        /// <summary>舌鞭钩曳</summary>
        TongueLash = 8,
        /// <summary>66% 转阶段演出</summary>
        PhaseTransition = 9,
        /// <summary>33% 低血大招，绯红大迁徙(后方血幕合拢)</summary>
        CrimsonExodus = 10,
        Despawn = 11,
        Death = 12,
        /// <summary>舌卷回吞投技：抓取舌命中/绕后惩罚升级</summary>
        TongueGrab = 13,
        /// <summary>签名招·饥饿长城：全墙裂口接力噬咬，阶段3专属</summary>
        JawRipple = 14,
        /// <summary>签名招·腐眼断头闸：墙面长出腐眼，锁定高度后水平斩束封锁跑道</summary>
        RotGuillotine = 15,
    }

    /// <summary>状态接口</summary>
    internal interface IWofState : IVaultState<WofStateContext>
    {
        WofStateIndex StateIndex { get; }
        void OnEnter(WofStateContext context);
        IWofState OnUpdate(WofStateContext context);
        void OnExit(WofStateContext context);
    }

    /// <summary>状态基类</summary>
    internal abstract class WofStateBase : VaultState<WofStateContext>, IWofState
    {
        public override int StateId => (int)StateIndex;
        public abstract override string StateName { get; }
        public abstract WofStateIndex StateIndex { get; }

        public virtual void OnEnter(WofStateContext context) {
            Timer = 0;
            Counter = 0;
        }

        public abstract IWofState OnUpdate(WofStateContext context);

        public virtual void OnExit(WofStateContext context) {
            context.ResetChargeState();
            context.MouthCommand = 0;
        }

        public override void OnEnter(VaultStateMachine<WofStateContext> machine, WofStateContext ctx) {
            OnEnter(ctx);
        }

        public override IVaultState<WofStateContext> OnUpdate(VaultStateMachine<WofStateContext> machine, WofStateContext ctx) {
            return OnUpdate(ctx);
        }

        public override void OnExit(VaultStateMachine<WofStateContext> machine, WofStateContext ctx) {
            OnExit(ctx);
        }

        #region 工具方法

        /// <summary>墙面前方一点(推进方向)</summary>
        protected static Vector2 AheadPoint(WofStateContext ctx, float distance, float yFraction = 0.5f) {
            float x = WofWallField.WallFaceX(ctx.Npc) + ctx.Npc.direction * distance;
            float y = MathHelper.Lerp(WofWallField.Top, WofWallField.Bottom, yFraction);
            return new Vector2(x, y);
        }

        /// <summary>目标是否位于推进方向前方</summary>
        protected static bool TargetInFront(WofStateContext ctx) {
            if (!ctx.Target.Alives()) {
                return false;
            }
            return (ctx.Target.Center.X - ctx.Npc.Center.X) * ctx.Npc.direction > 0f;
        }

        #endregion
    }
}
