using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Core
{
    /// <summary>臂驱动模式，状态每帧写指令、主体统一积分</summary>
    internal enum ArmMode
    {
        /// <summary>默认悬挂：弹簧回队形位 + 呼吸摆动</summary>
        Hang,
        /// <summary>弹簧持位到指定点（k/damp 由指令给）</summary>
        Hold,
        /// <summary>纯弹道飞行，链长收口勒停</summary>
        Ballistic,
        /// <summary>重力坠落，落到指令 Target.Y 即嵌入停住</summary>
        Fall,
        /// <summary>直接钉在目标点（无弹簧，进场待命/瞬移用）</summary>
        Snap,
    }

    /// <summary>单臂一帧的驱动指令</summary>
    internal struct ArmDirective
    {
        public ArmMode Mode;
        /// <summary>Hold/Snap 的目标点；Fall 时 Y 为地面线</summary>
        public Vector2 Target;
        /// <summary>弹簧系数</summary>
        public float Spring;
        /// <summary>阻尼</summary>
        public float Damping;
        /// <summary>期望旋转（贴图约定 rotation=0 工具口朝下）</summary>
        public float WantRot;
        /// <summary>旋转趋近速率</summary>
        public float RotRate;
        /// <summary>true 用 WantRot，false 走默认摆动</summary>
        public bool UseRot;

        public static ArmDirective HangDefault => new() {
            Mode = ArmMode.Hang,
            Spring = 0.11f,
            Damping = 0.86f,
            RotRate = 0.14f,
        };

        public static ArmDirective HoldAt(Vector2 target, float spring, float damping) => new() {
            Mode = ArmMode.Hold,
            Target = target,
            Spring = spring,
            Damping = damping,
            RotRate = 0.14f,
        };
    }

    /// <summary>废钢统帅状态上下文</summary>
    internal class ScrapStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public ScrapCommander Owner { get; set; }
        #endregion

        #region 战场事实（控制器每帧刷新）
        public bool MasterMode { get; set; }
        #endregion

        #region 出招编排
        /// <summary>轮换出招序号</summary>
        public int AttackIndex { get; set; }
        /// <summary>出招冷却（裁决只看权威端的值）</summary>
        public int AttackCooldown { get; set; }
        /// <summary>阶段：1=P1 工头 2=P2 统帅 3=过载熔断；写 ai[2] 同步</summary>
        public int Phase { get => (int)Npc.ai[2]; set => Npc.ai[2] = value; }
        /// <summary>死亡演出已完，CheckDead 据此放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        #endregion

        #region 臂指令（本地表现，每帧由 BeginFrameDefaults 重置为 Hang）
        public ArmDirective[] Arms { get; } = new ArmDirective[ScrapCommander.ArmCount];
        #endregion

        #region 表现通道（本地量，绘制层读取）
        /// <summary>工具可见度，进场/死亡演出逐件控制</summary>
        public float[] ToolAlpha { get; } = new float[ScrapCommander.ArmCount];
        /// <summary>头可见度</summary>
        public float HeadAlpha { get; set; } = 1f;
        /// <summary>磁力线亮度 0..1，进场拼装与磁暴用</summary>
        public float MagnetGlow { get; set; }
        /// <summary>磁场流向：+1 收束 / -1 外掷（磁场表现体读取）</summary>
        public float MagnetPull { get; set; } = 1f;
        /// <summary>残影强度 0..1，头锤/突刺举旗，每帧自然衰减</summary>
        public float AfterimageStrength { get; set; }
        /// <summary>焊缝热光 0..1，过载阶段拉满</summary>
        public float WeldHeat { get; set; }
        /// <summary>目镜扫光进度 0..1，&lt;0 表示熄灭</summary>
        public float EyeScan { get; set; } = -1f;
        /// <summary>进场组装锚点（各端本地记录，工具吊装持位用）</summary>
        public Vector2 IntroAnchor { get; set; }
        /// <summary>进场四件工具的坠落点（X=落点，Y=地面线）</summary>
        public Vector2[] IntroCrashSpot { get; } = new Vector2[ScrapCommander.ArmCount];
        /// <summary>一条射线预警/指挥线（每帧由状态重新登记）</summary>
        public struct BeamMark
        {
            public Vector2 From;
            public Vector2 Dir;
            public float Length;
            public float Alpha;
            /// <summary>1=滚动虚线预警 0=实线</summary>
            public float Dash;
            /// <summary>0..1 亮度热度</summary>
            public float Hot;
        }

        /// <summary>本帧要画的全部射线标记（突刺预警/矩阵网格/瀑布柱/指挥线共用一条通道）</summary>
        public System.Collections.Generic.List<BeamMark> Beams { get; } = new(12);

        /// <summary>登记一条预警虚线</summary>
        public void AddTelegraph(Vector2 from, Vector2 dir, float length, float alpha, float hot = 0.6f) {
            Beams.Add(new BeamMark {
                From = from,
                Dir = dir,
                Length = length,
                Alpha = alpha,
                Dash = 1f,
                Hot = hot,
            });
        }

        /// <summary>登记一条实线（指挥红线/探照灯等）</summary>
        public void AddSolidBeam(Vector2 from, Vector2 dir, float length, float alpha, float hot = 0.7f) {
            Beams.Add(new BeamMark {
                From = from,
                Dir = dir,
                Length = length,
                Alpha = alpha,
                Dash = 0f,
                Hot = hot,
            });
        }

        /// <summary>连击队列：当前攻击收招后直接接的状态号（-1 无；只有权威端的值驱动转场）</summary>
        public int QueuedChainState { get; set; } = -1;
        /// <summary>甩壳裸奔窗：受击加深 ×1.25（转阶段状态每帧举旗）</summary>
        public bool BareWindow { get; set; }
        #endregion

        /// <summary>每帧默认值：臂回悬挂、可见度回满、扫光熄灭；发光通道自然衰减</summary>
        public void BeginFrameDefaults() {
            for (int i = 0; i < Arms.Length; i++) {
                Arms[i] = ArmDirective.HangDefault;
                ToolAlpha[i] = 1f;
            }
            HeadAlpha = 1f;
            EyeScan = -1f;
            Beams.Clear();
            BareWindow = false;
            MagnetGlow *= 0.92f;
            if (MagnetGlow < 0.02f) {
                MagnetGlow = 0f;
            }
            AfterimageStrength *= 0.86f;
            if (AfterimageStrength < 0.03f) {
                AfterimageStrength = 0f;
            }
            //过载阶段焊缝常燃，不衰减
            if (Phase >= 3) {
                WeldHeat = MathHelper.Clamp(WeldHeat + 0.04f, 0f, 1f);
            }
            else {
                WeldHeat *= 0.95f;
                if (WeldHeat < 0.02f) {
                    WeldHeat = 0f;
                }
            }
        }
    }
}
