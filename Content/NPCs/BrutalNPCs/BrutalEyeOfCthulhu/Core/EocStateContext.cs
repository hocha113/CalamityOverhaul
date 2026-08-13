using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core
{
    /// <summary>状态机共享上下文</summary>
    internal class EocStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 阶段与模式
        /// <summary>转阶段血量阈值</summary>
        public float Phase2Ratio => IsDeathMode ? 0.62f : 0.55f;
        /// <summary>低血大招阈值</summary>
        public float LowPhaseRatio => 0.25f;

        public bool IsDeathMode { get; set; }
        /// <summary>口器阶段</summary>
        public bool IsSecondPhase { get; set; }
        /// <summary>低血段</summary>
        public bool IsLowPhase { get; set; }
        /// <summary>转阶段演出进行中</summary>
        public bool IsInPhaseTransition { get; set; }
        /// <summary>死亡演出完，CheckDead 放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>大招冷却帧，权威端递减</summary>
        public int MaelstromCooldown { get; set; }
        /// <summary>大招至少放过一次</summary>
        public bool MaelstromPlayed { get; set; }
        /// <summary>白昼狂暴强度 0~1，各端由已同步的昼夜状态确定性推导</summary>
        public float EnrageRamp { get; set; }
        #endregion

        #region 蓄力/前摇视觉
        /// <summary>蓄力类型 0无 1冲刺 2雾伏击 3口器 4大招</summary>
        public int ChargeType { get; set; }
        public bool IsCharging { get; set; }
        public float ChargeProgress { get; set; }

        public void SetChargeState(int type, float progress) {
            IsCharging = true;
            ChargeType = type;
            ChargeProgress = MathHelper.Clamp(progress, 0f, 1f);
        }

        public void ResetChargeState() {
            IsCharging = false;
            ChargeType = 0;
            ChargeProgress = 0f;
        }
        #endregion

        #region 本体视觉数据（各端本地驱动）
        /// <summary>虹膜辉光强度 0~1，状态推高，控制器衰减</summary>
        public float IrisGlow { get; set; }
        /// <summary>虹膜辉光颜色</summary>
        public Color IrisColor { get; set; } = new(255, 60, 48);
        /// <summary>血带拖尾热度 0~1</summary>
        public float TrailHeat { get; set; }
        /// <summary>残影增强 0~1</summary>
        public float AfterimageBoost { get; set; }
        /// <summary>雾中隐匿 0~1，1=几乎不可见</summary>
        public float FogHide { get; set; }
        /// <summary>雾隐目标值，状态声明，控制器缓动</summary>
        public float FogHideGoal { get; set; }
        /// <summary>撕皮进度 0~1，转阶段演出用</summary>
        public float SkinTear { get; set; }
        /// <summary>身体缩放演出值</summary>
        public float ScalePulse { get; set; } = 1f;

        /// <summary>动画帧 0~2，绘制时按阶段偏移</summary>
        public int FrameIndex { get; set; }
        public int FrameCounter { get; set; }
        /// <summary>帧速，口器阶段狂化时提速</summary>
        public int FrameRate { get; set; } = 6;

        /// <summary>冲刺车道预警：强度 0~1</summary>
        public float LaneIntensity { get; set; }
        /// <summary>车道起点（世界）</summary>
        public Vector2 LaneStart { get; set; }
        /// <summary>车道方向（单位）</summary>
        public Vector2 LaneDir { get; set; }
        /// <summary>车道长度 px</summary>
        public float LaneLength { get; set; } = 1500f;
        /// <summary>车道充能进度 0~1，1=起跑</summary>
        public float LaneProgress { get; set; }

        /// <summary>推高冲刺视觉</summary>
        public void PushDashVisuals(float trail, float afterimage) {
            if (trail > TrailHeat) {
                TrailHeat = trail;
            }
            if (afterimage > AfterimageBoost) {
                AfterimageBoost = afterimage;
            }
        }

        /// <summary>推高虹膜辉光</summary>
        public void PushIris(float glow, Color color) {
            if (glow >= IrisGlow) {
                IrisGlow = glow;
                IrisColor = color;
            }
        }

        /// <summary>每帧衰减与缓动，控制器调用</summary>
        public void DecayVisuals() {
            TrailHeat *= 0.90f;
            AfterimageBoost *= 0.93f;
            IrisGlow *= 0.88f;
            LaneIntensity *= 0.86f;
            SkinTear *= IsInPhaseTransition ? 1f : 0.94f;
            FogHide = MathHelper.Lerp(FogHide, FogHideGoal, 0.12f);
            ScalePulse = MathHelper.Lerp(ScalePulse, 1f, 0.1f);
            if (TrailHeat < 0.01f) {
                TrailHeat = 0f;
            }
            if (AfterimageBoost < 0.01f) {
                AfterimageBoost = 0f;
            }
            if (IrisGlow < 0.01f) {
                IrisGlow = 0f;
            }
            if (LaneIntensity < 0.01f) {
                LaneIntensity = 0f;
            }
        }
        #endregion

        #region 攻击洗牌袋（仅权威端使用）
        private readonly List<EocStateIndex> attackBag = [];
        private EocStateIndex lastAttack = EocStateIndex.VeilHover;

        /// <summary>当前阶段攻击池</summary>
        private void FillPool(List<EocStateIndex> pool) {
            pool.Clear();
            if (!IsSecondPhase) {
                pool.Add(EocStateIndex.FeintDash);
                pool.Add(EocStateIndex.FogAmbush);
                pool.Add(EocStateIndex.ServantLance);
                pool.Add(EocStateIndex.BloodFountain);
            }
            else {
                pool.Add(EocStateIndex.MawFrenzy);
                pool.Add(EocStateIndex.FogAmbush);
                pool.Add(EocStateIndex.ServantEncircle);
                pool.Add(EocStateIndex.BlindsideCross);
                pool.Add(EocStateIndex.FeintDash);
                if (IsLowPhase) {
                    pool.Add(EocStateIndex.ServantLance);
                }
            }
        }

        /// <summary>抽下一招：低血强制首个大招，其后按冷却；洗牌袋防复读</summary>
        public EocStateIndex NextAttack() {
            //低血大招：首次必放，其后冷却到 0 才回池
            if (IsLowPhase && MaelstromCooldown <= 0
                && (!MaelstromPlayed || Main.rand.NextBool(3))) {
                return EocStateIndex.Maelstrom;
            }

            if (attackBag.Count == 0) {
                FillPool(attackBag);
                //洗牌，且袋首不与上招重复
                for (int i = attackBag.Count - 1; i > 0; i--) {
                    int j = Main.rand.Next(i + 1);
                    (attackBag[i], attackBag[j]) = (attackBag[j], attackBag[i]);
                }
                if (attackBag.Count > 1 && attackBag[0] == lastAttack) {
                    (attackBag[0], attackBag[^1]) = (attackBag[^1], attackBag[0]);
                }
            }

            EocStateIndex next = attackBag[0];
            attackBag.RemoveAt(0);
            lastAttack = next;
            return next;
        }

        /// <summary>转阶段清袋，二阶段池重排</summary>
        public void ClearAttackBag() {
            attackBag.Clear();
        }
        #endregion
    }
}
