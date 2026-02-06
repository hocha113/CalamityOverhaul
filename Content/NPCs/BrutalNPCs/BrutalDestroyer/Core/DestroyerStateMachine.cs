namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>
    /// 毁灭者状态机
    /// </summary>
    internal class DestroyerStateMachine
    {
        public IDestroyerState CurrentState { get; private set; }
        public IDestroyerState PreviousState { get; private set; }
        public DestroyerStateContext Context { get; private set; }

        public DestroyerStateMachine(DestroyerStateContext context) {
            Context = context;
        }

        public void SetInitialState(IDestroyerState state) {
            CurrentState = state;
            CurrentState?.OnEnter(Context);
        }

        public void ForceChangeState(IDestroyerState newState) {
            if (newState == null) return;
            CurrentState?.OnExit(Context);
            PreviousState = CurrentState;
            CurrentState = newState;
            CurrentState.OnEnter(Context);
        }

        public void Update() {
            if (CurrentState == null) return;

            IDestroyerState nextState = CurrentState.OnUpdate(Context);
            if (nextState != null && nextState != CurrentState) {
                CurrentState.OnExit(Context);
                PreviousState = CurrentState;
                CurrentState = nextState;
                CurrentState.OnEnter(Context);
            }
        }
    }
}
