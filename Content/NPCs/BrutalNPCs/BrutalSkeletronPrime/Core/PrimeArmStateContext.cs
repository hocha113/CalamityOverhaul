using InnoVault.StateMachines;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>机械臂状态上下文，每臂一份</summary>
    internal class PrimeArmStateContext : INpcStateContext
    {
        #region 核心引用
        public NPC Npc { get; set; }
        public NPC Head { get; set; }
        public Player Target { get; set; }
        public PrimeArm Owner { get; set; }
        #endregion

        #region 战场事实（由控制器每帧刷新）
        public bool BossRush { get; set; }
        public bool MasterMode { get; set; }
        public bool Death { get; set; }
        public bool ViceAlive { get; set; }
        public bool CannonAlive { get; set; }
        public bool SawAlive { get; set; }
        public bool LaserAlive { get; set; }
        /// <summary>生成宽限期内禁止攻击</summary>
        public bool DontAttack { get; set; }
        /// <summary>臂侧 (-1 / 1)，取自 npc.ai[0]</summary>
        public int Side => (int)Npc.ai[PrimeAiSlots.ArmSide];
        #endregion

        #region 运行时驱动数据（状态写入，绘制读取）
        /// <summary>当前瞄准方向（平滑插值）</summary>
        public Vector2 AimDirection { get; set; } = Vector2.UnitX;
        /// <summary>弹簧物理速度（跨状态延续）</summary>
        public Vector2 SpringVelocity { get; set; }
        /// <summary>后坐力强度（发射时抬升、随帧衰减）</summary>
        public float RecoilIntensity { get; set; }
        /// <summary>电锯旋转速度（视觉/音效共用）</summary>
        public float SpinSpeed { get; set; }
        /// <summary>电锯目标旋转速度</summary>
        public float TargetSpinSpeed { get; set; }
        /// <summary>钳爪冲击反馈强度</summary>
        public float ImpactIntensity { get; set; }
        /// <summary>钳口是否张开（驱动钳爪帧动画：蓄力/突刺时张开，命中/待机时闭合）</summary>
        public bool ClawOpen { get; set; }
        /// <summary>激光蓄力进度 0~1（发光层渐变用）</summary>
        public float ChargeGlow { get; set; }
        /// <summary>出招轮换计数器（确定性轮换代替随机）</summary>
        public int AttackCycle { get; set; }
        #endregion

        /// <summary>同伴缺失数 0~3，狂暴化乘算用</summary>
        public int MissingPartnerCount {
            get {
                int missing = 0;
                int myType = Npc.type;
                if (!CannonAlive && myType != Terraria.ID.NPCID.PrimeCannon) {
                    missing++;
                }
                if (!LaserAlive && myType != Terraria.ID.NPCID.PrimeLaser) {
                    missing++;
                }
                if (!SawAlive && myType != Terraria.ID.NPCID.PrimeSaw) {
                    missing++;
                }
                if (!ViceAlive && myType != Terraria.ID.NPCID.PrimeVice) {
                    missing++;
                }
                return missing;
            }
        }

        /// <summary>同步蓄力计时槽（npc.ai[3]，两端确定性自增）</summary>
        public ref float ChargeTimer => ref Npc.ai[PrimeAiSlots.ArmChargeTimer];

        /// <summary>发射后坐力：反推机体并抬升视觉反馈强度</summary>
        public void ApplyRecoil(float intensity) {
            RecoilIntensity = intensity;
            Npc.velocity -= AimDirection * intensity * 0.5f;
        }
    }
}
