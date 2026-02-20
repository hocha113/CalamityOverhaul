using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>
    /// 双子魔眼状态机，服务端/单人端驱动状态转移，客户端通过npc.ai[1]同步
    /// </summary>
    internal class TwinsStateMachine
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        public ITwinsState CurrentState { get; private set; }

        /// <summary>
        /// 上一个状态
        /// </summary>
        public ITwinsState PreviousState { get; private set; }

        /// <summary>
        /// 状态上下文
        /// </summary>
        public TwinsStateContext Context { get; private set; }

        public TwinsStateMachine(TwinsStateContext context) {
            Context = context;
        }

        /// <summary>
        /// 设置初始状态
        /// </summary>
        public void SetInitialState(ITwinsState state) {
            CurrentState = state;
            CurrentState?.OnEnter(Context);
            SyncStateToAI();
        }

        /// <summary>
        /// 强制切换状态
        /// </summary>
        public void ForceChangeState(ITwinsState newState) {
            if (newState == null) {
                return;
            }

            CurrentState?.OnExit(Context);
            PreviousState = CurrentState;
            CurrentState = newState;
            CurrentState.OnEnter(Context);
            SyncStateToAI();
            Context.Npc.netUpdate = true;
        }

        /// <summary>
        /// 更新状态机
        /// </summary>
        public void Update() {
            if (CurrentState == null) {
                return;
            }

            //客户端通过ai[1]检测服务端的状态切换
            if (VaultUtils.isClient) {
                SyncStateFromAI();
            }

            ITwinsState nextState = CurrentState.OnUpdate(Context);

            //只有服务端/单人端才能驱动状态转移
            if (!VaultUtils.isClient && nextState != null && nextState != CurrentState) {
                CurrentState.OnExit(Context);
                PreviousState = CurrentState;
                CurrentState = nextState;
                CurrentState.OnEnter(Context);
                SyncStateToAI();
                Context.Npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 将当前状态索引写入npc.ai[1]，以便网络同步
        /// </summary>
        private void SyncStateToAI() {
            if (CurrentState != null && Context.Npc != null) {
                Context.Npc.ai[1] = (float)CurrentState.StateIndex;
            }
        }

        /// <summary>
        /// 客户端从npc.ai[1]读取服务端状态，若不一致则切换
        /// </summary>
        private void SyncStateFromAI() {
            if (Context.Npc == null || CurrentState == null) return;
            int serverStateIndex = (int)Context.Npc.ai[1];
            if (serverStateIndex != (int)CurrentState.StateIndex) {
                ITwinsState newState = CreateStateFromIndex((TwinsStateIndex)serverStateIndex);
                if (newState != null) {
                    CurrentState.OnExit(Context);
                    PreviousState = CurrentState;
                    CurrentState = newState;
                    CurrentState.OnEnter(Context);
                }
            }
        }

        /// <summary>
        /// 根据索引创建状态实例（客户端同步用）
        /// </summary>
        internal static ITwinsState CreateStateFromIndex(TwinsStateIndex index) {
            return index switch {
                //魔焰眼一阶段
                TwinsStateIndex.SpazmatismHoverShoot => new SpazmatismHoverShootState(),
                TwinsStateIndex.SpazmatismDashPrepare => new SpazmatismDashPrepareState(),
                TwinsStateIndex.SpazmatismDashing => new SpazmatismDashingState(0, 2),
                TwinsStateIndex.SpazmatismFireVortex => new SpazmatismFireVortexState(),
                //魔焰眼二阶段
                TwinsStateIndex.SpazmatismFlameChase => new SpazmatismFlameChaseState(),
                TwinsStateIndex.SpazmatismPhase2DashPrepare => new SpazmatismPhase2DashPrepareState(),
                TwinsStateIndex.SpazmatismPhase2Dashing => new SpazmatismPhase2DashingState(0, 4),
                TwinsStateIndex.SpazmatismShadowDash => new SpazmatismShadowDashState(),
                TwinsStateIndex.SpazmatismFlameStorm => new SpazmatismFlameStormState(),
                TwinsStateIndex.SpazmatismSoloRage => new SpazmatismSoloRageState(),
                //激光眼一阶段
                TwinsStateIndex.RetinazerHoverShoot => new RetinazerHoverShootState(),
                TwinsStateIndex.RetinazerRepositionState => new RetinazerRepositionState(),
                TwinsStateIndex.RetinazerFocusedBeam => new RetinazerFocusedBeamState(),
                //激光眼二阶段
                TwinsStateIndex.RetinazerVerticalBarrage => new RetinazerVerticalBarrageState(),
                TwinsStateIndex.RetinazerHorizontalBarrage => new RetinazerHorizontalBarrageState(),
                TwinsStateIndex.RetinazerLaserSweep => new RetinazerLaserSweepState(),
                TwinsStateIndex.RetinazerLaserMatrix => new RetinazerLaserMatrixState(),
                TwinsStateIndex.RetinazerPrecisionSniper => new RetinazerPrecisionSniperState(),
                TwinsStateIndex.RetinazerSoloRage => new RetinazerSoloRageState(),
                //公共状态
                TwinsStateIndex.TwinsPhaseTransition => new TwinsPhaseTransitionState(),
                TwinsStateIndex.TwinsCombinedAttack => new TwinsCombinedAttackState(),
                _ => null,
            };
        }
    }
}
