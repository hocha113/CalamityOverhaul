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
        /// <summary>本次协奏走短变体（循环内非首位协奏席；服务端选招时写入，协奏 OnEnter 消费）</summary>
        public bool ConcertoShortVariant { get; set; }
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

        #region 移动申报（状态每帧写，爬行系统消费——本体无自走）
        /// <summary>本体想去的位置（世界坐标）</summary>
        public Vector2 MoveGoal { get; set; }
        /// <summary>移动紧迫度 0~1（步频与拽力）</summary>
        public float MoveUrgency { get; set; }
        /// <summary>移动策略，核心 AI 帧首重置为 Off，状态不申报即自管</summary>
        public MLordMovePolicy MovePolicy { get; set; }
        #endregion

        #region 死亡演出数据（运镜与玩家侧读取）
        public int DeathTimer { get; set; }
        public MLordDeathPhase DeathPhase { get; set; }
        #endregion

        /// <summary>编队时钟引用</summary>
        public ref float FormationClock => ref Owner.ai[MLordAiSlots.OvFormationClock];

        /// <summary>三相拱卫固定出招表：最强演出（噬咬投技）居中段，两个长光束态拆开，坍缩收尾</summary>
        internal static readonly MLordStateIndex[] TrinityCycle = [
            MLordStateIndex.Concerto,
            MLordStateIndex.TidalPalms,
            MLordStateIndex.Starfall,
            MLordStateIndex.Concerto,
            MLordStateIndex.MoonBite,
            MLordStateIndex.DeathrayScan,
            MLordStateIndex.Concerto,
            MLordStateIndex.CrescentClose,
            MLordStateIndex.GravityCollapse,
        ];

        /// <summary>核心裸露强化出招表（同状态吃 CoreExposed 旗标）：噬咬前移进终局前半</summary>
        internal static readonly MLordStateIndex[] ExposedCycle = [
            MLordStateIndex.Concerto,
            MLordStateIndex.MoonBite,
            MLordStateIndex.CrescentClose,
            MLordStateIndex.Starfall,
            MLordStateIndex.Concerto,
            MLordStateIndex.TidalPalms,
            MLordStateIndex.DeathrayScan,
            MLordStateIndex.GravityCollapse,
        ];

        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
        }

        public void SetChargeState(float progress) {
            IsCharging = true;
            ChargeProgress = MathHelper.Clamp(progress, 0f, 1f);
        }

        /// <summary>
        /// 按当前阶段推进出招游标并返回下一状态索引。
        /// 协奏席规则：循环首位协奏为完整连接拍，其余席走短变体；
        /// 升压跳拍：三相期部件破坏≥2 后非首位协奏直接让位（后期压力上行，
        /// PartBreak 事件演出本身已提供喘息）
        /// </summary>
        public MLordStateIndex NextAttackIndex() {
            if (CoreExposed) {
                int cursor = ExposedCursor;
                MLordStateIndex next = AdvanceCycle(ExposedCycle, ref cursor, allowSkip: false);
                ExposedCursor = cursor;
                //残血席位升级：死光扫描让位给月明湮灭（每轮循环一次的压轴巨束）
                if (next == MLordStateIndex.DeathrayScan
                    && Npc.life < Npc.lifeMax * MLordDirector.AnnihilationLifeRatio) {
                    return MLordStateIndex.LunarAnnihilation;
                }
                return next;
            }
            int trinityCursor = TrinityCursor;
            MLordStateIndex trinityNext = AdvanceCycle(TrinityCycle, ref trinityCursor,
                allowSkip: Parts.BrokenCount >= 2);
            TrinityCursor = trinityCursor;
            return trinityNext;
        }

        private MLordStateIndex AdvanceCycle(MLordStateIndex[] cycle, ref int cursor, bool allowSkip) {
            for (int guard = 0; guard <= cycle.Length; guard++) {
                int pos = cursor % cycle.Length;
                MLordStateIndex next = cycle[pos];
                cursor++;
                if (next == MLordStateIndex.Concerto) {
                    bool firstSlot = pos == 0;
                    if (!firstSlot && allowSkip) {
                        continue;
                    }
                    ConcertoShortVariant = !firstSlot;
                }
                return next;
            }
            return MLordStateIndex.Concerto;    //表内不可能全为可跳协奏，兜底不可达
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
