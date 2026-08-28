using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core
{
    /// <summary>躯干状态上下文</summary>
    internal class GolemStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public GolemBodyAI Owner { get; set; }
        #endregion

        #region 战场事实（由控制器每帧刷新）
        public bool BossRush { get; set; }
        public bool AsuraMode { get; set; }
        public bool MasterMode { get; set; }
        /// <summary>神庙外/地表上激怒</summary>
        public bool Enraged { get; set; }
        public GolemLimbStatus Limbs { get; set; }
        /// <summary>二阶段（头部已分离）</summary>
        public bool Sundered => (int)Npc.ai[GolemAiSlots.BodyPhase] >= GolemPhase.Sundered;
        /// <summary>缺拳数，狂暴化乘算用</summary>
        public int MissingFists => 2 - Limbs.FistCount;
        #endregion

        #region 出招编排
        /// <summary>一阶段固定出招序列索引</summary>
        public int AttackIndexP1 { get; set; }
        /// <summary>二阶段固定出招序列索引</summary>
        public int AttackIndexP2 { get; set; }
        /// <summary>大招后强化循环</summary>
        public bool PostUltRage { get; set; }
        /// <summary>死亡演出已完，CheckDead 据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>上次投技尝试帧（服务端冷却裁决，0=从未）</summary>
        public uint LastGrabTick { get; set; }
        #endregion

        #region 蓄力/表现数据（状态写入，绘制读取）
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无/1宝石充能/2太阳过载</summary>
        public int ChargeType { get; set; }
        /// <summary>岩浆脉络强度 0~1（表现层）</summary>
        public float VeinGlow { get; set; }
        /// <summary>帧组 0待机/1蹲伏蓄力/2跃空</summary>
        public int FrameMode { get; set; }
        #endregion

        #region 死亡演出数据（供运镜与部件读取，各端本地推进）
        public int DeathTimer { get; set; }
        public GolemDeathPhase DeathPhase { get; set; }
        #endregion

        /// <summary>大招落点/陨落锁定点（Override.ai 同步）</summary>
        public Vector2 LockPoint {
            get => new(Owner.ai[GolemAiSlots.OverrideLockX], Owner.ai[GolemAiSlots.OverrideLockY]);
            set {
                Owner.ai[GolemAiSlots.OverrideLockX] = value.X;
                Owner.ai[GolemAiSlots.OverrideLockY] = value.Y;
            }
        }

        /// <summary>大招是否已释放（Override.ai 同步）</summary>
        public bool UltFired {
            get => Owner.ai[GolemAiSlots.OverrideUltFired] != 0f;
            set => Owner.ai[GolemAiSlots.OverrideUltFired] = value ? 1f : 0f;
        }

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

    /// <summary>死亡演出阶段</summary>
    internal enum GolemDeathPhase : int
    {
        /// <summary>踉跄跪地</summary>
        Stagger = 0,
        /// <summary>裂纹蔓延</summary>
        Crack = 1,
        /// <summary>自上而下崩解</summary>
        Collapse = 2,
        /// <summary>太阳宝石谢幕</summary>
        GemFinale = 3,
    }
}
