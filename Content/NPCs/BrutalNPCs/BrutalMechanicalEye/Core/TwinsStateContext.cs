using InnoVault.StateMachines;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>
    /// 双子魔眼状态上下文
    /// 存储状态机运行所需的共享数据
    /// </summary>
    internal class TwinsStateContext : INpcStateContext
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
            ComboSignal = -1;
            ComboSharedStep = 0;
            ComboReadyMask = 0;
        }

        /// <summary>
        /// 当前请求的合击状态索引(<see cref="TwinsStateIndex"/>)，-1表示无合击请求。
        /// 先到达合击节点的眼睛发起请求并直接进入合击；另一只眼在锚点状态察觉信号后立即跟进，
        /// 合击状态的集合阶段会等待双方到齐，取代旧的"步数巧合同步"
        /// </summary>
        public static int ComboSignal { get; set; } = -1;

        /// <summary>
        /// 合击共享的连招步数，保证双方退出合击后停留在同一套路进度
        /// </summary>
        public static int ComboSharedStep { get; set; }

        /// <summary>
        /// 合击就绪掩码：bit0=魔焰眼集合完成，bit1=激光眼集合完成。
        /// 双方都就绪后合击才同步推进，确保对撞/夹剪等动作完全同拍
        /// </summary>
        public static int ComboReadyMask { get; set; }

        /// <summary>
        /// 标记本眼已完成合击集合
        /// </summary>
        public static void MarkComboReady(bool isSpazmatism) => ComboReadyMask |= isSpazmatism ? 1 : 2;

        /// <summary>
        /// 双眼是否都已完成合击集合
        /// </summary>
        public static bool BothComboReady => (ComboReadyMask & 3) == 3;

        /// <summary>
        /// 发起合击请求(不覆盖已有请求)
        /// </summary>
        public static void RequestCombo(TwinsStateIndex stateIndex, int comboStep) {
            if (ComboSignal == -1) {
                ComboSignal = (int)stateIndex;
                ComboSharedStep = comboStep;
                ComboReadyMask = 0;
            }
        }

        /// <summary>
        /// 清除合击请求(合击状态退出时调用)
        /// </summary>
        public static void ClearComboSignal() {
            ComboSignal = -1;
            ComboReadyMask = 0;
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
        /// 是否处于二阶段
        /// </summary>
        public bool IsSecondPhase { get; set; }
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
        /// <summary>
        /// 死亡演出是否已播放完毕。<see cref="States.Common.TwinsDeathState"/> 在演出结束时置为 true，
        /// 控制器的 CheckDead 据此放行真正的死亡（之前一律锁血拦截）。每只眼睛各自独立。
        /// </summary>
        public bool DeathPerformanceFinished { get; set; }
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

        #region 冲刺视觉数据
        /// <summary>
        /// 速度拉伸强度(0~1)，渲染层据此对本体做沿速度方向的squash&amp;stretch。
        /// 状态推高数值，控制器每帧自动衰减
        /// </summary>
        public float DashStretch { get; set; }
        /// <summary>
        /// 残影增强(0~1)，渲染层据此提升残影密度与亮度。
        /// 状态推高数值，控制器每帧自动衰减
        /// </summary>
        public float AfterimageBoost { get; set; }

        /// <summary>
        /// 每帧衰减冲刺视觉数据(控制器调用)
        /// </summary>
        public void DecayDashVisuals() {
            DashStretch *= 0.88f;
            AfterimageBoost *= 0.93f;
            if (DashStretch < 0.01f) {
                DashStretch = 0f;
            }
            if (AfterimageBoost < 0.01f) {
                AfterimageBoost = 0f;
            }
        }

        /// <summary>
        /// 推高冲刺视觉强度
        /// </summary>
        public void PushDashVisuals(float stretch, float afterimage) {
            if (stretch > DashStretch) {
                DashStretch = stretch;
            }
            if (afterimage > AfterimageBoost) {
                AfterimageBoost = afterimage;
            }
        }
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
