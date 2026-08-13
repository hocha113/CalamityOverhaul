using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core
{
    /// <summary>状态上下文</summary>
    internal class FishronStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 运动参数（状态声明，主控 UpdateMovement 消费）
        public Vector2 TargetPosition { get; set; }
        public float MoveSpeed { get; set; }
        /// <summary>速度趋近加速度，SimpleFly 风格</summary>
        public float Accel { get; set; } = 0.5f;
        /// <summary>跳过常规运动（状态直控速度）</summary>
        public bool SkipDefaultMovement { get; set; }
        #endregion

        #region 战斗阶段
        /// <summary>死亡模式/BossRush 增压</summary>
        public bool IsDeathMode { get; set; }
        /// <summary>离开海域/太空的原版式激怒</summary>
        public bool IsLandEnraged { get; set; }
        /// <summary>二阶段狂化已启（单调锁存）</summary>
        public bool PhaseTwoStarted { get; set; }
        /// <summary>三阶段入夜已启（单调锁存）</summary>
        public bool PhaseThreeStarted { get; set; }
        /// <summary>低血大招已放，一场一次</summary>
        public bool MaelstromUsed { get; set; }
        /// <summary>投技冷却帧，主控每帧递减，选择器只在归零后放行</summary>
        public int GrabCooldown { get; set; }
        /// <summary>当前阶段 1/2/3</summary>
        public int Phase => PhaseThreeStarted ? 3 : PhaseTwoStarted ? 2 : 1;
        /// <summary>出招环索引</summary>
        public int AttackRingIndex { get; set; }
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        public float LifeRatio => Npc.lifeMax > 0 ? Npc.life / (float)Npc.lifeMax : 0f;
        #endregion

        #region 蓄力/预告视觉（每帧由状态声明）
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无 1冲刺 2吐息 3风暴</summary>
        public int ChargeType { get; set; }
        /// <summary>冲刺方向（预警线朝向）</summary>
        public Vector2 DashDirection { get; set; }
        #endregion

        #region 动画/表现（每帧重声明，未声明回落默认）
        /// <summary>帧命令 0自动游动 1咆哮定帧 2高速游动</summary>
        public int FrameCommand { get; set; }
        /// <summary>本帧风暴强度加成 0~1，叠加在阶段基准之上</summary>
        public float StormBoost { get; set; }
        #endregion

        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
            ChargeType = 0;
        }

        public void SetChargeState(int type, float progress) {
            IsCharging = true;
            ChargeType = type;
            ChargeProgress = progress;
        }

        /// <summary>阶段基准风暴强度：一阶段阴云，二阶段落雨，三阶段黑夜雷暴</summary>
        public float PhaseStormGrade => PhaseThreeStarted ? 1f : PhaseTwoStarted ? 0.6f : 0.25f;
    }
}
