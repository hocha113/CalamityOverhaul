using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core
{
    /// <summary>
    /// 毁灭者状态上下文，存储状态机运行所需的共享数据
    /// </summary>
    internal class DestroyerStateContext
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
        #endregion

        #region 战斗状态
        public bool IsEnraged { get; set; }
        public bool IsMachineRebellion { get; set; }
        public bool IsDeathMode { get; set; }
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
