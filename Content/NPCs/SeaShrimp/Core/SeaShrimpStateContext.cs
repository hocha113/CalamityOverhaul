using InnoVault.StateMachines;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Core
{
    /// <summary>螯臂驱动模式，状态每帧写指令、骨架统一求解</summary>
    internal enum ClawMode
    {
        /// <summary>默认收拢守位：折叠在头前，随呼吸微摆</summary>
        Guard,
        /// <summary>弹簧持位到世界点（预瞄/蓄力后拉用）</summary>
        Hold,
        /// <summary>一帧打点：目标弹簧被冲量弹出，poly 急停由弹簧自然完成</summary>
        Strike,
    }

    /// <summary>单侧螯臂一帧的驱动指令</summary>
    internal struct ClawDirective
    {
        public ClawMode Mode;
        /// <summary>Hold/Strike 的世界目标点</summary>
        public Vector2 Target;
        /// <summary>弹簧刚度（0 用默认）</summary>
        public float Spring;
        /// <summary>阻尼（0 用默认）</summary>
        public float Damping;
        /// <summary>螯体姿态角偏置（相对前臂方向）</summary>
        public float ClawPoseOffset;
        /// <summary>螯钳开合 0..1（0 合拢，1 张开）</summary>
        public float ClawOpen;

        public static ClawDirective GuardDefault => new() {
            Mode = ClawMode.Guard,
            Spring = SeaShrimpDirector.ArmSpring,
            Damping = SeaShrimpDirector.ArmDamping,
        };
    }

    /// <summary>渊晶海虾状态上下文：状态写通道，运动学/绘制层消费</summary>
    internal class SeaShrimpStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        public SeaShrimpBoss Owner { get; set; }
        #endregion

        #region 战场事实（控制器每帧刷新）
        public bool MasterMode { get; set; }
        #endregion

        #region 出招编排
        /// <summary>轮换出招序号</summary>
        public int AttackIndex { get; set; }
        /// <summary>出招冷却（裁决只看权威端的值）</summary>
        public int AttackCooldown { get; set; }
        /// <summary>阶段：1=P1 甲壳期 2=P2 涨压 3=P3 裸晶深渊；写 ai[2] 同步</summary>
        public int Phase { get => (int)Npc.ai[2]; set => Npc.ai[2] = value; }
        /// <summary>死亡演出已完，CheckDead 据此放行（死亡态末帧翻真）</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>连击队列：当前攻击收招后直接接的状态号（-1 无）</summary>
        public int QueuedChainState { get; set; } = -1;
        #endregion

        #region 姿态通道（每帧由 BeginFrameDefaults 复位，状态重新断言）
        /// <summary>脊柱卷曲 -1..1：负=后卷 C 形（尾弹蓄力），正=前拱；0 自然</summary>
        public float SpineCurl { get; set; }
        /// <summary>爬行 S 波增益 0..2（游泳划水可调大）</summary>
        public float WaveGain { get; set; } = 1f;
        /// <summary>尾扇张合目标 0..1</summary>
        public float TailFlare { get; set; } = 0.35f;
        /// <summary>双螯指令：0=近侧 1=远侧</summary>
        public ClawDirective[] Claws { get; } = new ClawDirective[2];
        #endregion

        #region 表现通道（本地量，绘制层读取）
        /// <summary>全身可见度（入场/蜕壳演出用）</summary>
        public float BodyAlpha { get; set; } = 1f;
        /// <summary>蜕壳进度 0..1（持久量，蜕壳态推进；绘制层据此提亮裸晶体色）</summary>
        public float Molted01 { get; set; }
        /// <summary>死亡黯淡 0..1（持久量，死亡演出推进；压暗体色与晶光）</summary>
        public float DeathGloom { get; set; }
        /// <summary>晶簇发光脉冲增益 0..1（蓄力时拉高）</summary>
        public float CrystalGlow { get; set; }
        /// <summary>残影强度 0..1，尾弹/冲撞举旗，每帧自然衰减</summary>
        public float AfterimageStrength { get; set; }
        /// <summary>螯击伤害窗（攻击态每帧举旗，判定弹幕消费：伤害窗=视觉窗）</summary>
        public bool ClawDamageWindow { get; set; }

        /// <summary>一条射线预警/指示线（每帧由状态重新登记）</summary>
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

        /// <summary>本帧要画的全部射线标记</summary>
        public List<BeamMark> Beams { get; } = new(8);

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

        /// <summary>登记一条实线</summary>
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
        #endregion

        /// <summary>每帧默认值：姿态回自然、螯回守位、光通道自然衰减</summary>
        public void BeginFrameDefaults() {
            SpineCurl = 0f;
            WaveGain = 1f;
            TailFlare = 0.35f;
            for (int i = 0; i < Claws.Length; i++) {
                ClawDirective d = ClawDirective.GuardDefault;
                //守位保留上一帧开合，避免钳口瞬跳
                d.ClawOpen = Claws[i].ClawOpen * 0.9f;
                Claws[i] = d;
            }
            Beams.Clear();
            ClawDamageWindow = false;
            CrystalGlow *= 0.9f;
            if (CrystalGlow < 0.02f) {
                CrystalGlow = 0f;
            }
            AfterimageStrength *= 0.87f;
            if (AfterimageStrength < 0.03f) {
                AfterimageStrength = 0f;
            }
        }
    }
}
