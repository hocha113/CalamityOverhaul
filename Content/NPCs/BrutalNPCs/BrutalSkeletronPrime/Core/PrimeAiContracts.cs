using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    internal static class PrimeAiSlots
    {
        public const int HeadMainState = 0;
        public const int HeadAttackState = 1;
        public const int HeadAttackTimer = 2;
        public const int HeadMechQueenFlag = 3;
        public const int OverrideTwoStageSubState = 3;
        public const int OverrideIdleTeleportTimer = 10;

        public const int ArmSide = 0;
        public const int ArmHeadIndex = 1;
        public const int ArmState = 2;
        public const int ArmSharedTimer = 3;

        public const int ArmSpawnGraceFrames = 180;
    }

    internal readonly struct PrimeLimbStatus
    {
        public readonly bool CannonAlive;
        public readonly bool ViceAlive;
        public readonly bool SawAlive;
        public readonly bool LaserAlive;

        public PrimeLimbStatus(bool cannonAlive, bool viceAlive, bool sawAlive, bool laserAlive) {
            CannonAlive = cannonAlive;
            ViceAlive = viceAlive;
            SawAlive = sawAlive;
            LaserAlive = laserAlive;
        }

        public bool NoArm => !CannonAlive && !ViceAlive && !SawAlive && !LaserAlive;
    }

    internal static class PrimeFacts
    {
        public static PrimeLimbStatus GetLimbStatus() {
            return new PrimeLimbStatus(
                IsNpcActive(CWRWorld.primeCannon),
                IsNpcActive(CWRWorld.primeVice),
                IsNpcActive(CWRWorld.primeSaw),
                IsNpcActive(CWRWorld.primeLaser)
            );
        }

        public static bool IsDeathPerformance(NPC head) {
            return head != null && head.active && head.ai[PrimeAiSlots.HeadMainState] == HeadPrimeAI.DeathPerformanceMainState;
        }

        private static bool IsNpcActive(int whoAmI) {
            return whoAmI >= 0 && whoAmI < Main.maxNPCs && Main.npc[whoAmI].active;
        }
    }
}
