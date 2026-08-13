using InnoVault.StateMachines;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core
{
    /// <summary>状态机共享上下文</summary>
    internal class TwinsStateContext : INpcStateContext
    {
        #region 静态同步数据
        /// <summary>二阶段已触发</summary>
        public static bool Phase2Triggered { get; set; }
        /// <summary>谁先触发二阶段(NPC type)</summary>
        public static int Phase2TriggerSource { get; set; }
        /// <summary>二阶段转换计时</summary>
        public static int Phase2TransitionTimer { get; set; }

        /// <summary>提前狂暴名额占用者(NPC type)，0=未占用</summary>
        public static int EarlyRageClaimedBy { get; set; }

        /// <summary>重置静态同步</summary>
        public static void ResetSyncData() {
            Phase2Triggered = false;
            Phase2TriggerSource = 0;
            Phase2TransitionTimer = 0;
            ComboSignal = -1;
            ComboSharedStep = 0;
            ComboReadyMask = 0;
            EarlyRageClaimedBy = 0;
            ResetPincerData();
            PincerLastEndUpdate = 0;
        }

        #region 钳形投技共享记录(仅服务端权威；客户端一律以 override ai 槽同步值为准)
        /// <summary>被夹玩家 whoAmI，-1 无</summary>
        public static int PincerGrabbedPlayer { get; set; } = -1;
        /// <summary>投技节拍，见 TwinsPincerGrabState.Beat*</summary>
        public static int PincerBeat { get; set; }
        /// <summary>钳口交扣点</summary>
        public static Vector2 PincerClampPoint { get; set; }
        /// <summary>钳形轴线角(魔焰→交点方向)</summary>
        public static float PincerLineAngle { get; set; }
        /// <summary>上次投技结束时的 Main.GameUpdateCount，冷却基准</summary>
        public static uint PincerLastEndUpdate { get; set; }
        /// <summary>上次投技是否扑空，扑空冷却减半</summary>
        public static bool PincerLastWasWhiff { get; set; }
        /// <summary>被抓瞬间双眼血量合计，救援阀基准</summary>
        public static int PincerEyesLifeAtClamp { get; set; }

        /// <summary>清空一次投技过程的记录，冷却戳另行处理</summary>
        public static void ResetPincerData() {
            PincerGrabbedPlayer = -1;
            PincerBeat = 0;
            PincerClampPoint = Vector2.Zero;
            PincerLineAngle = 0f;
            PincerEyesLifeAtClamp = 0;
        }
        #endregion

        /// <summary>抢占提前狂暴名额，只有一只眼能在搭档濒死时提前狂暴</summary>
        public static bool TryClaimEarlyRage(int myType) {
            if (EarlyRageClaimedBy == 0) {
                EarlyRageClaimedBy = myType;
                return true;
            }
            return EarlyRageClaimedBy == myType;
        }

        /// <summary>合击索引(TwinsStateIndex)，-1=无</summary>
        public static int ComboSignal { get; set; } = -1;

        /// <summary>合击共享 comboStep</summary>
        public static int ComboSharedStep { get; set; }

        /// <summary>合击就绪掩码 bit0魔焰 bit1激光</summary>
        public static int ComboReadyMask { get; set; }

        /// <summary>本眼合击集合完成</summary>
        public static void MarkComboReady(bool isSpazmatism) => ComboReadyMask |= isSpazmatism ? 1 : 2;

        /// <summary>双眼集合都完成</summary>
        public static bool BothComboReady => (ComboReadyMask & 3) == 3;

        /// <summary>发起合击，不覆盖已有</summary>
        public static void RequestCombo(TwinsStateIndex stateIndex, int comboStep) {
            if (ComboSignal == -1) {
                ComboSignal = (int)stateIndex;
                ComboSharedStep = comboStep;
                ComboReadyMask = 0;
            }
        }

        /// <summary>清合击请求</summary>
        public static void ClearComboSignal() {
            ComboSignal = -1;
            ComboReadyMask = 0;
        }

        /// <summary>触发二阶段</summary>
        public static void TriggerPhase2(int sourceType) {
            if (!Phase2Triggered) {
                Phase2Triggered = true;
                Phase2TriggerSource = sourceType;
                Phase2TransitionTimer = 0;
            }
        }

        /// <summary>取搭档 NPC</summary>
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
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public float[] Ai { get; set; }
        #endregion

        #region 状态标记
        public bool IsSecondPhase { get; set; }
        public bool IsDeathMode { get; set; }
        public bool IsSpazmatism { get; set; }
        /// <summary>转阶段中</summary>
        public bool IsInPhaseTransition { get; set; }
        /// <summary>独眼狂暴</summary>
        public bool IsSoloRageMode { get; set; }
        /// <summary>独眼刚触发，切态用</summary>
        public bool SoloRageJustTriggered { get; set; }
        /// <summary>死亡演出完，CheckDead 放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 蓄力特效数据
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无 1冲刺 2扇形激光 3火焰漩涡 4激光扫射</summary>
        public int ChargeType { get; set; }
        #endregion

        #region 动画数据
        public int FrameIndex { get; set; }
        public int FrameCount { get; set; }
        #endregion

        #region 冲刺视觉数据
        /// <summary>速度拉伸 0~1，状态推高，控制器衰减</summary>
        public float DashStretch { get; set; }
        /// <summary>残影增强 0~1，状态推高，控制器衰减</summary>
        public float AfterimageBoost { get; set; }

        /// <summary>每帧衰减冲刺视觉</summary>
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

        /// <summary>推高冲刺视觉</summary>
        public void PushDashVisuals(float stretch, float afterimage) {
            if (stretch > DashStretch) {
                DashStretch = stretch;
            }
            if (afterimage > AfterimageBoost) {
                AfterimageBoost = afterimage;
            }
        }
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
    }
}
