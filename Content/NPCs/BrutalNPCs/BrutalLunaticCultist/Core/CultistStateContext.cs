using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>三元素轮转</summary>
    internal enum CultistElement : int
    {
        Fire = 0,
        Ice = 1,
        Thunder = 2,
    }

    /// <summary>元素调色板与通用参数</summary>
    internal static class CultistPalette
    {
        //火：焚焰绯金
        internal static readonly Color FireDeep = new(150, 30, 20);
        internal static readonly Color FireMain = new(255, 110, 40);
        internal static readonly Color FireBright = new(255, 205, 110);
        //冰：霜蓝月白
        internal static readonly Color IceDeep = new(30, 60, 140);
        internal static readonly Color IceMain = new(90, 180, 255);
        internal static readonly Color IceBright = new(210, 245, 255);
        //雷：紫电白金
        internal static readonly Color ThunderDeep = new(80, 30, 150);
        internal static readonly Color ThunderMain = new(170, 110, 255);
        internal static readonly Color ThunderBright = new(235, 215, 255);

        internal static Color Deep(CultistElement e) => e switch {
            CultistElement.Fire => FireDeep,
            CultistElement.Ice => IceDeep,
            _ => ThunderDeep,
        };

        internal static Color Main(CultistElement e) => e switch {
            CultistElement.Fire => FireMain,
            CultistElement.Ice => IceMain,
            _ => ThunderMain,
        };

        internal static Color Bright(CultistElement e) => e switch {
            CultistElement.Fire => FireBright,
            CultistElement.Ice => IceBright,
            _ => ThunderBright,
        };

        /// <summary>分身用去饱和主色（可学习破绽之一）</summary>
        internal static Color CloneMain(CultistElement e) {
            Color c = Main(e);
            float gray = (c.R + c.G + c.B) / 3f;
            return Color.Lerp(c, new Color(gray / 255f, gray / 255f, gray / 255f), 0.35f);
        }
    }

    /// <summary>施法姿态，映射原版帧行</summary>
    internal static class CultistPose
    {
        internal const int Float = 0;      //悬浮 4-6
        internal const int CastForward = 1;//前施法 10-12
        internal const int CastUp = 2;     //上施法 7-9
        internal const int Scream = 3;     //嘶吼 13-15
        internal const int Stand = 4;      //静立 帧0
    }

    /// <summary>拜月教徒状态上下文</summary>
    internal class CultistStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public List<NPC> Clones { get; } = [];
        #endregion

        #region 战斗进度
        public bool IsDeathMode { get; set; }
        /// <summary>低于50%生命</summary>
        public bool IsPhase2 { get; set; }
        /// <summary>转阶段演出已完成</summary>
        public bool PhaseTransitionDone { get; set; }
        /// <summary>低血大招已释放</summary>
        public bool CataclysmUsed { get; set; }
        /// <summary>死亡演出完，CheckDead 放行真死</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>出招环索引</summary>
        public int AttackCycleIndex { get; set; }
        /// <summary>当前元素，服务端在 Weave 轮转并写 Ai[0]</summary>
        public CultistElement Element { get; set; }
        /// <summary>脱战狂暴（拉远/环境原因）：免伤+增伤，AI 不变；镜像 Ai[6]</summary>
        public bool Enraged { get; set; }
        #endregion

        #region 博弈数据（服务端权威）
        /// <summary>真身受击累计（MirrorBlink/GrandRitual 内），服务端</summary>
        public int TrueBodyHurtAccum { get; set; }
        /// <summary>上帧生命快照，服务端算受击增量</summary>
        public int LifeSnapshot { get; set; }
        /// <summary>破绽硬直剩余帧，>0 时防御归零</summary>
        public int StaggerTimer { get; set; }
        /// <summary>大仪式进度 0-1，镜像 Ai[2]</summary>
        public float RitualProgress { get; set; }
        /// <summary>大仪式圆心，镜像 Ai[4]/Ai[5] 供分身与各端读取</summary>
        public Vector2 RitualCenter { get; set; }
        /// <summary>错误献祭计数（分身被击中），GrandRitual 状态消费，服务端</summary>
        public int RitualPunishRequests { get; set; }
        #endregion

        #region 每帧声明的表现数据
        /// <summary>施法姿态，见 CultistPose</summary>
        public int CastPose { get; set; }
        /// <summary>施法辉光 0-1</summary>
        public float CastGlow { get; set; }
        /// <summary>元素光环强度 0-1</summary>
        public float ElementAura { get; set; }
        /// <summary>悬浮锚点</summary>
        public Vector2 HoverAnchor { get; set; }
        /// <summary>跳过默认悬浮（状态直控速度）</summary>
        public bool SkipDefaultHover { get; set; }

        /// <summary>舞台法阵（入场/撤离/死亡演出），每帧声明</summary>
        public float StageSigilProgress { get; set; }
        public Vector2 StageSigilPos { get; set; }
        public float StageSigilRadius { get; set; } = 200f;
        public float StageSigilFlash { get; set; }
        public float StageSigilBreak { get; set; }
        public float StageSigilSpin { get; set; }
        #endregion

        #region 动画数据
        public int FrameCounter { get; set; }
        #endregion

        /// <summary>刷新分身列表（type 440 且 ai[3] 指向本体）</summary>
        public void RefreshClones() {
            Clones.Clear();
            if (Npc == null) {
                return;
            }
            foreach (var n in Terraria.Main.ActiveNPCs) {
                if (n.type == NPCID.CultistBossClone && (int)n.ai[3] == Npc.whoAmI) {
                    Clones.Add(n);
                }
            }
        }

        /// <summary>本阶段分身编制数</summary>
        public int DesiredCloneCount => IsPhase2 ? 3 : 2;

        /// <summary>元素前进一步（火→冰→雷）</summary>
        public void AdvanceElement() {
            Element = (CultistElement)(((int)Element + 1) % 3);
        }
    }
}
