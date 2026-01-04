namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>
    /// 双子魔眼状态机
    /// 管理状态的切换和更新
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
        }

        /// <summary>
        /// 更新状态机
        /// </summary>
        public void Update() {
            if (CurrentState == null) {
                return;
            }

            ITwinsState nextState = CurrentState.OnUpdate(Context);

            if (nextState != null && nextState != CurrentState) {
                CurrentState.OnExit(Context);
                PreviousState = CurrentState;
                CurrentState = nextState;
                CurrentState.OnEnter(Context);
            }
        }
    }
}
