namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>战斗调参中心</summary>
    internal static class PrimeDirector
    {
        /// <summary>冲撞/闪现类预警帧数</summary>
        public static int DashTelegraphFrames => 36;
        /// <summary>光束类预警帧数</summary>
        public static int BeamTelegraphFrames => 90;
        /// <summary>武装阶段指挥 hub 悬停帧数</summary>
        public static int CommandHubFrames => 120;
        /// <summary>狂暴 connector 帧数</summary>
        public static int RageConnectorFrames => 75;
        /// <summary>战术指令广播持续帧数</summary>
        public static int CommandExecuteFrames => 90;
        /// <summary>转移后弹速热身比例（首帧→满速）</summary>
        public static float ProjectileWarmupStart => 0.2f;
        /// <summary>冲刺接触伤害速度门槛 px/帧</summary>
        public static float DashContactSpeedThreshold => 20f;
        /// <summary>发射后坐 px/帧</summary>
        public static float FireRecoil => 6f;
        /// <summary>重击后坐 px/帧</summary>
        public static float HeavyRecoil => 35f;

        /// <summary>每缺一臂充能加速</summary>
        public static float MissingLimbChargeBonus => 0.5f;
        /// <summary>重炮/激光缺臂额外加速</summary>
        public static float MissingHeavyLimbChargeBonus => 1f;
        /// <summary>修罗模式充能倍率</summary>
        public static float DeathChargeMultiplier => 2f;
        /// <summary>普通/专家臂充能阈值，帧</summary>
        public static int NormalArmChargeThreshold => 180;
        /// <summary>大师臂充能阈值，帧</summary>
        public static int MasterArmChargeThreshold => 120;
        /// <summary>修罗模式臂充能阈值，帧</summary>
        public static int DeathArmChargeThreshold => 60;

        public static int GetArmChargeThreshold(bool masterMode, bool asura) {
            if (asura) {
                return DeathArmChargeThreshold;
            }

            return masterMode ? MasterArmChargeThreshold : NormalArmChargeThreshold;
        }

        public static float GetMissingLimbChargeBonus(bool firstAlive, bool secondAlive, bool thirdAlive, float missingBonus = 0.5f) {
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
