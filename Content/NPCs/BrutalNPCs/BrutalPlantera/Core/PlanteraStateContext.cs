using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core
{
    /// <summary>状态上下文</summary>
    internal class PlanteraStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        /// <summary>钩爪列表，按 ai[3] 序号排序</summary>
        public List<NPC> Hooks { get; } = [];
        /// <summary>触手列表</summary>
        public List<NPC> Tentacles { get; } = [];
        #endregion

        #region 悬吊运动参数（状态声明，主控消费）
        /// <summary>悬吊点相对钩爪质心的偏移目标</summary>
        public Vector2 SuspendOffset { get; set; }
        public float MoveSpeed { get; set; } = 6f;
        public float AccelRate { get; set; } = 0.05f;
        /// <summary>跳过悬吊运动(状态直控速度)</summary>
        public bool SkipDefaultMovement { get; set; }
        /// <summary>本帧朝向 0锁玩家 1锁速度方向 2不动</summary>
        public int RotationMode { get; set; }
        /// <summary>悬吊摆动相位</summary>
        public float SwayPhase { get; set; }
        #endregion

        #region 战斗状态
        /// <summary>二阶段(蜕壳后)，由主控在转阶段演出中翻转</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>激怒：目标不在丛林或在地表</summary>
        public bool IsEnraged { get; set; }
        public bool IsDeathMode { get; set; }
        /// <summary>低血 25% 以下</summary>
        public bool IsLowLife { get; set; }
        /// <summary>凋零绽放已放过</summary>
        public bool NovaUsed { get; set; }
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>出招洗牌袋</summary>
        public List<PlanteraStateIndex> AttackBag { get; } = [];
        /// <summary>上一个攻击状态，防重复</summary>
        public PlanteraStateIndex LastAttack { get; set; } = PlanteraStateIndex.Canopy;
        /// <summary>投技冷却计时(权威端递减)，>0 时选择器跳过投技</summary>
        public int VineFeastCooldown { get; set; }
        #endregion

        #region 蓄力/演出视觉数据（每帧由状态声明）
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力类型 0无 1猛扑 2加特林 3新星 4格栅</summary>
        public int ChargeType { get; set; }
        /// <summary>本体荧光强度 0~1，每帧声明</summary>
        public float GlowPulse { get; set; }
        /// <summary>激怒红辉 0~1，主控平滑驱动，绘制层消费</summary>
        public float RageGlow { get; set; }
        /// <summary>身体额外缩放脉冲(呼吸/蓄力压缩)</summary>
        public float BodyScalePulse { get; set; }
        /// <summary>枯萎进度 0~1，死亡演出抽干颜色</summary>
        public float DeathWilt { get; set; }
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

        /// <summary>刷新钩爪/触手列表</summary>
        public void RefreshParts() {
            Hooks.Clear();
            Tentacles.Clear();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.PlanterasHook) {
                    Hooks.Add(n);
                }
                else if (n.type == NPCID.PlanterasTentacle) {
                    Tentacles.Add(n);
                }
            }
            //按序号稳定排序，保证各端一致
            Hooks.Sort((a, b) => ((int)a.ai[3]).CompareTo((int)b.ai[3]));
        }

        /// <summary>钩爪质心，无钩爪回退本体</summary>
        public Vector2 HookCentroid() {
            if (Hooks.Count == 0) {
                return Npc.Center;
            }
            Vector2 sum = Vector2.Zero;
            foreach (var hook in Hooks) {
                sum += hook.Center;
            }
            return sum / Hooks.Count;
        }
    }
}
