using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>宏观阶段，写核心 npc.ai[0]；2 被原版 checkDead 用作真死哨兵，禁作常规阶段</summary>
    internal static class MLordPhase
    {
        /// <summary>刚生成，尚未初始化</summary>
        public const int Uninit = 0;
        /// <summary>日蚀降临登场演出</summary>
        public const int Intro = 1;
        /// <summary>原版 checkDead 的真死放行哨兵，仅死亡演出落幕帧写入</summary>
        public const int VanillaDeathSentinel = 2;
        /// <summary>三相拱卫，部件存活</summary>
        public const int Trinity = 3;
        /// <summary>核心裸露，部件全破</summary>
        public const int CoreExposed = 4;
        /// <summary>死亡演出</summary>
        public const int DeathShow = 5;
        /// <summary>脱战离场</summary>
        public const int Leaving = 6;
    }

    /// <summary>月总各部件 npc.ai[] 槽位契约（原版 ai 全 4 槽自动同步）</summary>
    internal static class MLordAiSlots
    {
        //―――― 核心 (MoonLordCore) ――――
        /// <summary>核心 ai[0] 宏观阶段 <see cref="MLordPhase"/></summary>
        public const int CorePhase = 0;
        /// <summary>核心 ai[1] 事件数据（部件破坏事件排队计数）</summary>
        public const int CoreEventData = 1;
        /// <summary>核心 ai[2] 状态机槽 <see cref="MLordStateIndex"/></summary>
        public const int CoreStateSlot = 2;

        //―――― 手 Override ai（12 槽，随 npc.netUpdate 同步）：爬行步态通道 ――――
        /// <summary>手 Override ai[0] 爬行相位 <see cref="MLordCrawlPhase"/>（服务端裁定转移）</summary>
        public const int HandOvCrawlPhase = 0;
        /// <summary>手 Override ai[1] 抓取锚点 X（世界坐标，服务端骰点写入）</summary>
        public const int HandOvAnchorX = 1;
        /// <summary>手 Override ai[2] 抓取锚点 Y</summary>
        public const int HandOvAnchorY = 2;
        /// <summary>手 Override ai[3] 相位内计时（各端镜像自增，同步矫偏）</summary>
        public const int HandOvPhaseTick = 3;

        //―――― 手 (MoonLordHand) ――――
        /// <summary>
        /// 手/头 ai[0] 破坏标记：-2=已破。原版 checkDead 对 396/397 的特判
        /// 先于 tML CheckDead 钩子执行（NPC.cs 71306 早于 71390 的钩子位），
        /// 会自行写入 -2 并生成真眼，本值即采纳原版约定
        /// </summary>
        public const int PartBroken = 0;
        /// <summary>破坏标记值（原版写入）</summary>
        public const float BrokenMark = -2f;
        /// <summary>手 ai[1] 行位 0上对/1下对（原版 checkDead 不触碰此槽，生成时写入）</summary>
        public const int HandRow = 1;
        /// <summary>手 ai[2] 边位 0左/1右（沿用原版槽位语义）</summary>
        public const int HandSide = 2;
        /// <summary>手/头/真眼 ai[3] 核心 whoAmI（沿用原版槽位语义）</summary>
        public const int PartCoreIndex = 3;

        //―――― 真眼 (MoonLordFreeEye) ――――
        /// <summary>真眼 ai[0] 预留（原版 checkDead 生成路径不写此槽；席位改用 whoAmI 扫描序）</summary>
        public const int EyeSquadSlot = 0;
        /// <summary>真眼 ai[1] 出生计时（各端镜像自增，同步矫偏）</summary>
        public const int EyeBirthTimer = 1;

        //―――― 核心 Override ai（12 槽，随 npc.netUpdate 同步）――――
        /// <summary>Override ai[0] 编队时钟，全部件/真眼共用相位源</summary>
        public const int OvFormationClock = 0;
        /// <summary>Override ai[1] 攻击锚点 X（服务端骰点写入）</summary>
        public const int OvAnchorX = 1;
        /// <summary>Override ai[2] 攻击锚点 Y</summary>
        public const int OvAnchorY = 2;
        /// <summary>Override ai[3] 攻击种子（服务端骰点，客户端演出复现）</summary>
        public const int OvAttackSeed = 3;
        /// <summary>Override ai[4] 真眼指挥指令 <see cref="MLordEyeCommand"/></summary>
        public const int OvEyeCommand = 4;
        /// <summary>Override ai[5] 最近破坏的部件 1上左/2上右/3头/4下左/5下右</summary>
        public const int OvLastBrokenPart = 5;
        /// <summary>Override ai[6] 大招已放标记位掩码 <see cref="MLordUltFlags"/>
        /// （Override ai 只有 12 槽且已排满，各大招的"已放"标记合存此槽）</summary>
        public const int OvUltUsed = 6;
        /// <summary>Override ai[7] 投技被抓玩家 whoAmI+1，0=无（服务端写，被抓端读）</summary>
        public const int OvGrabTarget = 7;
        /// <summary>Override ai[8] 投技抓握之手 whoAmI+1，0=无</summary>
        public const int OvGrabHand = 8;
        /// <summary>Override ai[9] 黑闪已放标记位掩码 <see cref="MLordBlackFlashFlags"/>
        /// （开幕/残血两拍分位记账；残血拍失手不消耗：失手退场时清回对应位允许重试）</summary>
        public const int OvBlackFlashUsed = 9;
        /// <summary>Override ai[10] 黑闪节拍：0正常 / 1蓄力被打断（服务端写，各端演出分支）</summary>
        public const int OvBlackFlashBeat = 10;
        /// <summary>Override ai[11] 黑闪重试门线（生命比例）：失手时写入"当前血线-一档"，
        /// 0=未设；核心转移检测取它与解锁线的较小者</summary>
        public const int OvBlackFlashRearm = 11;
    }

    /// <summary>
    /// 大招已放标记位，同存 <see cref="MLordAiSlots.OvUltUsed"/> 一槽。
    /// 各标记只负责"这场还欠不欠这一次"，不影响出招表里的常规席位
    /// </summary>
    internal static class MLordUltFlags
    {
        /// <summary>虚空撕裂已放</summary>
        public const int VoidRupture = 1;
        /// <summary>月明湮灭已放（保底强制线据此判断是否还欠一次压轴巨束）</summary>
        public const int Annihilation = 2;

        public static bool Has(float slotValue, int flag) => ((int)slotValue & flag) != 0;
        public static float With(float slotValue, int flag) => (int)slotValue | flag;
    }

    /// <summary>
    /// 黑闪已放标记位，同存 <see cref="MLordAiSlots.OvBlackFlashUsed"/> 一槽。
    /// 一场两拍：全眼破碎进二阶段的开幕宣言拍 + 残血底牌拍
    /// </summary>
    internal static class MLordBlackFlashFlags
    {
        /// <summary>开幕拍已放（核心裸露后的第一个常规拍强制释放，失手即算放过不重试）</summary>
        public const int Opener = 1;
        /// <summary>残血底牌拍已放（失手清位重试，门线走 OvBlackFlashRearm）</summary>
        public const int Desperate = 2;

        public static bool Has(float slotValue, int flag) => ((int)slotValue & flag) != 0;
        public static float With(float slotValue, int flag) => (int)slotValue | flag;
        public static float Without(float slotValue, int flag) => (int)slotValue & ~flag;
    }

    /// <summary>真眼编队指令</summary>
    internal static class MLordEyeCommand
    {
        /// <summary>自主循环：环绕编队+轮流出手</summary>
        public const int Solo = 0;
        /// <summary>锚定阵位：为核心攻击站桩（弦月合拢第三弧等）</summary>
        public const int Anchor = 1;
        /// <summary>退避收拢：演出/转换期贴核心待机</summary>
        public const int Retreat = 2;
    }

    /// <summary>爬行相位（写手 Override ai[0]）</summary>
    internal static class MLordCrawlPhase
    {
        /// <summary>空闲：走编队/巢位</summary>
        public const int Free = 0;
        /// <summary>探爪：冲向抓取锚点</summary>
        public const int Reach = 1;
        /// <summary>抓牢：钉死在锚点，向本体供力</summary>
        public const int Planted = 2;
        /// <summary>松爪：短暂收势后回到空闲</summary>
        public const int Recover = 3;
    }

    /// <summary>本体移动策略（状态每帧向上下文重申）</summary>
    internal enum MLordMovePolicy
    {
        /// <summary>状态自管（演出/投技），爬行系统整体停摆</summary>
        Off = 0,
        /// <summary>爬行赶路：手轮流抓点，把本体拽向 MoveGoal</summary>
        Travel = 1,
        /// <summary>编队拖曳：手阵携行本体缓移（月蚀噬咬合围等手全被征用的状态）</summary>
        Tow = 2,
        /// <summary>抓桩定身：四爪张成 X 形钉死，本体锁位（射击仪式/大招发射架）</summary>
        Brace = 3,
    }

    /// <summary>部件存活快照：四手 + 头。手槽序 = 行*2+边（0上左/1上右/2下左/3下右）</summary>
    internal readonly struct MLordPartsStatus
    {
        /// <summary>手槽总数</summary>
        public const int HandSlots = 4;

        private readonly int hand0, hand1, hand2, hand3;
        /// <summary>存活位掩码：bit0~3 手槽，bit4 头</summary>
        private readonly int aliveMask;
        public readonly int Head;

        public MLordPartsStatus(ReadOnlySpan<int> hands, int aliveMask, int head) {
            hand0 = hands[0];
            hand1 = hands[1];
            hand2 = hands[2];
            hand3 = hands[3];
            this.aliveMask = aliveMask;
            Head = head;
        }

        /// <summary>槽位手 whoAmI，缺位 -1</summary>
        public int HandIndex(int slot) => slot switch { 0 => hand0, 1 => hand1, 2 => hand2, _ => hand3 };
        /// <summary>槽位手在场且未破坏</summary>
        public bool HandAlive(int slot) => (aliveMask & (1 << slot)) != 0;
        public bool HeadAlive => (aliveMask & (1 << HandSlots)) != 0;

        /// <summary>存活（未破坏）部件数（四手+头）</summary>
        public int AliveCount {
            get {
                int count = HeadAlive ? 1 : 0;
                for (int slot = 0; slot < HandSlots; slot++) {
                    if (HandAlive(slot)) {
                        count++;
                    }
                }
                return count;
            }
        }
        /// <summary>已破坏部件数（部件实体仍在场，仅眼位破坏）</summary>
        public int BrokenCount {
            get {
                int present = Head >= 0 ? 1 : 0;
                for (int slot = 0; slot < HandSlots; slot++) {
                    if (HandIndex(slot) >= 0) {
                        present++;
                    }
                }
                return present - AliveCount;
            }
        }
        public bool AllBroken => AliveCount == 0;
        public bool AnyHandAlive => (aliveMask & 0b1111) != 0;
        /// <summary>存活手数</summary>
        public int AliveHandCount {
            get {
                int count = 0;
                for (int slot = 0; slot < HandSlots; slot++) {
                    if (HandAlive(slot)) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>自偏好槽起顺时针找首个存活手 whoAmI，无则 -1</summary>
        public int FirstAliveHand(int preferSlot = 0) {
            for (int i = 0; i < HandSlots; i++) {
                int slot = (preferSlot + i) % HandSlots;
                if (HandAlive(slot) && HandIndex(slot) >= 0) {
                    return HandIndex(slot);
                }
            }
            return -1;
        }
    }

    /// <summary>跨类共享的月总事实查询，扫描 Main.npc 无静态缓存</summary>
    internal static class MLordFacts
    {
        /// <summary>真眼集群规模上限（四手+头各脱出一只）</summary>
        public const int MaxFreeEyes = 5;

        /// <summary>扫出隶属该核心的四手/头部件状态（手槽 = 行*2+边）</summary>
        public static MLordPartsStatus ScanParts(NPC core) {
            Span<int> hands = stackalloc int[MLordPartsStatus.HandSlots];
            hands.Fill(-1);
            int aliveMask = 0;
            int head = -1;

            if (core == null || !core.active) {
                return new MLordPartsStatus(hands, 0, -1);
            }

            foreach (NPC npc in Main.ActiveNPCs) {
                if ((int)npc.ai[MLordAiSlots.PartCoreIndex] != core.whoAmI) {
                    continue;
                }
                if (npc.type == NPCID.MoonLordHand) {
                    int row = Math.Clamp((int)npc.ai[MLordAiSlots.HandRow], 0, 1);
                    int side = Math.Clamp((int)npc.ai[MLordAiSlots.HandSide], 0, 1);
                    int slot = row * 2 + side;
                    hands[slot] = npc.whoAmI;
                    if (npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark) {
                        aliveMask |= 1 << slot;
                    }
                }
                else if (npc.type == NPCID.MoonLordHead) {
                    head = npc.whoAmI;
                    if (npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark) {
                        aliveMask |= 1 << MLordPartsStatus.HandSlots;
                    }
                }
            }
            return new MLordPartsStatus(hands, aliveMask, head);
        }

        /// <summary>场上隶属该核心的真眼列表写入 buffer，返回数量（钳到缓冲长度，防越界读默认槽）</summary>
        public static int ScanFreeEyes(NPC core, Span<int> buffer) {
            int count = 0;
            if (core == null || !core.active) {
                return 0;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.MoonLordFreeEye
                    || (int)npc.ai[MLordAiSlots.PartCoreIndex] != core.whoAmI) {
                    continue;
                }
                if (count >= buffer.Length) {
                    break;
                }
                buffer[count] = npc.whoAmI;
                count++;
            }
            return count;
        }

        /// <summary>
        /// 取第 ordinal 只真眼（whoAmI 扫描序，对在场数循环取模）。
        /// 心脏与残口不客串小弹幕炮口——无活头时的小型弹幕代射一律从这里领真眼，
        /// 场上无真眼返回 null（该拍静默）
        /// </summary>
        public static NPC GetFreeEye(NPC core, int ordinal) {
            Span<int> eyes = stackalloc int[MaxFreeEyes];
            int count = ScanFreeEyes(core, eyes);
            if (count <= 0) {
                return null;
            }
            return Main.npc[eyes[ordinal % count]];
        }

        /// <summary>由部件反查核心，无效返回 null</summary>
        public static NPC GetCore(NPC part) {
            int index = (int)part.ai[MLordAiSlots.PartCoreIndex];
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC core = Main.npc[index];
            return core.active && core.type == NPCID.MoonLordCore ? core : null;
        }

        /// <summary>核心当前同步状态索引</summary>
        public static MLordStateIndex GetCoreState(NPC core) {
            return (MLordStateIndex)(int)core.ai[MLordAiSlots.CoreStateSlot];
        }

        /// <summary>读取核心 Override ai 槽，核心失效返回退避值</summary>
        public static float ReadCoreOverrideAi(NPC core, int slot, float fallback = 0f) {
            //补上type守卫：存储索引过期后槽位可能被任意NPC复用，此时也算核心失效
            if (core == null || !core.active || core.type != NPCID.MoonLordCore) {
                return fallback;
            }
            //取不到覆写一并退避（精确索引缺键会抛出）
            if (!core.TryGetOverride(out MoonLordCoreAI overrideAI)) {
                return fallback;
            }
            return overrideAI.ai[slot];
        }

        /// <summary>取手部覆写实例（爬行通道读写），失效返回 null</summary>
        public static MoonLordHandAI GetHandOverride(NPC hand) {
            if (hand == null || !hand.active || hand.type != NPCID.MoonLordHand) {
                return null;
            }
            return hand.TryGetOverride(out MoonLordHandAI overrideAI) ? overrideAI : null;
        }
    }
}
