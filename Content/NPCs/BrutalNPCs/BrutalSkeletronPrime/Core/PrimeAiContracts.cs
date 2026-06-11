using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core
{
    /// <summary>
    /// 机械骷髅王宏观阶段标记，写入 <c>npc.ai[0]</c>（原版自动同步），供外部系统读取：
    /// <see cref="TwinsAccompanyHandler"/>、<see cref="PrimeFacts.IsDeathPerformance"/>、双子随从生成判定等
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

    /// <summary>
    /// 机械骷髅王 AI 槽位契约。
    /// <para>头部（SkeletronPrime 本体 <c>npc.ai[]</c>）：</para>
    /// <list type="bullet">
    /// <item><c>ai[0]</c> —— 宏观阶段（<see cref="PrimePhase"/>）</item>
    /// <item><c>ai[1]</c> —— 保留未用</item>
    /// <item><c>ai[2]</c> —— 状态机同步槽（<see cref="PrimeStateIndex"/>，由 NpcStateMachine 写入）</item>
    /// <item><c>ai[3]</c> —— 原版 Mechdusa（机械混合体）标记，禁止占用</item>
    /// </list>
    /// <para>头部（NPCOverride <c>ai[]</c>）：</para>
    /// <list type="bullet">
    /// <item><c>ai[9]</c> —— 编队旋转时钟（每帧自增，机械臂环绕编队共用）</item>
    /// <item><c>ai[10]</c> —— 传送恢复计时（由 <see cref="Projectiles.Boss.SkeletronPrime.SetPosingStarm"/> 杀死时写入 180）</item>
    /// </list>
    /// <para>机械臂（<c>npc.ai[]</c>）：</para>
    /// <list type="bullet">
    /// <item><c>ai[0]</c> —— 原版臂侧 (-1/1)</item>
    /// <item><c>ai[1]</c> —— 头部 whoAmI</item>
    /// <item><c>ai[2]</c> —— 状态机同步槽（<see cref="PrimeArmStateIndex"/>）</item>
    /// <item><c>ai[3]</c> —— 蓄力计时（两端按相同公式确定性自增）</item>
    /// </list>
    /// </summary>
    internal static class PrimeAiSlots
    {
        public const int HeadPhase = 0;
        public const int HeadStateSlot = 2;
        public const int HeadMechQueenFlag = 3;

        public const int OverrideOrbitClock = 9;
        public const int OverrideTeleportTimer = 10;

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

        public static bool IsDeathPerformance(NPC head) {
            return head != null && head.active && head.ai[PrimeAiSlots.HeadPhase] == PrimePhase.DeathShow;
        }

        private static bool IsNpcActive(int whoAmI) {
            return whoAmI >= 0 && whoAmI < Main.maxNPCs && Main.npc[whoAmI].active;
        }
    }
}
