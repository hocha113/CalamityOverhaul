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

        //―――― 手 (MoonLordHand) ――――
        /// <summary>
        /// 手/头 ai[0] 破坏标记：-2=已破。原版 checkDead 对 396/397 的特判
        /// 先于 tML CheckDead 钩子执行（NPC.cs 71306 早于 71390 的钩子位），
        /// 会自行写入 -2 并生成真眼——本值即采纳原版约定
        /// </summary>
        public const int PartBroken = 0;
        /// <summary>破坏标记值（原版写入）</summary>
        public const float BrokenMark = -2f;
        /// <summary>手 ai[1] 攻击事件数据（掌击目标锁定等）</summary>
        public const int PartEventData = 1;
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
        /// <summary>Override ai[5] 最近破坏的部件 1左手/2右手/3头</summary>
        public const int OvLastBrokenPart = 5;
        /// <summary>Override ai[6] 大招已用标记 0/1</summary>
        public const int OvUltUsed = 6;
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

    /// <summary>部件存活快照</summary>
    internal readonly struct MLordPartsStatus
    {
        public readonly int LeftHand;
        public readonly int RightHand;
        public readonly int Head;
        public readonly bool LeftHandAlive;
        public readonly bool RightHandAlive;
        public readonly bool HeadAlive;

        public MLordPartsStatus(int leftHand, int rightHand, int head,
            bool leftHandAlive, bool rightHandAlive, bool headAlive) {
            LeftHand = leftHand;
            RightHand = rightHand;
            Head = head;
            LeftHandAlive = leftHandAlive;
            RightHandAlive = rightHandAlive;
            HeadAlive = headAlive;
        }

        /// <summary>存活（未破坏）部件数</summary>
        public int AliveCount => (LeftHandAlive ? 1 : 0) + (RightHandAlive ? 1 : 0) + (HeadAlive ? 1 : 0);
        /// <summary>已破坏部件数（部件实体仍在场，仅眼位破坏）</summary>
        public int BrokenCount {
            get {
                int present = (LeftHand >= 0 ? 1 : 0) + (RightHand >= 0 ? 1 : 0) + (Head >= 0 ? 1 : 0);
                return present - AliveCount;
            }
        }
        public bool AllBroken => AliveCount == 0;
        public bool AnyHandAlive => LeftHandAlive || RightHandAlive;
    }

    /// <summary>跨类共享的月总事实查询，扫描 Main.npc 无静态缓存</summary>
    internal static class MLordFacts
    {
        /// <summary>扫出隶属该核心的手/头部件状态</summary>
        public static MLordPartsStatus ScanParts(NPC core) {
            int leftHand = -1, rightHand = -1, head = -1;
            bool leftAlive = false, rightAlive = false, headAlive = false;

            if (core == null || !core.active) {
                return new MLordPartsStatus(-1, -1, -1, false, false, false);
            }

            foreach (NPC npc in Main.ActiveNPCs) {
                if ((int)npc.ai[MLordAiSlots.PartCoreIndex] != core.whoAmI) {
                    continue;
                }
                if (npc.type == NPCID.MoonLordHand) {
                    bool alive = npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark;
                    if ((int)npc.ai[MLordAiSlots.HandSide] == 0) {
                        leftHand = npc.whoAmI;
                        leftAlive = alive;
                    }
                    else {
                        rightHand = npc.whoAmI;
                        rightAlive = alive;
                    }
                }
                else if (npc.type == NPCID.MoonLordHead) {
                    head = npc.whoAmI;
                    headAlive = npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark;
                }
            }
            return new MLordPartsStatus(leftHand, rightHand, head, leftAlive, rightAlive, headAlive);
        }

        /// <summary>场上隶属该核心的真眼列表写入 buffer，返回数量（钳到缓冲长度，防越界读默认槽）</summary>
        public static int ScanFreeEyes(NPC core, int[] buffer) {
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
    }
}
