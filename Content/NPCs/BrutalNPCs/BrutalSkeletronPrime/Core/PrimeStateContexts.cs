using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    internal class PrimeHeadStateContext : INpcStateContext
    {
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public HeadPrimeAI Owner { get; set; }
        public bool BossRush { get; set; }
        public bool Death { get; set; }
        public bool CannonAlive { get; set; }
        public bool ViceAlive { get; set; }
        public bool SawAlive { get; set; }
        public bool LaserAlive { get; set; }
        public bool NoArm => !CannonAlive && !ViceAlive && !SawAlive && !LaserAlive;
        public bool NoEye { get; set; }
    }

    internal class PrimeArmStateContext : INpcStateContext
    {
        public NPC Npc { get; set; }
        public NPC Head { get; set; }
        public Player Target { get; set; }
        public PrimeArm Owner { get; set; }
        public bool BossRush { get; set; }
        public bool MasterMode { get; set; }
        public bool Death { get; set; }
        public bool ViceAlive { get; set; }
        public bool CannonAlive { get; set; }
        public bool SawAlive { get; set; }
        public bool LaserAlive { get; set; }
        public bool DontAttack { get; set; }
    }

    internal abstract class PrimeHeadStateBase : VaultState<PrimeHeadStateContext>
    {
        public sealed override string StateName => GetType().Name;
    }

    internal abstract class PrimeArmStateBase : VaultState<PrimeArmStateContext>
    {
        public sealed override string StateName => GetType().Name;
    }
}
