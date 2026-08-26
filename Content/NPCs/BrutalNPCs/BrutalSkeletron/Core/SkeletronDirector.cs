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

        #region 合掌拍捉（投技）
        /// <summary>拍捉解锁血量比（好招压后）</summary>
        public static float SnatchLifeGate => 0.85f;
        /// <summary>拍捉命中后冷却帧</summary>
        public static int SnatchCooldownTicks => 2700;
        /// <summary>拍空后冷却帧（更快再试）</summary>
        public static int SnatchWhiffCooldownTicks => 1500;
        /// <summary>对峙预警帧数（公平阀 ≥40）</summary>
        public static int SnatchTelegraphFrames => 48;
        /// <summary>预警末拍锚点锁定读秒窗</summary>
        public static int SnatchAnchorLockFrames => 10;
        /// <summary>双掌对峙横距 px</summary>
        public static float SnatchFlankDistance => 560f;
        /// <summary>夹持后双掌半间距 px（囚笼半宽）</summary>
        public static float SnatchHalfGap => 46f;
        /// <summary>合拍闭合速度</summary>
        public static float SnatchSnapSpeed(bool death) => ClapSpeed(death) + 6f;
        /// <summary>夹持顿帧伤害基准（走难度缩放，受害端结算）</summary>
        public static int SnatchClampDamage => 24;
        /// <summary>砸地终结伤害基准（走难度缩放，受害端结算）</summary>
        public static int SnatchSlamDamage => 44;
        /// <summary>整套投技 Hurt 伤害预算（占玩家最大生命比，超限跳过终结伤害）</summary>
        public static float SnatchDamageBudget => 0.55f;
        /// <summary>释放后无敌帧</summary>
        public static int SnatchReleaseImmune => 90;
        #endregion

        #region 骨臂弹指（Hub 期间手部支援火力）
        /// <summary>弹指周期帧（左右手错半拍）</summary>
        public static int FlickPeriod => 96;
        /// <summary>弹指蓄势帧（卷腕拉弓）</summary>
        public static int FlickWindup => 16;
        /// <summary>弹指颅火速度（直线弹，不追踪）</summary>
        public static float FlickSkullSpeed(bool death) => death ? 7.6f : 6.8f;
        /// <summary>缺口（契约3）：贴身不弹指，近身是安全窗，弹指判定直接读取</summary>
        public static float FlickMinDistance => 300f;
        #endregion

        #region 嘲讽鼓掌
        /// <summary>缺口（契约3）：骨屑环朝玩家的扇区 ±该角永不发射（鼓掌不瞄人），发射循环直接读取</summary>
        public static float ApplauseGapHalfAngle => 0.55f;
        /// <summary>击掌骨屑环基数（第N击 +3N）</summary>
        public static int ApplauseRingCount => 10;
        /// <summary>击掌骨屑环速度（第N击 +0.5N）</summary>
        public static float ApplauseRingSpeed(bool death) => death ? 4.4f : 3.8f;
        #endregion
    }
}
