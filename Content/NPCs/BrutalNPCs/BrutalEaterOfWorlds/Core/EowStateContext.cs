using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core
{
    /// <summary>世界吞噬者状态上下文</summary>
    internal class EowStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        /// <summary>体节按链序缓存(含尾)，ordinal 即下标</summary>
        public List<NPC> Segments { get; set; } = [];
        #endregion

        #region 运动参数（状态声明，主控 UpdateMovement 消费）
        public Vector2 TargetPosition { get; set; }
        public float MoveSpeed { get; set; }
        public float TurnSpeed { get; set; }
        /// <summary>跳过常规运动(状态直控速度)</summary>
        public bool SkipDefaultMovement { get; set; }
        /// <summary>蛇形摆动 0~1，每帧声明</summary>
        public float SlitherStrength { get; set; }
        /// <summary>蛇形相位累计</summary>
        public float SlitherPhase { get; set; }
        /// <summary>速度趋近率</summary>
        public float AccelRate { get; set; } = 0.07f;
        #endregion

        #region 战斗状态
        /// <summary>二阶段(蜕皮后)</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>蜕皮已完成</summary>
        public bool MoltDone { get; set; }
        /// <summary>大招环已启动</summary>
        public bool ApexCycleStarted { get; set; }
        public bool IsDeathMode { get; set; }
        /// <summary>出招环索引</summary>
        public int AttackPhaseIndex { get; set; }
        /// <summary>死亡演出完成，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>脱离腐化区累计帧，过阈撤离</summary>
        public int OutOfZoneTimer { get; set; }
        #endregion

        #region 分裂协同（状态每帧声明，主控 UpdateSplitSteering 消费）
        /// <summary>分裂组数(0=未分裂)，镜像自 override ai 同步槽</summary>
        public int SplitGroups { get; set; }
        /// <summary>分裂形变进度0~1(撕开/合拢的表现插值)，随同步槽走</summary>
        public float SplitProgress { get; set; }
        /// <summary>体节总数(身+尾)，镜像自 override ai 同步槽；分裂布局唯一口径</summary>
        public int TotalSegments { get; set; }
        /// <summary>组目标点，[0]=头自身组</summary>
        public Vector2[] GroupTargets { get; } = new Vector2[EowSplitLayout.MaxGroups];
        /// <summary>组速度</summary>
        public float[] GroupSpeeds { get; } = new float[EowSplitLayout.MaxGroups];
        /// <summary>组转向</summary>
        public float[] GroupTurns { get; } = new float[EowSplitLayout.MaxGroups];
        /// <summary>组直控速度(非零时覆盖寻的转向)</summary>
        public Vector2?[] GroupDirectVelocity { get; } = new Vector2?[EowSplitLayout.MaxGroups];
        /// <summary>合体回链期：分组首节追前邻，近距自动收编</summary>
        public bool MergeHoming { get; set; }
        #endregion

        #region 演出通道
        /// <summary>体节间距系数(0.55~1.2)，蓄势压缩用；写入同步槽</summary>
        public float Compression { get; set; } = 1f;
        /// <summary>体节脉冲种类 0无 1蓄势波 2蜕皮波 3死亡波 4分裂点闪</summary>
        public int PulseKind { get; set; }
        /// <summary>脉冲波相位 0头→1尾</summary>
        public float PulsePhase { get; set; }
        /// <summary>头部酸光强度(0~1，客户端表现)</summary>
        public float MawGlow { get; set; }
        /// <summary>全局绿雾滤镜浓度声明(0~1)</summary>
        public float MiasmaLevel { get; set; }
        #endregion

        public void ResetSplitDeclaration() {
            for (int i = 0; i < EowSplitLayout.MaxGroups; i++) {
                GroupTargets[i] = Vector2.Zero;
                GroupSpeeds[i] = 0f;
                GroupTurns[i] = 1f;
                GroupDirectVelocity[i] = null;
            }
            MergeHoming = false;
        }

        /// <summary>按链序刷新体节列表(身+尾)</summary>
        public void RefreshSegments() {
            Segments.Clear();
            //先收集再按 ordinal(npc.ai[0]) 排序，保证链序
            foreach (var n in Main.ActiveNPCs) {
                if ((n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail)
                    && (int)n.ai[3] == Npc.whoAmI) {
                    Segments.Add(n);
                }
            }
            Segments.Sort((a, b) => ((int)a.ai[0]).CompareTo((int)b.ai[0]));
        }
    }
}
