using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>
    /// 毁灭者状态上下文，存储状态机运行所需的共享数据
    /// </summary>
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
        /// <summary>
        /// 是否跳过常规运动（冲刺等需要直接控制速度的状态设为true）
        /// </summary>
        public bool SkipDefaultMovement { get; set; }
        /// <summary>
        /// 蛇形摆动强度 0~1：在航向上叠加正弦摆动，让蠕虫"游动"而非"漂移"。
        /// 每帧由状态重新声明（头部AI在状态机更新前清零），巡航类状态设为1
        /// </summary>
        public float SlitherStrength { get; set; }
        /// <summary>蛇形摆动相位累计（仅视觉/运动手感，轻微跨端漂移可被位置同步纠正）</summary>
        public float SlitherPhase { get; set; }
        /// <summary>
        /// 速度趋近率（每帧向目标速度的指数趋近系数），状态可调。
        /// 较低=重型机械的迟缓惯性，较高=灵敏响应
        /// </summary>
        public float AccelRate { get; set; } = 0.055f;
        #endregion

        #region 战斗状态
        public bool IsEnraged { get; set; }
        public bool IsDeathMode { get; set; }
        //固定出招顺序的当前索引
        public int AttackPhaseIndex { get; set; }
        /// <summary>
        /// 死亡演出是否已经播放完毕。<see cref="States.DestroyerDeathState"/> 在演出结束时置为 true，
        /// 头部 AI 的 CheckDead 据此放行真正的死亡（之前一律锁血拦截）。
        /// </summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 蓄力特效数据
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>
        /// 蓄力类型: 0=无 1=冲刺蓄力 2=激光弹幕充能 3=包围 4=探针阵列
        /// </summary>
        public int ChargeType { get; set; }
        /// <summary>
        /// 冲刺方向（用于瞄准线绘制）
        /// </summary>
        public Vector2 DashDirection { get; set; }
        /// <summary>
        /// 轨道绞杀演出模式: 0=无 1=蓄能撤离 2=高速俯冲 3=破土回场（影响热感滤镜与体节火花）
        /// </summary>
        public int OrbitalVisual { get; set; }
        #endregion

        #region 动画数据
        public int Frame { get; set; }
        public int GlowFrame { get; set; }
        public bool OpenMouth { get; set; }
        public int DontOpenMouthTime { get; set; }
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

        /// <summary>
        /// 更新体节列表
        /// </summary>
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
