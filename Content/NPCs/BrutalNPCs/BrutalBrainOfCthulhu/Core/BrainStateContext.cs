using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>状态上下文</summary>
    internal class BrainStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public List<NPC> Creepers { get; set; } = [];
        /// <summary>主控重制实例（借用其 override.ai 同步槽：0/1=编队锚点）</summary>
        public BrainOfCthulhuAI Master { get; set; }
        #endregion

        #region 战斗状态
        /// <summary>二阶段裸脑（镜像 npc.ai[0] 的符号约定）</summary>
        public bool IsPhase2 { get; set; }
        public bool IsAsuraMode { get; set; }
        /// <summary>低血狂化（≤28%），解锁心搏骤停</summary>
        public bool IsLowLife { get; set; }
        public float LifeRatio { get; set; } = 1f;
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>心搏骤停后的力竭窗口（帧），期间受伤+30%</summary>
        public int FalterTimer { get; set; }
        /// <summary>出猩红狂暴强度 0~1，权威端推导，随 override.ai 同步槽下发</summary>
        public float EnrageRamp { get; set; }
        /// <summary>攻击袋（服务端专用）</summary>
        public List<BrainStateIndex> AttackBag { get; } = [];
        /// <summary>上一招，防复读</summary>
        public BrainStateIndex LastAttack { get; set; } = BrainStateIndex.Hover;
        /// <summary>距上次心搏骤停的帧数</summary>
        public int HeartAttackCooldown { get; set; }
        /// <summary>摄心镜狱冷却（帧），主控每帧递减</summary>
        public int MindSeizeCooldown { get; set; }
        #endregion

        #region 心跳
        /// <summary>本拍周期（帧），由状态每帧声明</summary>
        public int BeatPeriod { get; set; } = 54;
        /// <summary>全局心跳强度 0~1，影响屏效与音量，每帧声明</summary>
        public float BeatIntensity { get; set; } = 0.5f;
        /// <summary>上次响拍时的时钟戳（ai[3] 整数值，本地表现去重，严格前进才响）</summary>
        public long LastPlayedBeat { get; set; } = -1;
        /// <summary>心跳静止（骤停/死亡演出的死寂段）</summary>
        public bool BeatSilenced { get; set; }
        #endregion

        #region 演出与绘制数据（每帧由状态声明，主控归零）
        /// <summary>蓄力预警发光 0~1</summary>
        public float TelegraphGlow { get; set; }
        /// <summary>眼芒（真身出手前兆）0~1</summary>
        public float EyeGlint { get; set; }
        /// <summary>护壳裂纹 0~1（转换/死亡演出）</summary>
        public float ShellCrack { get; set; }
        /// <summary>实体化程度 0~1，瞬移后从0爬升，绘制虚影用</summary>
        public float GhostFade { get; set; } = 1f;
        /// <summary>强制帧命令 0自动 1强制露心（一阶段露心搏动）</summary>
        public int FrameCommand { get; set; }
        /// <summary>露心窗口：防御归零（BloodPulse 高风险高回报）</summary>
        public bool HeartExposed { get; set; }
        /// <summary>镜阵/骤停中不可被仆从索敌（防召唤物点破真身）</summary>
        public bool HideFromMinions { get; set; }
        /// <summary>骤停黑幕目标 0~1，状态每帧声明</summary>
        public float BlackoutTarget { get; set; }
        /// <summary>本帧无敌（每帧声明，主控统一落到 npc.dontTakeDamage，防中途加入的客户端残留原版默认值）</summary>
        public bool Invulnerable { get; set; }
        #endregion

        public void ResetTelegraph() {
            TelegraphGlow = 0f;
            EyeGlint = 0f;
            FrameCommand = 0;
            HeartExposed = false;
            HideFromMinions = false;
            BlackoutTarget = 0f;
            Invulnerable = false;
        }

        /// <summary>刷新飞眼列表</summary>
        public void RefreshCreepers() {
            Creepers.Clear();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.Creeper) {
                    Creepers.Add(n);
                }
            }
        }
    }
}
