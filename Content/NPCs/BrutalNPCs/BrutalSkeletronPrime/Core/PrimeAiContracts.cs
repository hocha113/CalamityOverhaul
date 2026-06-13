using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>
    /// 机械骷髅王宏观阶段标记，写入 <c>npc.ai[0]</c>（原版自动同步），
    /// 供外部系统读取：<see cref="PrimeFacts.IsDeathPerformance"/> 等
    /// </summary>
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
    /// <para>头部 SkeletronPrime <c>npc.ai[]</c>：</para>
    /// <list type="bullet">
    /// <item><c>ai[0]</c>：宏观阶段 <see cref="PrimePhase"/></item>
    /// <item><c>ai[1]</c>：战术指令 <see cref="PrimeCommandKind"/>（见 HeadCommandSlot）</item>
    /// <item><c>ai[2]</c>：状态机槽 <see cref="PrimeStateIndex"/></item>
    /// <item><c>ai[3]</c>：原版 Mechdusa 标记，禁止占用</item>
    /// </list>
    /// <para>头部 NPCOverride <c>ai[]</c>：</para>
    /// <list type="bullet">
    /// <item><c>ai[9]</c>：编队旋转时钟，机械臂环绕共用</item>
    /// </list>
    /// <para>机械臂 <c>npc.ai[]</c>：</para>
    /// <list type="bullet">
    /// <item><c>ai[0]</c>：臂侧 -1/1</item>
    /// <item><c>ai[1]</c>：头部 whoAmI</item>
    /// <item><c>ai[2]</c>：状态机槽 <see cref="PrimeArmStateIndex"/></item>
    /// <item><c>ai[3]</c>：蓄力计时，两端确定性自增</item>
    /// </list>
    internal static class PrimeAiSlots
    {
        public const int HeadPhase = 0;
        /// <summary>向四臂广播的战术指令（<see cref="PrimeCommandKind"/>）</summary>
        public const int HeadCommandSlot = 1;
        public const int HeadStateSlot = 2;
        public const int HeadMechQueenFlag = 3;

        public const int OverrideOrbitClock = 9;

        public const int ArmSide = 0;
        public const int ArmHeadIndex = 1;
        public const int ArmStateSlot = 2;
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

        /// <summary>收尾蓄力已可见，预警须兑现；<see cref="PrimeArm"/> 延后编队，头部推迟冲撞类招式</summary>
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
