using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core
{
    /// <summary>头部状态上下文</summary>
    internal class SkeletronStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public SkeletronHeadAI Owner { get; set; }
        #endregion

        #region 战场事实（控制器每帧刷新）
        public bool BossRush { get; set; }
        public bool DeathMode { get; set; }
        public bool MasterMode { get; set; }
        public int HandCount { get; set; }
        public NPC LeftHand { get; set; }
        public NPC RightHand { get; set; }
        public bool AnyHandAlive => HandCount > 0;
        #endregion

        #region 出招编排（仅权威端消费）
        /// <summary>一阶段固定出招序列索引</summary>
        public int AttackIndexP1 { get; set; }
        /// <summary>二阶段固定出招序列索引</summary>
        public int AttackIndexP2 { get; set; }
        /// <summary>低血大招已使用</summary>
        public bool UltUsed { get; set; }
        /// <summary>死亡演出已完，CheckDead 据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 视觉数据（各端本地驱动）
        /// <summary>眼火强度 0~1.5，绘制层消费</summary>
        public float EyeFlame { get; set; } = 1f;
        /// <summary>二阶段诅咒火之冠强度 0~1</summary>
        public float CrownFlame { get; set; }
        /// <summary>旋杀涡流强度 0~1</summary>
        public float SpinVortex { get; set; }
        /// <summary>涡流向心汇聚度 0~1（仪式/大招蓄力用）</summary>
        public float VortexConverge { get; set; }
        /// <summary>冲刺预警强度 0~1（沿 npc.ai[3] 角度画线）</summary>
        public float DashTelegraph { get; set; }
        /// <summary>帧组 0常态/1旋转怒相</summary>
        public int FrameMode { get; set; }
        /// <summary>死亡演出计时（各端本地推进）</summary>
        public int DeathTimer { get; set; }
        #endregion

        /// <summary>编队旋转时钟</summary>
        public ref float OrbitClock => ref Owner.ai[SkeletronAiSlots.OverrideOrbitClock];

        /// <summary>状态离场时清空瞬态视觉</summary>
        public void ResetTransientVisuals() {
            SpinVortex = 0f;
            VortexConverge = 0f;
            DashTelegraph = 0f;
        }
    }
}
