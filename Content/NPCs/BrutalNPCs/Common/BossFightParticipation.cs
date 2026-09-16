using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.Common
{
    /// <summary>
    /// 本机玩家"打过谁"的客户端账本。原版 <see cref="NPC.playerInteraction"/> 在联机客户端恒为空
    /// （StrikeNPC 只在单机写它），所以自己在命中钩子里记：按 whoAmI 存 type，槽位被复用即失效；
    /// 部件命中向父体传播（石巨人拳/头 → 躯干，骷髅王手 → 头，机械骷髅王四臂 → 头，月总部件 → 核心，realLife 段 → 本体），
    /// 父体要求活着且类型对得上，避免非残酷模式下 ai 槽语义不同时误记
    /// </summary>
    internal sealed class BossFightParticipationNPC : GlobalNPC
    {
        private static readonly int[] hitType = new int[Main.maxNPCs];
        private static bool initialized;

        private static void EnsureInit() {
            if (initialized) {
                return;
            }
            Clear();
            initialized = true;
        }

        internal static void Clear() {
            for (int i = 0; i < hitType.Length; i++) {
                hitType[i] = -1;
            }
            initialized = true;
        }

        /// <summary>本机玩家是否打过这个 NPC（含其部件）</summary>
        internal static bool LocalPlayerHit(NPC npc) {
            if (npc == null || !npc.active || npc.whoAmI < 0 || npc.whoAmI >= Main.maxNPCs) {
                return false;
            }
            EnsureInit();
            return hitType[npc.whoAmI] == npc.type;
        }

        internal static void Mark(NPC npc) {
            if (Main.dedServ || npc == null || !npc.active) {
                return;
            }
            EnsureInit();
            Record(npc);
            int parent = ResolveParent(npc);
            if (parent >= 0 && parent < Main.maxNPCs && parent != npc.whoAmI && Main.npc[parent].active) {
                Record(Main.npc[parent]);
            }
        }

        private static void Record(NPC npc) {
            if (npc.whoAmI >= 0 && npc.whoAmI < Main.maxNPCs) {
                hitType[npc.whoAmI] = npc.type;
            }
        }

        /// <summary>部件 → 父体索引；父体类型不符返回 -1</summary>
        private static int ResolveParent(NPC npc) {
            int index;
            int expectedType;
            switch (npc.type) {
                case NPCID.GolemFistLeft:
                case NPCID.GolemFistRight:
                case NPCID.GolemHead:
                case NPCID.GolemHeadFree:
                    index = (int)npc.ai[GolemAiSlots.PartBodyIndex];
                    expectedType = NPCID.Golem;
                    break;
                case NPCID.SkeletronHand:
                    index = (int)npc.ai[SkeletronAiSlots.HandHeadIndex];
                    expectedType = NPCID.SkeletronHead;
                    break;
                case NPCID.PrimeCannon:
                case NPCID.PrimeLaser:
                case NPCID.PrimeSaw:
                case NPCID.PrimeVice:
                    index = (int)npc.ai[PrimeAiSlots.ArmHeadIndex];
                    expectedType = NPCID.SkeletronPrime;
                    break;
                case NPCID.MoonLordHand:
                case NPCID.MoonLordHead:
                    index = (int)npc.ai[MLordAiSlots.PartCoreIndex];
                    expectedType = NPCID.MoonLordCore;
                    break;
                default:
                    if (npc.realLife >= 0 && npc.realLife != npc.whoAmI) {
                        return npc.realLife;
                    }
                    return -1;
            }
            if (index < 0 || index >= Main.maxNPCs || Main.npc[index].type != expectedType) {
                return -1;
            }
            return index;
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (player.whoAmI == Main.myPlayer) {
                Mark(npc);
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner == Main.myPlayer) {
                Mark(npc);
            }
        }
    }

    /// <summary>账本失效：无 Boss 在场持续两秒即清空；进出世界清空</summary>
    internal sealed class BossFightParticipationSystem : ModSystem
    {
        private int noBossTicks;

        public override void OnWorldLoad() => BossFightParticipationNPC.Clear();

        public override void OnWorldUnload() => BossFightParticipationNPC.Clear();

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (CWRWorld.HasBoss) {
                noBossTicks = 0;
                return;
            }
            if (++noBossTicks == 120) {
                BossFightParticipationNPC.Clear();
            }
        }
    }

    /// <summary>
    /// 死亡演出观众门槛：打过这个 Boss（含部件）或人在 <see cref="WatchDistance"/> 内。
    /// 判定按 Boss 实例缓存，5 帧内重复查询直接复用——同一场演出只判一次，
    /// 中途走近的人不会从 tick 0 起播一段和 Boss 时间轴错位的运镜
    /// </summary>
    internal static class BossDeathSpectator
    {
        internal const float WatchDistance = 2600f;
        private const uint CacheTicks = 5;

        private static int cachedWhoAmI = -1;
        private static int cachedType = -1;
        private static bool cachedResult;
        private static uint lastQueryTick;

        internal static bool ShouldWatch(Player player, NPC boss) {
            if (Main.dedServ || player == null || !player.active || boss == null || !boss.active) {
                return false;
            }
            uint now = Main.GameUpdateCount;
            if (cachedWhoAmI == boss.whoAmI && cachedType == boss.type && now - lastQueryTick <= CacheTicks) {
                lastQueryTick = now;
                return cachedResult;
            }
            bool hit = BossFightParticipationNPC.LocalPlayerHit(boss);
            bool near = player.Distance(boss.Center) < WatchDistance;
            cachedWhoAmI = boss.whoAmI;
            cachedType = boss.type;
            cachedResult = hit || near;
            lastQueryTick = now;
            return cachedResult;
        }
    }
}
