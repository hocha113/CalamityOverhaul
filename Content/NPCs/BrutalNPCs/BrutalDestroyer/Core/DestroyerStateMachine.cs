using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>
    /// 毁灭者状态机，服务端/单人端驱动状态转移，客户端通过npc.ai[]同步
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
            SyncStateToAI();
        }

        public void ForceChangeState(IDestroyerState newState) {
            if (newState == null) return;
            CurrentState?.OnExit(Context);
            PreviousState = CurrentState;
            CurrentState = newState;
            CurrentState.OnEnter(Context);
            SyncStateToAI();
            Context.Npc.netUpdate = true;
        }

        public void Update() {
            if (CurrentState == null) return;

            //客户端通过ai[]检测服务端的状态切换
            if (VaultUtils.isClient) {
                SyncStateFromAI();
            }

            IDestroyerState nextState = CurrentState.OnUpdate(Context);

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
        /// 将当前状态索引写入npc.ai[2]，以便网络同步
        /// </summary>
        private void SyncStateToAI() {
            if (CurrentState != null && Context.Npc != null) {
                Context.Npc.ai[2] = (float)CurrentState.StateIndex;
            }
        }

        /// <summary>
        /// 客户端从npc.ai[2]读取服务端状态，若不一致则切换
        /// </summary>
        private void SyncStateFromAI() {
            if (Context.Npc == null || CurrentState == null) return;
            int serverStateIndex = (int)Context.Npc.ai[2];
            if (serverStateIndex != (int)CurrentState.StateIndex) {
                IDestroyerState newState = CreateStateFromIndex((DestroyerStateIndex)serverStateIndex);
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
        internal static IDestroyerState CreateStateFromIndex(DestroyerStateIndex index) {
            return index switch {
                DestroyerStateIndex.Intro => new DestroyerIntroState(),
                DestroyerStateIndex.Patrol => new DestroyerPatrolState(),
                DestroyerStateIndex.DashPrepare => new DestroyerDashPrepareState(),
                DestroyerStateIndex.Dashing => new DestroyerDashingState(0, 3),
                DestroyerStateIndex.DashCooldown => new DestroyerDashCooldownState(0, 3),
                DestroyerStateIndex.LaserBarrage => new DestroyerLaserBarrageState(),
                DestroyerStateIndex.Encircle => new DestroyerEncircleState(),
                DestroyerStateIndex.ProbeMatrix => new DestroyerProbeMatrixState(),
                DestroyerStateIndex.Despawn => new DestroyerDespawnState(),
                DestroyerStateIndex.Death => new DestroyerDeathState(),
                _ => null,
            };
        }
    }
}
