using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core
{
    /// <summary>状态上下文</summary>
    internal class DeerclopsStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 战斗进度
        /// <summary>血量≤55%后的第二阶段(由 npc.ai[0] 位标同步)</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>阶段转换演出是否已播</summary>
        public bool PhaseRoarDone { get; set; }
        /// <summary>白澈大招是否已用</summary>
        public bool WhiteoutUsed { get; set; }
        public bool IsAsuraMode { get; set; }
        /// <summary>出招环索引</summary>
        public int AttackPhaseIndex { get; set; }
        /// <summary>上次投技结束的 GameUpdateCount 戳(服务端选招用，不同步)</summary>
        public int GrabLastEndStamp { get; set; } = -1000000;
        /// <summary>死亡演出完成，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 运动命令（状态声明，主控 UpdateMovement 消费）
        /// <summary>完全接管速度(含垂直)，状态自管</summary>
        public bool SkipDefaultMovement { get; set; }
        /// <summary>驻足(水平摩擦停但垂直物理照跑)</summary>
        public bool HaltMovement { get; set; }
        /// <summary>行走速度倍率</summary>
        public float MoveSpeedMult { get; set; } = 1f;
        /// <summary>水平目标覆盖，NaN=追目标</summary>
        public float TargetXOverride { get; set; } = float.NaN;
        /// <summary>强制朝向，0=运动自决</summary>
        public int ForcedDirection { get; set; }
        #endregion

        #region 动画命令（状态每帧声明，FindFrame 接管消费）
        public DeerAnimMode AnimMode { get; set; }
        /// <summary>攻击帧序时钟(状态自己推进)</summary>
        public int AnimTimer { get; set; }
        #endregion

        #region 视觉声明（客户端绘制/后效读取）
        /// <summary>暴风雪目标强度 0~1，状态每帧声明</summary>
        public float VeilTarget { get; set; } = 0.45f;
        /// <summary>凝视阶段 0无 1警告 2惩罚窗，每帧声明</summary>
        public int GazePhase { get; set; }
        /// <summary>独眼亮度 0~1</summary>
        public float EyeGlow { get; set; }
        /// <summary>独眼热度 0白→1血红</summary>
        public float EyeHeat { get; set; }
        /// <summary>身体前倾角(冲撞用)</summary>
        public float BodyLean { get; set; }
        /// <summary>本体溶解度 0实体→1消散(入场/退场/死亡)</summary>
        public float Dissolve { get; set; }
        /// <summary>白澈领域强度 0~1</summary>
        public float Whiteout { get; set; }
        /// <summary>暗影护盾侵蚀 0~30，主控计算</summary>
        public float ShadowShield { get; set; }
        #endregion

        /// <summary>状态退出时清除一次性命令</summary>
        public void ResetPerStateCommands() {
            SkipDefaultMovement = false;
            HaltMovement = false;
            MoveSpeedMult = 1f;
            TargetXOverride = float.NaN;
            ForcedDirection = 0;
            AnimMode = DeerAnimMode.Locomotion;
            AnimTimer = 0;
            GazePhase = 0;
            BodyLean = 0f;
            if (Npc != null) {
                Npc.damage = Npc.defDamage;
            }
        }
    }
}
