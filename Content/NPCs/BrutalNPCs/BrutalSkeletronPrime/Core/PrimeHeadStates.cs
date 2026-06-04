using InnoVault.StateMachines;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    [VaultState(1, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadDebutState : PrimeHeadStateBase
    {
        public override int StateId => 1;

        public override IVaultState<PrimeHeadStateContext> OnUpdate(
            VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunDebutState();
            return null;
        }
    }

    [VaultState(2, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadPhaseOneState : PrimeHeadStateBase
    {
        public override int StateId => 2;

        public override IVaultState<PrimeHeadStateContext> OnUpdate(
            VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunPhaseOneState();
            return null;
        }
    }

    [VaultState(3, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadPhaseTwoState : PrimeHeadStateBase
    {
        public override int StateId => 3;
        public bool SkipRemainingFrame { get; private set; }

        public override IVaultState<PrimeHeadStateContext> OnUpdate(
            VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            SkipRemainingFrame = ctx.Owner.RunPhaseTwoState();
            return null;
        }
    }

    [VaultState(4, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadDeathPerformanceState : PrimeHeadStateBase
    {
        public override int StateId => 4;
        public bool HandledFrame { get; private set; }

        public override IVaultState<PrimeHeadStateContext> OnUpdate(
            VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            HandledFrame = ctx.Owner.RunDeathPerformanceState();
            return null;
        }
    }
}
