namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core
{
    /// <summary>战斗调参中心</summary>
    internal static class SkeletronDirector
    {
        /// <summary>旋杀/瞬移类预警帧数（固定常数供玩家内化）</summary>
        public static int DashTelegraphFrames => 36;
        /// <summary>手掌砸击预警帧数</summary>
        public static int SlamTelegraphFrames => 30;
        /// <summary>连接件 hub 悬停帧数</summary>
        public static int HubFrames => 84;
        /// <summary>出招门闸预告帧数</summary>
        public static int TelegraphLead => 26;

        /// <summary>旋杀冲刺速度</summary>
        public static float SpinDashSpeed(bool death, bool p2) => (death ? 27f : 23.5f) + (p2 ? 2.5f : 0f);
        /// <summary>旋杀接触伤害倍率</summary>
        public static float SpinDamageMult => 1.3f;
        /// <summary>旋杀冲刺接触伤害速度门槛 px/帧</summary>
        public static float DashContactSpeedThreshold => 15f;

        /// <summary>手掌砸击俯冲速度</summary>
        public static float SlamSpeed(bool death) => death ? 44f : 38f;
        /// <summary>合拍钳杀合拢速度</summary>
        public static float ClapSpeed(bool death) => death ? 42f : 36f;

        /// <summary>幽灵臂扑抓速度</summary>
        public static float GhostLungeSpeed(bool death) => death ? 42f : 36f;
        /// <summary>幽灵臂横扫速度</summary>
        public static float GhostSweepSpeed(bool death) => death ? 17f : 14f;

        /// <summary>低血大招触发血量比</summary>
        public static float UltLifeRatio => 0.25f;
        /// <summary>转阶段触发血量比</summary>
        public static float PhaseLifeRatio => 0.55f;
        /// <summary>死亡演出触发血量</summary>
        public static int DeathTriggerLife => 10;

        /// <summary>二阶段节奏倍率（hub缩短等）</summary>
        public static float P2TempoMult => 0.8f;
    }
}
