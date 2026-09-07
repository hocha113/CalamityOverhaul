using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>
    /// 姿态风格，映射原版 DrawNPCDirect_HallowBoss 对 npc.ai[0] 的语义，
    /// 由主控写入姿态通道后原版绘制自动给出对应手臂帧与身体特效
    /// </summary>
    internal enum EmpressPose : int
    {
        /// <summary>登场，双臂缓抬</summary>
        Spawn = 0,
        /// <summary>常态悬浮</summary>
        Idle = 1,
        /// <summary>左手施法</summary>
        CastLeft = 2,
        /// <summary>双手齐举施法</summary>
        CastBoth = 4,
        /// <summary>右手施法</summary>
        CastRight = 5,
        /// <summary>日舞长引</summary>
        Dance = 6,
        /// <summary>冲刺（原版绘制附带彩虹环绕残影）</summary>
        DashLeft = 8,
        /// <summary>冲刺（反向）</summary>
        DashRight = 9,
        /// <summary>变身光环（原版绘制附带白闪与8向幻影）</summary>
        Transform = 10,
    }

    /// <summary>灼痕分量档，决定命中后灼痕秒数（昼）</summary>
    internal enum EmpressScorchTier : byte
    {
        /// <summary>8 秒：普通接触、余韵类</summary>
        Light = 0,
        /// <summary>16 秒：长枪、瞬现枪、光球</summary>
        Medium = 1,
        /// <summary>22 秒：熔光扇</summary>
        Heavy = 2,
        /// <summary>25 秒：冲刺接触、投技</summary>
        Grab = 3,
        /// <summary>30 秒：追踪光束（绝对禁区）</summary>
        Beam = 4,
    }

    /// <summary>状态机共享上下文</summary>
    internal class EmpressStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public Player Target { get; set; }
        #endregion

        #region 阶段与形态
        /// <summary>二阶段，主控从 npc.ai[3] 位读出，全端一致</summary>
        public bool IsSecondPhase { get; set; }
        /// <summary>三阶段（终章），主控从 npc.ai[3] 位读出</summary>
        public bool IsThirdPhase { get; set; }
        /// <summary>昼形态，各端由全局昼夜标志本地判定</summary>
        public bool DayEmpowered { get; set; }
        /// <summary>昼形态视觉过渡 0~1，各端本地缓动</summary>
        public float DayFormBlend { get; set; }
        /// <summary>修罗模式/BossRush 增压</summary>
        public bool IsAsuraMode { get; set; }
        /// <summary>死亡演出结束，CheckDead 放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>攻击循环计数，服务端权威</summary>
        public int AttackCounter { get; set; }
        /// <summary>光绫缚舞冷却tick，服务端权威递减</summary>
        public int GrabCooldown { get; set; }
        /// <summary>终章缩圈完成，血量地板解除（NPCOverride.ai 同步）</summary>
        public bool FinaleKillable { get; set; }
        #endregion

        #region 姿态通道（写入 npc.ai[0]/ai[1] 供原版绘制消费）
        public EmpressPose Pose { get; set; } = EmpressPose.Idle;
        public float PoseTimer { get; set; }
        #endregion

        #region 蓄力特效数据
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力手 0无 1左手 2右手 3双手</summary>
        public int ChargeHand { get; set; }
        #endregion

        #region 竞技场（昼）
        /// <summary>本帧期望半径，0=关闭；主控写入 NPCOverride.ai[0] 同步</summary>
        public float ArenaRadiusRequest { get; set; }
        /// <summary>竞技场圆心跟随速度上限</summary>
        public float ArenaFollowSpeed { get; set; } = 12f;
        #endregion

        #region 手部锚点
        public Vector2 LeftHand => Npc.Center + new Vector2(-55f, -30f);
        public Vector2 RightHand => Npc.Center + new Vector2(55f, -30f);
        /// <summary>施法手（原版右手偏移，光球/冲击波出生点）</summary>
        public Vector2 CastHand => Npc.Center + new Vector2(60f, -45f);
        #endregion

        #region 节奏与伤害
        /// <summary>节奏因子：昼形态与修罗模式压缩计时（乘在阶段时长上）</summary>
        public float TempoScale {
            get {
                float scale = 1f;
                if (DayEmpowered) {
                    scale *= 0.85f;
                }
                if (IsAsuraMode) {
                    scale *= 0.9f;
                }
                return scale;
            }
        }

        /// <summary>按节奏缩放帧数，下限8帧防止过窄预警</summary>
        public int Scaled(int frames) => System.Math.Max(8, (int)(frames * TempoScale));

        /// <summary>光球伤害</summary>
        public int BoltDamage => ScaleDamage(IsSecondPhase ? 52 : 46, IsSecondPhase ? 36 : 30);
        /// <summary>长枪伤害（墙/智能枪）</summary>
        public int LanceDamage => ScaleDamage(IsSecondPhase ? 60 : 52, IsSecondPhase ? 38 : 32);
        /// <summary>瞬现枪伤害</summary>
        public int HitscanDamage => ScaleDamage(IsSecondPhase ? 60 : 52, IsSecondPhase ? 38 : 32);
        /// <summary>追踪光束伤害（绝对禁区，最高档）</summary>
        public int BeamDamage => ScaleDamage(IsSecondPhase ? 78 : 70, IsSecondPhase ? 50 : 44);
        /// <summary>熔光扇伤害</summary>
        public int FanDamage => ScaleDamage(IsSecondPhase ? 70 : 62, IsSecondPhase ? 46 : 40);

        /// <summary>昼形态 ×1.25，不再 9999（灼痕系统接管威慑）</summary>
        private int ScaleDamage(int normal, int expert) {
            int value = Npc.GetAttackDamage_ForProjectiles(normal, expert);
            if (DayEmpowered) {
                value = (int)(value * 1.25f);
            }
            return value;
        }
        #endregion

        public void ResetChargeState() {
            IsCharging = false;
            ChargeProgress = 0f;
            ChargeHand = 0;
        }

        public void SetChargeState(int hand, float progress) {
            IsCharging = true;
            ChargeHand = hand;
            ChargeProgress = MathHelper.Clamp(progress, 0f, 1f);
        }
    }
}
