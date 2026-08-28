using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core
{
    /// <summary>状态机共享上下文，每只女王一份</summary>
    internal class QueenBeeStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        /// <summary>蜂群编队控制器，与女王同生命周期</summary>
        public SwarmDirector Swarm { get; set; }
        /// <summary>override.ai 引用(状态写出招环游标等，随 netUpdate 同步)</summary>
        public float[] OverrideAi { get; set; }
        #endregion

        #region 战斗状态
        public bool IsAsuraMode { get; set; }
        /// <summary>二阶段已进入(60%)</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>低血大招已放</summary>
        public bool UltimateDone { get; set; }
        /// <summary>环境激怒 0~2(地表+1 离丛林+1)，蜂巢墙内豁免</summary>
        public float EnrageScale { get; set; }
        /// <summary>出招环游标，镜像 override.ai[4]</summary>
        public int AttackCycleIndex { get; set; }
        /// <summary>死亡演出完成，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>投技就绪(服务端每帧算：满标记+冷却完+目标有效)，Reposition 末帧消费</summary>
        public bool GrabReady { get; set; }
        /// <summary>被蜂群标记的玩家 whoAmI(镜像 override.ai[5]-1)，-1 无</summary>
        public int MarkedPlayerWhoAmI { get; set; } = -1;
        #endregion

        #region 蓄力/演出数据(渲染消费，每帧声明)
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无 1俯冲 2迫击炮 3蜕变 4蜂潮</summary>
        public int ChargeType { get; set; }
        /// <summary>本帧用冲刺帧组(0~3)，未声明用悬停帧组(4~11)</summary>
        public bool UseChargePose { get; set; }
        /// <summary>激怒金边辉光 0~1，二阶段常驻</summary>
        public float RageGlow { get; set; }
        #endregion

        #region 冲刺视觉
        /// <summary>残影增强 0~1，状态推高，控制器衰减</summary>
        public float AfterimageBoost { get; set; }

        public void PushAfterimage(float boost) {
            if (boost > AfterimageBoost) {
                AfterimageBoost = boost;
            }
        }

        public void DecayDashVisuals() {
            AfterimageBoost *= 0.92f;
            if (AfterimageBoost < 0.01f) {
                AfterimageBoost = 0f;
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
