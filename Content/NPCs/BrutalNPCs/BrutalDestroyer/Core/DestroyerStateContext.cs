using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>状态上下文</summary>
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
        /// <summary>跳过常规运动(直控速度)</summary>
        public bool SkipDefaultMovement { get; set; }
        /// <summary>蛇形摆动 0~1，每帧声明</summary>
        public float SlitherStrength { get; set; }
        /// <summary>蛇形相位累计</summary>
        public float SlitherPhase { get; set; }
        /// <summary>速度趋近率，低=重惯</summary>
        public float AccelRate { get; set; } = 0.055f;
        #endregion

        #region 战斗状态
        public bool IsEnraged { get; set; }
        public bool IsDeathMode { get; set; }
        //出招环索引
        public int AttackPhaseIndex { get; set; }
        /// <summary>激怒环已启，过半血首招轨道绞杀</summary>
        public bool EnrageCycleStarted { get; set; }
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 蓄力特效数据
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无1冲刺2激光3包围4探针</summary>
        public int ChargeType { get; set; }
        /// <summary>冲刺方向(预警线)</summary>
        public Vector2 DashDirection { get; set; }
        /// <summary>轨道演出 0无1撤离2俯冲3回场</summary>
        public int OrbitalVisual { get; set; }
        #endregion

        #region 动画数据
        public int Frame { get; set; }
        public int GlowFrame { get; set; }
        public bool OpenMouth { get; set; }
        public int DontOpenMouthTime { get; set; }
        /// <summary>下颚 0自动1强张2咬合，每帧声明</summary>
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

        /// <summary>刷新体节</summary>
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
