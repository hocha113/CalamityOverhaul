namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    internal static class PrimeDirector
    {
        /// <summary>冲撞/闪现类预警帧数</summary>
        public const int DashTelegraphFrames = 36;
        /// <summary>光束类预警帧数</summary>
        public const int BeamTelegraphFrames = 90;
        /// <summary>武装阶段指挥 hub 悬停帧数</summary>
        public const int CommandHubFrames = 120;
        /// <summary>狂暴 connector 帧数</summary>
        public const int RageConnectorFrames = 75;
        /// <summary>战术指令广播持续帧数</summary>
        public const int CommandExecuteFrames = 90;
        /// <summary>转移后弹速热身比例（首帧→满速）</summary>
        public const float ProjectileWarmupStart = 0.2f;
        /// <summary>冲刺接触伤害速度门槛 px/帧</summary>
        public const float DashContactSpeedThreshold = 20f;
        /// <summary>发射后坐 px/帧</summary>
        public const float FireRecoil = 6f;
        /// <summary>重击后坐 px/帧</summary>
        public const float HeavyRecoil = 35f;

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
