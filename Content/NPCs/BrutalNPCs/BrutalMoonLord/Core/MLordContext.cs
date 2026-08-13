using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>核心状态上下文，控制器每帧刷新</summary>
    internal class MLordContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public MoonLordCoreAI Owner { get; set; }
        #endregion

        #region 战场事实（每帧刷新）
        public bool DeathMode { get; set; }
        public bool BossRush { get; set; }
        public bool MasterMode { get; set; }
        /// <summary>部件存活快照</summary>
        public MLordPartsStatus Parts { get; set; }
        /// <summary>核心裸露阶段（部件全破）</summary>
        public bool CoreExposed { get; set; }
        #endregion

        #region 出招编排
        /// <summary>三相拱卫出招序列游标</summary>
        public int TrinityCursor { get; set; }
        /// <summary>核心裸露出招序列游标</summary>
        public int ExposedCursor { get; set; }
        /// <summary>死亡演出已完，CheckDead 据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>待处理的部件破坏事件数（服务端排队）</summary>
        public int PendingBreakEvents { get; set; }
        /// <summary>排队破坏事件的归因码（与 <see cref="PendingBreakEvents"/> 同进同出，服务端消费）</summary>
        public List<int> PendingBreakCodes { get; } = [];
        #endregion

        #region 演出驱动（客户端表现读取）
        /// <summary>蓄力进度 0~1，滤镜/光环消费</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>日蚀强度请求 0~1，天幕消费，每帧由状态重申</summary>
        public float EclipseDrive { get; set; }
        /// <summary>心脏裸露程度 0~1，核心胸腔帧动画消费</summary>
        public float HeartExposure { get; set; }
        /// <summary>核心倾斜目标角</summary>
        public float LeanAngle { get; set; }
        /// <summary>演出期锁全体部件火力/动作</summary>
        public bool HoldAllParts { get; set; }
        /// <summary>大招收招硬直受击加伤窗口</summary>
        public bool StaggerVulnerable { get; set; }
        #endregion

        #region 死亡演出数据（运镜与玩家侧读取）
        public int DeathTimer { get; set; }
        public MLordDeathPhase DeathPhase { get; set; }
        #endregion

        /// <summary>编队时钟引用</summary>
        public ref float FormationClock => ref Owner.ai[MLordAiSlots.OvFormationClock];

        /// <summary>三相拱卫固定出招表</summary>
        internal static readonly MLordStateIndex[] TrinityCycle = [
            MLordStateIndex.Concerto,
            MLordStateIndex.TidalPalms,
            MLordStateIndex.DeathrayScan,
            MLordStateIndex.Concerto,
            MLordStateIndex.Starfall,
            MLordStateIndex.CrescentClose,
            MLordStateIndex.Concerto,
            MLordStateIndex.MoonBite,
            MLordStateIndex.GravityCollapse,
        ];

        /// <summary>核心裸露强化出招表（同状态吃 CoreExposed 旗标）</summary>
        internal static readonly MLordStateIndex[] ExposedCycle = [
            MLordStateIndex.Concerto,
            MLordStateIndex.CrescentClose,
            MLordStateIndex.Starfall,
            MLordStateIndex.TidalPalms,
            MLordStateIndex.DeathrayScan,
            MLordStateIndex.GravityCollapse,
            MLordStateIndex.Concerto,
            MLordStateIndex.MoonBite,
        ];

        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
        }

        public void SetChargeState(float progress) {
            IsCharging = true;
            ChargeProgress = MathHelper.Clamp(progress, 0f, 1f);
        }

        /// <summary>按当前阶段推进出招游标并返回下一状态索引</summary>
        public MLordStateIndex NextAttackIndex() {
            if (CoreExposed) {
                MLordStateIndex next = ExposedCycle[ExposedCursor % ExposedCycle.Length];
                ExposedCursor++;
                return next;
            }
            MLordStateIndex trinityNext = TrinityCycle[TrinityCursor % TrinityCycle.Length];
            TrinityCursor++;
            return trinityNext;
        }
    }

    /// <summary>死亡演出阶段</summary>
    internal enum MLordDeathPhase
    {
        /// <summary>假死坍缩</summary>
        Collapse,
        /// <summary>引力内爆</summary>
        Implosion,
        /// <summary>死寂</summary>
        Silence,
        /// <summary>超新星</summary>
        Supernova,
        /// <summary>余烬</summary>
        Embers,
        /// <summary>结束</summary>
        Done
    }
}
