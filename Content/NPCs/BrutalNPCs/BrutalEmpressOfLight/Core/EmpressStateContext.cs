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
        /// <summary>白天处刑形态（伤害9999+节奏加速），各端由全局昼夜标志本地判定</summary>
        public bool DayEmpowered { get; set; }
        /// <summary>昼形态视觉过渡 0~1，各端本地缓动</summary>
        public float DayFormBlend { get; set; }
        /// <summary>死亡模式/BossRush 增压</summary>
        public bool IsDeathMode { get; set; }
        /// <summary>低血大招已经放过一次</summary>
        public bool OverdriveUsed { get; set; }
        /// <summary>死亡演出结束，CheckDead 放行</summary>
        public bool DeathPerformanceFinished { get; set; }
        /// <summary>攻击循环计数，服务端权威</summary>
        public int AttackCounter { get; set; }
        /// <summary>光绫缚舞冷却tick，服务端权威递减，客户端不参与判定</summary>
        public int GrabCooldown { get; set; }
        #endregion

        #region 姿态通道（写入 npc.ai[0]/ai[1] 供原版绘制消费）
        /// <summary>本帧姿态，状态每帧声明，未声明回落 Idle</summary>
        public EmpressPose Pose { get; set; } = EmpressPose.Idle;
        /// <summary>姿态计时，映射原版 ai[1] 的臂帧窗口</summary>
        public float PoseTimer { get; set; }
        #endregion

        #region 蓄力特效数据
        /// <summary>蓄力进度 0~1</summary>
        public float ChargeProgress { get; set; }
        public bool IsCharging { get; set; }
        /// <summary>蓄力手 0无 1左手 2右手 3双手</summary>
        public int ChargeHand { get; set; }
        #endregion

        #region 手部锚点
        /// <summary>左手世界坐标（面向由绘制处理，锚点固定偏移与原版一致）</summary>
        public Vector2 LeftHand => Npc.Center + new Vector2(-55f, -30f);
        /// <summary>右手世界坐标</summary>
        public Vector2 RightHand => Npc.Center + new Vector2(55f, -30f);
        #endregion

        #region 节奏与伤害
        /// <summary>节奏因子：昼形态与死亡模式压缩计时（乘在阶段时长上）</summary>
        public float TempoScale {
            get {
                float scale = 1f;
                if (DayEmpowered) {
                    scale *= 0.8f;
                }
                if (IsDeathMode) {
                    scale *= 0.88f;
                }
                return scale;
            }
        }

        /// <summary>按节奏缩放帧数，下限8帧防止过窄预警</summary>
        public int Scaled(int frames) => System.Math.Max(8, (int)(frames * TempoScale));

        /// <summary>棱彩弹伤害</summary>
        public int BoltDamage => ScaleDamage(IsSecondPhase ? 50 : 45, IsSecondPhase ? 35 : 30);
        /// <summary>以太枪骑伤害</summary>
        public int LanceDamage => ScaleDamage(IsSecondPhase ? 60 : 50, IsSecondPhase ? 35 : 30);
        /// <summary>光剑伤害</summary>
        public int BladeDamage => ScaleDamage(IsSecondPhase ? 58 : 50, IsSecondPhase ? 38 : 32);
        /// <summary>日舞光束伤害</summary>
        public int SunrayDamage => ScaleDamage(IsSecondPhase ? 60 : 50, IsSecondPhase ? 40 : 35);
        /// <summary>虹瓣伤害</summary>
        public int PetalDamage => ScaleDamage(50, 35);
        /// <summary>极光帘幕伤害</summary>
        public int AuroraDamage => ScaleDamage(60, 40);

        private int ScaleDamage(int normal, int expert) {
            if (DayEmpowered) {
                return 9999;//白天处刑：与原版一致的即死威慑
            }
            return Npc.GetAttackDamage_ForProjectiles(normal, expert);
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
