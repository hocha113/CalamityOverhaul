using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>头部状态上下文</summary>
    internal class PrimeStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public HeadPrimeAI Owner { get; set; }
        #endregion

        #region 战场事实（由控制器每帧刷新）
        public bool BossRush { get; set; }
        public bool AsuraMode { get; set; }
        public bool MasterMode { get; set; }
        public bool CannonAlive { get; set; }
        public bool ViceAlive { get; set; }
        public bool SawAlive { get; set; }
        public bool LaserAlive { get; set; }
        public bool NoArm => !CannonAlive && !ViceAlive && !SawAlive && !LaserAlive;
        #endregion

        #region 出招编排
        /// <summary>武装阶段固定出招序列索引</summary>
        public int AttackPhaseIndex { get; set; }
        /// <summary>狂暴阶段固定出招序列索引</summary>
        public int RageAttackIndex { get; set; }
        /// <summary>死亡演出已完，CheckDead据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>金币枪狂怒后脱战</summary>
        public bool DespawnFromCoinFury { get; set; }
        #endregion

        #region 蓄力特效数据
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无/1冲撞/2过载/3环形</summary>
        public int ChargeType { get; set; }
        /// <summary>冲撞方向，供预警与视觉</summary>
        public Vector2 DashDirection { get; set; }
        #endregion

        #region 动画数据
        /// <summary>帧组 0常态/1冲撞/2狂暴</summary>
        public int FrameMode { get; set; }
        #endregion

        #region 死亡演出数据（供钳子 Actor 与运镜层读取）
        public int DeathTimer { get; set; }
        public int DeathTargetIndex { get; set; } = -1;
        public PrimeDeathPhase DeathPhase { get; set; }
        #endregion

        #region 投技演出数据（供四臂编排与被抓玩家侧读取，各端本地推进）
        public int ViceExecutionTick { get; set; }
        #endregion

        /// <summary>编队旋转时钟</summary>
        public ref float OrbitClock => ref Owner.ai[PrimeAiSlots.OverrideOrbitClock];

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
