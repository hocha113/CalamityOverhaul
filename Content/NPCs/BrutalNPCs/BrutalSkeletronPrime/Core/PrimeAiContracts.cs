using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>机械骷髅王宏观阶段标记，写入npc.ai[0]（原版自动同步），供外部系统读取：<see cref="PrimeFacts.IsDeathPerformance"/>等</summary>
    internal static class PrimePhase
    {
        /// <summary>刚生成，尚未初始化</summary>
        public const int Uninit = 0;
        /// <summary>登场演出</summary>
        public const int Intro = 1;
        /// <summary>武装阶段：四肢健在，头部担任指挥</summary>
        public const int Armed = 2;
        /// <summary>狂暴阶段：四肢殉爆，头部亲自下场</summary>
        public const int Rage = 3;
        /// <summary>死亡演出</summary>
        public const int DeathShow = 4;
    }

    /// <summary>机械骷髅王 ai[] 槽位契约</summary>
    internal static class PrimeAiSlots
    {
        /// <summary>头 ai[0] 宏观阶段 <see cref="PrimePhase"/></summary>
        public const int HeadPhase = 0;
        /// <summary>头 ai[1] 战术指令 <see cref="PrimeCommandKind"/></summary>
        public const int HeadCommandSlot = 1;
        /// <summary>头 ai[2] 状态机槽 <see cref="PrimeStateIndex"/></summary>
        public const int HeadStateSlot = 2;
        /// <summary>头 ai[3] 原版 Mechdusa 标记，禁占</summary>
        public const int HeadMechQueenFlag = 3;

        /// <summary>头 Override ai[9] 编队旋转时钟，四臂共用</summary>
        public const int OverrideOrbitClock = 9;
        /// <summary>头 Override ai[10] 狂暴闪现冲撞锁定方向 X</summary>
        public const int OverrideRageDashDirX = 10;
        /// <summary>头 Override ai[11] 狂暴闪现冲撞锁定方向 Y</summary>
        public const int OverrideRageDashDirY = 11;

        /// <summary>臂 ai[0] 侧 -1/1</summary>
        public const int ArmSide = 0;
        /// <summary>臂 ai[1] 头部 whoAmI</summary>
        public const int ArmHeadIndex = 1;
        /// <summary>臂 ai[2] 状态机槽 <see cref="PrimeArmStateIndex"/></summary>
        public const int ArmStateSlot = 2;
        /// <summary>臂 ai[3] 蓄力计时，两端确定性自增</summary>
        public const int ArmChargeTimer = 3;

        /// <summary>机械臂生成后的攻击宽限帧数</summary>
        public const int ArmSpawnGraceFrames = 180;
    }

    /// <summary>四肢存活状态快照</summary>
    internal readonly struct PrimeLimbStatus
    {
        public readonly bool CannonAlive;
        public readonly bool ViceAlive;
        public readonly bool SawAlive;
        public readonly bool LaserAlive;

        public PrimeLimbStatus(bool cannonAlive, bool viceAlive, bool sawAlive, bool laserAlive) {
            CannonAlive = cannonAlive;
            ViceAlive = viceAlive;
            SawAlive = sawAlive;
            LaserAlive = laserAlive;
        }

        public bool NoArm => !CannonAlive && !ViceAlive && !SawAlive && !LaserAlive;
    }

    /// <summary>跨类共享的机械骷髅王事实查询</summary>
    internal static class PrimeFacts
    {
        public static PrimeLimbStatus GetLimbStatus() {
            return new PrimeLimbStatus(
                IsNpcActive(CWRWorld.primeCannon),
                IsNpcActive(CWRWorld.primeVice),
                IsNpcActive(CWRWorld.primeSaw),
                IsNpcActive(CWRWorld.primeLaser)
            );
        }

        /// <summary>收尾蓄力已可见，预警须兑现；PrimeArm 延后编队，头部推迟冲撞类招式</summary>
        public static bool IsCommittedArmState(int armStateIndex) {
            return armStateIndex == (int)PrimeArmStateIndex.CannonMortar
                || armStateIndex == (int)PrimeArmStateIndex.LaserSweep
                || armStateIndex == (int)PrimeArmStateIndex.LaserChargedShot;
        }

        /// <summary>是否仍有存活机械臂处于收尾蓄力攻击中（基于同步槽判定，两端一致）</summary>
        public static bool AnyArmCommitted() {
            int[] arms = [CWRWorld.primeCannon, CWRWorld.primeVice, CWRWorld.primeSaw, CWRWorld.primeLaser];
            foreach (int index in arms) {
                if (index < 0 || index >= Main.maxNPCs) {
                    continue;
                }
                NPC arm = Main.npc[index];
                if (arm.active && IsCommittedArmState((int)arm.ai[PrimeAiSlots.ArmStateSlot])) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsDeathPerformance(NPC head) {
            return head != null && head.active && head.ai[PrimeAiSlots.HeadPhase] == PrimePhase.DeathShow;
        }

        private static bool IsNpcActive(int whoAmI) {
            return whoAmI >= 0 && whoAmI < Main.maxNPCs && Main.npc[whoAmI].active;
        }
    }
}
