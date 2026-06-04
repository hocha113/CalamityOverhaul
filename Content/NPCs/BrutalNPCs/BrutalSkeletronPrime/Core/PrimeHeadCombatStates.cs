using InnoVault.StateMachines;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    internal abstract class PrimeHeadPhaseTwoSubStateBase : PrimeHeadStateBase
    {
        public bool SkipRemainingFrame { get; protected set; }
    }

    [VaultState(10, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadHoverTrackingState : PrimeHeadStateBase
    {
        public override int StateId => 10;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunPhaseOneHoverTrackingState();
            return null;
        }
    }

    [VaultState(11, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadDashChargeState : PrimeHeadStateBase
    {
        public override int StateId => 11;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunPhaseOneDashChargeState();
            return null;
        }
    }

    [VaultState(12, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadDayEnrageState : PrimeHeadStateBase
    {
        public override int StateId => 12;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunPhaseOneDayEnrageState();
            return null;
        }
    }

    [VaultState(13, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadDespawnState : PrimeHeadStateBase
    {
        public override int StateId => 13;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunPhaseOneDespawnState();
            return null;
        }
    }

    [VaultState(14, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadCoinGunFuryState : PrimeHeadStateBase
    {
        public override int StateId => 14;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            ctx.Owner.RunPhaseOneCoinGunFuryState();
            return null;
        }
    }

    [VaultState(20, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadTwinSummonSetupState : PrimeHeadPhaseTwoSubStateBase
    {
        public override int StateId => 20;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            SkipRemainingFrame = ctx.Owner.RunPhaseTwoLegacyState();
            return null;
        }
    }

    [VaultState(21, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadSpreadBarrageState : PrimeHeadPhaseTwoSubStateBase
    {
        public override int StateId => 21;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            SkipRemainingFrame = ctx.Owner.RunPhaseTwoLegacyState();
            return null;
        }
    }

    [VaultState(22, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadRadialBarrageState : PrimeHeadPhaseTwoSubStateBase
    {
        public override int StateId => 22;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            SkipRemainingFrame = ctx.Owner.RunPhaseTwoLegacyState();
            return null;
        }
    }

    [VaultState(23, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadLaserWallState : PrimeHeadPhaseTwoSubStateBase
    {
        public override int StateId => 23;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            SkipRemainingFrame = ctx.Owner.RunPhaseTwoLegacyState();
            return null;
        }
    }

    [VaultState(24, typeof(PrimeHeadStateContext))]
    internal sealed class PrimeHeadPhaseRecoveryState : PrimeHeadPhaseTwoSubStateBase
    {
        public override int StateId => 24;
        public override IVaultState<PrimeHeadStateContext> OnUpdate(VaultStateMachine<PrimeHeadStateContext> machine, PrimeHeadStateContext ctx) {
            SkipRemainingFrame = ctx.Owner.RunPhaseTwoLegacyState();
            return null;
        }
    }
}
