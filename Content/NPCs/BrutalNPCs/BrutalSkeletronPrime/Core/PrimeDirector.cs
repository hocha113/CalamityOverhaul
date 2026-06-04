namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    internal static class PrimeDirector
    {
        public const float MissingLimbChargeBonus = 0.5f;
        public const float MissingHeavyLimbChargeBonus = 1f;
        public const float DeathChargeMultiplier = 2f;
        public const int NormalArmChargeThreshold = 180;
        public const int MasterArmChargeThreshold = 120;
        public const int DeathArmChargeThreshold = 60;

        public static int GetArmChargeThreshold(bool masterMode, bool death) {
            if (death) {
                return DeathArmChargeThreshold;
            }

            return masterMode ? MasterArmChargeThreshold : NormalArmChargeThreshold;
        }

        public static float GetMissingLimbChargeBonus(bool firstAlive, bool secondAlive, bool thirdAlive, float missingBonus = MissingLimbChargeBonus) {
            float bonus = 0f;
            if (!firstAlive) {
                bonus += missingBonus;
            }
            if (!secondAlive) {
                bonus += missingBonus;
            }
            if (!thirdAlive) {
                bonus += missingBonus;
            }

            return bonus;
        }
    }
}
