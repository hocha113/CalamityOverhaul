using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>
    /// 双子魔眼状态上下文
    /// 存储状态机运行所需的共享数据
    /// </summary>
    internal class TwinsStateContext
    {
        #region 静态同步数据
        /// <summary>
        /// 是否已触发二阶段转换
        /// </summary>
        public static bool Phase2Triggered { get; set; }
        /// <summary>
        /// 触发二阶段的眼睛类型(用于确定谁先触发)
        /// </summary>
        public static int Phase2TriggerSource { get; set; }
        /// <summary>
        /// 二阶段转换计时器
        /// </summary>
        public static int Phase2TransitionTimer { get; set; }

        /// <summary>
        /// 重置静态同步数据
        /// </summary>
        public static void ResetSyncData() {
            Phase2Triggered = false;
            Phase2TriggerSource = 0;
            Phase2TransitionTimer = 0;
        }

        /// <summary>
        /// 触发二阶段转换
        /// </summary>
        public static void TriggerPhase2(int sourceType) {
            if (!Phase2Triggered) {
                Phase2Triggered = true;
                Phase2TriggerSource = sourceType;
                Phase2TransitionTimer = 0;
            }
        }

        /// <summary>
        /// 检查另一只眼睛是否存活
        /// </summary>
        public static NPC GetPartnerNpc(int myType) {
            int partnerType = myType == NPCID.Spazmatism ? NPCID.Retinazer : NPCID.Spazmatism;
            foreach (var n in Main.npc) {
                if (n.active && n.type == partnerType) {
                    return n;
                }
            }
            return null;
        }
        #endregion

        #region 核心引用
        /// <summary>
        /// NPC实例引用
        /// </summary>
        public NPC Npc { get; set; }
        /// <summary>
        /// 目标玩家引用
        /// </summary>
        public Player Target { get; set; }
        /// <summary>
        /// AI数组引用
        /// </summary>
        public float[] Ai { get; set; }
        #endregion

        #region 状态标记
        /// <summary>
        /// 是否处于随从模式
        /// </summary>
        public bool IsAccompanyMode { get; set; }
        /// <summary>
        /// 是否处于二阶段
        /// </summary>
        public bool IsSecondPhase { get; set; }
        /// <summary>
        /// 是否为机械叛乱模式
        /// </summary>
        public bool IsMachineRebellion { get; set; }
        /// <summary>
        /// 是否为死亡模式
        /// </summary>
        public bool IsDeathMode { get; set; }
        /// <summary>
        /// 是否为魔焰眼
        /// </summary>
        public bool IsSpazmatism { get; set; }
        /// <summary>
        /// 是否正在执行转阶段动画
        /// </summary>
        public bool IsInPhaseTransition { get; set; }
        /// <summary>
        /// 是否处于独眼狂暴模式(另一只眼睛已死亡)
        /// </summary>
        public bool IsSoloRageMode { get; set; }
        /// <summary>
        /// 独眼狂暴模式刚刚触发(用于状态切换)
        /// </summary>
        public bool SoloRageJustTriggered { get; set; }
        #endregion

        #region 蓄力特效数据
        /// <summary>
        /// 蓄力进度(0到1)
        /// </summary>
        public float ChargeProgress { get; set; }
        /// <summary>
        /// 是否正在蓄力
        /// </summary>
        public bool IsCharging { get; set; }
        /// <summary>
        /// 蓄力类型
        /// 0=无 1=冲刺蓄力 2=扇形激光蓄力 3=火焰漩涡蓄力 4=激光扫射蓄力
        /// </summary>
        public int ChargeType { get; set; }
        #endregion

        #region 动画数据
        /// <summary>
        /// 帧索引
        /// </summary>
        public int FrameIndex { get; set; }
        /// <summary>
        /// 帧计数器
        /// </summary>
        public int FrameCount { get; set; }
        #endregion

        /// <summary>
        /// 重置蓄力状态
        /// </summary>
        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
            ChargeType = 0;
        }

        /// <summary>
        /// 设置蓄力状态
        /// </summary>
        public void SetChargeState(int type, float progress) {
            IsCharging = true;
            ChargeType = type;
            ChargeProgress = progress;
        }
    }
}
