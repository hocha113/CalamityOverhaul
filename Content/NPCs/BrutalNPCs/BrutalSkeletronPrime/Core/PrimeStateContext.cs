using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>
    /// 机械骷髅王头部状态上下文，存储状态机运行所需的共享数据
    /// </summary>
    internal class PrimeStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public HeadPrimeAI Owner { get; set; }
        #endregion

        #region 战场事实（由控制器每帧刷新）
        public bool BossRush { get; set; }
        public bool DeathMode { get; set; }
        public bool MasterMode { get; set; }
        public bool CannonAlive { get; set; }
        public bool ViceAlive { get; set; }
        public bool SawAlive { get; set; }
        public bool LaserAlive { get; set; }
        public bool NoArm => !CannonAlive && !ViceAlive && !SawAlive && !LaserAlive;
        public bool NoEye { get; set; }
        /// <summary>场上 SetPosingStarm（毁灭者协奏领域）数量</summary>
        public int StormCount { get; set; }
        #endregion

        #region 出招编排
        /// <summary>武装阶段固定出招序列索引</summary>
        public int AttackPhaseIndex { get; set; }
        /// <summary>狂暴阶段固定出招序列索引</summary>
        public int RageAttackIndex { get; set; }
        /// <summary>
        /// 死亡演出是否已经播放完毕。<see cref="States.PrimeDeathState"/> 在演出结束时置为 true，
        /// <see cref="HeadPrimeAI.CheckDead"/> 据此放行真正的死亡（之前一律锁血拦截）
        /// </summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>由金币枪狂怒进入脱战时为 true，离场时播放嘲讽台词</summary>
        public bool DespawnFromCoinFury { get; set; }
        #endregion

        #region 蓄力特效数据
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型: 0=无 1=冲撞蓄力 2=过载转阶段 3=环形爆发充能</summary>
        public int ChargeType { get; set; }
        /// <summary>冲撞方向（用于预警与视觉）</summary>
        public Vector2 DashDirection { get; set; }
        #endregion

        #region 动画数据
        /// <summary>帧组: 0=常态(0-3) 1=冲撞(4-7) 2=狂暴(8-11)</summary>
        public int FrameMode { get; set; }
        #endregion

        #region 死亡演出数据（供钳子 Actor 与运镜层读取）
        public int DeathTimer { get; set; }
        public int DeathTargetIndex { get; set; } = -1;
        public PrimeDeathPhase DeathPhase { get; set; }
        #endregion

        /// <summary>编队旋转时钟（头部每帧自增，供机械臂环绕编队取角）</summary>
        public ref float OrbitClock => ref Owner.ai[PrimeAiSlots.OverrideOrbitClock];
        /// <summary>传送恢复计时（由 SetPosingStarm.OnKill 写入）</summary>
        public ref float TeleportTimer => ref Owner.ai[PrimeAiSlots.OverrideTeleportTimer];

        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
            ChargeType = 0;
        }

        public void SetChargeState(int type, float progress) {
            IsCharging = true;
            ChargeType = type;
            ChargeProgress = MathHelper.Clamp(progress, 0f, 1f);
        }
    }
}
