using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>毁灭者状态上下文</summary>
    internal class DestroyerStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public List<NPC> BodySegments { get; set; } = [];
        #endregion

        #region 运动参数（由状态设置，主控制器的UpdateMovement消费）
        public Vector2 TargetPosition { get; set; }
        public float MoveSpeed { get; set; }
        public float TurnSpeed { get; set; }
        /// <summary>跳过常规运动（冲刺等直控速度）</summary>
        public bool SkipDefaultMovement { get; set; }
        /// <summary>蛇形摆动 0~1，航向正弦扰动；每帧由状态声明</summary>
        public float SlitherStrength { get; set; }
        /// <summary>蛇形相位累计（视觉/手感，轻微跨端漂移可同步纠正）</summary>
        public float SlitherPhase { get; set; }
        /// <summary>速度趋近率；低=重型惯性，高=灵敏</summary>
        public float AccelRate { get; set; } = 0.055f;
        #endregion

        #region 战斗状态
        public bool IsEnraged { get; set; }
        public bool IsDeathMode { get; set; }
        //固定出招顺序的当前索引
        public int AttackPhaseIndex { get; set; }
        /// <summary>激怒出招环是否已启动（首次过50%血量时归零出招索引，保证激怒首招为轨道绞杀）</summary>
        public bool EnrageCycleStarted { get; set; }
        /// <summary>死亡演出已完；DestroyerHeadAI.CheckDead 据此放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 蓄力特效数据
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型: 0=无 1=冲刺蓄力 2=激光弹幕充能 3=包围 4=探针阵列</summary>
        public int ChargeType { get; set; }
        /// <summary>冲刺方向，预警线绘制</summary>
        public Vector2 DashDirection { get; set; }
        /// <summary>轨道绞杀演出模式: 0=无 1=蓄能撤离 2=高速俯冲 3=破土回场（影响热感滤镜与体节火花）</summary>
        public int OrbitalVisual { get; set; }
        #endregion

        #region 动画数据
        public int Frame { get; set; }
        public int GlowFrame { get; set; }
        public bool OpenMouth { get; set; }
        public int DontOpenMouthTime { get; set; }
        /// <summary>下颚指令：0=自动 1=强制张口 2=猛然咬合；每帧由状态声明</summary>
        public int JawCommand { get; set; }
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

        /// <summary>刷新体节列表</summary>
        public void RefreshBodySegments() {
            BodySegments.Clear();
            foreach (var n in Main.ActiveNPCs) {
                if ((n.type == NPCID.TheDestroyerBody || n.type == NPCID.TheDestroyerTail) && n.realLife == Npc.whoAmI) {
                    BodySegments.Add(n);
                }
            }
        }
    }
}
