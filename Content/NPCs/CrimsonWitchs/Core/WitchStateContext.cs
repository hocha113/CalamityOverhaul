using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.CrimsonWitchs.Core
{
    /// <summary>红莲魔女状态上下文，状态机共享数据；实现 <see cref="INpcStateContext"/> 零配置接入 ai 槽同步</summary>
    internal class WitchStateContext : INpcStateContext
    {
        #region 核心引用
        /// <summary>NPC实例引用</summary>
        public NPC Npc { get; set; }
        /// <summary>目标玩家引用</summary>
        public Player Target { get; set; }
        #endregion

        #region 阶段标记
        /// <summary>是否为死亡模式/Boss Rush（数值与节奏加压用）</summary>
        public bool IsDeathMode { get; set; }
        /// <summary>当前阶段：1=庭园教学 2=红莲盛开 3=炼狱庭园 4=终幕</summary>
        public int Phase { get; set; } = 1;
        /// <summary>是否正在执行变阶演出（期间响指节拍器暂停、不触发濒死演出）</summary>
        public bool IsInPhaseTransition { get; set; }
        /// <summary>死亡演出完毕；死亡状态置 true 后 CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 响指节拍器（M2 实装，此处先立槽位）
        /// <summary>距下次响指的剩余帧数；由 <see cref="CrimsonWitch"/> 每帧递减并经 npc.ai[2] 同步</summary>
        public int SnapCountdown { get; set; } = WitchBattleConst.SnapInterval;
        /// <summary>响指节拍器是否运行中（登场/变阶/终幕/死亡演出期间挂起）</summary>
        public bool SnapMetronomeActive { get; set; }
        #endregion

        #region 演出可视量（渲染层读取，状态推高、控制器衰减）
        /// <summary>施法蓄力进度(0~1)，驱动汇聚粒子与抬手姿态</summary>
        public float CastCharge { get; set; }
        /// <summary>突进视觉强度(0~1)，驱动残影与拖尾</summary>
        public float DashVisual { get; set; }

        /// <summary>每帧衰减演出可视量（控制器调用）</summary>
        public void DecayVisuals() {
            DashVisual *= 0.9f;
            if (DashVisual < 0.01f) {
                DashVisual = 0f;
            }
        }

        /// <summary>推高突进视觉强度</summary>
        public void PushDashVisual(float value) {
            if (value > DashVisual) {
                DashVisual = value;
            }
        }
        #endregion

        #region 工具
        /// <summary>目标是否有效存活</summary>
        public bool TargetValid => Target != null && Target.active && !Target.dead;

        /// <summary>与目标的距离（目标失效时返回极大值）</summary>
        public float DistanceToTarget => TargetValid ? Npc.Center.Distance(Target.Center) : float.MaxValue;

        /// <summary>按当前血量比例推算应处阶段（1~4）</summary>
        public int LifePhase {
            get {
                float factor = Npc.life / (float)Npc.lifeMax;
                if (factor <= WitchBattleConst.FinaleLifeFactor) {
                    return 4;
                }
                if (factor <= WitchBattleConst.Phase3LifeFactor) {
                    return 3;
                }
                if (factor <= WitchBattleConst.Phase2LifeFactor) {
                    return 2;
                }
                return 1;
            }
        }
        #endregion
    }
}
