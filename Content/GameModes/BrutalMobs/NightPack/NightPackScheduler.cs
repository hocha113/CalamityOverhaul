using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.NightPack
{
    /// <summary>夜行猎群家族</summary>
    internal enum NightPackFamily
    {
        Zombie,
        DemonEye,
        Skeleton,
        CaveBat,
    }

    /// <summary>
    /// 攻击令牌调度器。世界级状态，只由服务端决策路径读写（客户端在 <see cref="NightPackNPC.PostAI"/> 早退，
    /// 客户端可见结果全部来自弹幕实体与 NPC 速度的原生同步）。
    /// 同族并发硬上限与猎群错拍节拍既是机制卖点也是公平阀门
    /// </summary>
    internal static class NightPackScheduler
    {
        /// <summary>同族同时突进数硬上限：任意时刻扑向玩家的同族攻击不超过此数（公平阀门）</summary>
        internal const int MaxConcurrentPerFamily = 2;

        /// <summary>猎群成型数量：目标玩家周边同族达到此数时启用错拍节拍</summary>
        internal const int PackSize = 3;

        /// <summary>猎群聚集判定半径（以目标玩家为圆心）</summary>
        internal const float PackRadius = 800f;

        /// <summary>钳形分离角下限：并发的第二只恶魔眼进攻方向与首只至少相差此角，两道俯冲构成钳形而非同线叠加</summary>
        internal const float PincerMinSeparationRad = 1.1f;

        private struct Token
        {
            public NightPackFamily Family;
            public int NpcIndex;
            public int NpcType;
            public uint ExpireTick;
            public float ApproachAngle;
        }

        /// <summary>存活令牌，容量恒小（≤2×家族数）</summary>
        private static readonly List<Token> live = new(MaxConcurrentPerFamily * 4);

        /// <summary>各家族上次授予令牌的时刻，错拍节拍基准</summary>
        private static readonly uint[] lastGrant = new uint[4];

        /// <summary>
        /// 类型 → 家族。僵尸直接用 <see cref="NPCID.Sets.Zombies"/>，
        /// 但剔除集合里的血月变体 TheGroom/TheBride（军团组领地）
        /// </summary>
        internal static bool TryGetFamily(int type, out NightPackFamily family) {
            if (NPCID.Sets.Zombies[type] && type != NPCID.TheGroom && type != NPCID.TheBride) {
                family = NightPackFamily.Zombie;
                return true;
            }
            if (NPCID.Sets.DemonEyes[type]) {
                family = NightPackFamily.DemonEye;
                return true;
            }
            if (type is NPCID.Skeleton or NPCID.HeadacheSkeleton or NPCID.MisassembledSkeleton
                or NPCID.PantlessSkeleton or NPCID.UndeadMiner) {
                family = NightPackFamily.Skeleton;
                return true;
            }
            if (type == NPCID.CaveBat) {
                family = NightPackFamily.CaveBat;
                return true;
            }
            family = default;
            return false;
        }

        /// <summary>申请攻击令牌。approachAngle 为进攻方向（怪指向玩家），供钳形分离检查</summary>
        internal static bool TryAcquire(NPC npc, NightPackFamily family, Vector2 targetCenter,
            int leaseTicks, int staggerTicks, float approachAngle) {
            Prune();

            int concurrent = 0;
            float firstAngle = 0f;
            for (int i = 0; i < live.Count; i++) {
                if (live[i].Family != family) {
                    continue;
                }
                if (concurrent == 0) {
                    firstAngle = live[i].ApproachAngle;
                }
                concurrent++;
            }

            if (concurrent >= MaxConcurrentPerFamily) {
                return false;
            }

            //钳形约束：并发的第二只恶魔眼必须来自足够不同的方向
            if (family == NightPackFamily.DemonEye && concurrent > 0
                && Math.Abs(MathHelper.WrapAngle(approachAngle - firstAngle)) < PincerMinSeparationRad) {
                return false;
            }

            //猎群成型后错拍：同族两次进攻至少间隔 staggerTicks
            if (CountPack(family, targetCenter) >= PackSize
                && Main.GameUpdateCount - lastGrant[(int)family] < (uint)staggerTicks) {
                return false;
            }

            live.Add(new Token {
                Family = family,
                NpcIndex = npc.whoAmI,
                NpcType = npc.type,
                ExpireTick = Main.GameUpdateCount + (uint)leaseTicks,
                ApproachAngle = approachAngle,
            });
            lastGrant[(int)family] = Main.GameUpdateCount;
            return true;
        }

        /// <summary>归还令牌。正常收招时调用；死亡与丢失由租期到期和槽位类型校验兜底</summary>
        internal static void Release(NPC npc) {
            for (int i = live.Count - 1; i >= 0; i--) {
                if (live[i].NpcIndex == npc.whoAmI && live[i].NpcType == npc.type) {
                    live.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>清理过期、死亡与槽位易主的令牌（槽位复用靠类型校验识破）</summary>
        private static void Prune() {
            uint now = Main.GameUpdateCount;
            for (int i = live.Count - 1; i >= 0; i--) {
                Token token = live[i];
                NPC npc = Main.npc[token.NpcIndex];
                if (now >= token.ExpireTick || !npc.active || npc.type != token.NpcType) {
                    live.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清空全部令牌与节拍基准。GameUpdateCount 每次进世界归零，
        /// 跨世界残留的 ExpireTick 会伪装成远未过期，若旧槽位恰被同类型新怪占用则 Prune 三条件全部失守，
        /// 一个幽灵令牌能长期占死同族并发位，故进出世界必须清零
        /// </summary>
        internal static void ClearAll() {
            live.Clear();
            Array.Clear(lastGrant, 0, lastGrant.Length);
        }

        /// <summary>统计目标玩家周边的同族数量。仅在申请令牌时调用，不进每帧路径</summary>
        private static int CountPack(NightPackFamily family, Vector2 targetCenter) {
            int count = 0;
            float radiusSq = PackRadius * PackRadius;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active
                    || !TryGetFamily(other.type, out NightPackFamily otherFamily)
                    || otherFamily != family
                    || Vector2.DistanceSquared(other.Center, targetCenter) > radiusSq) {
                    continue;
                }
                count++;
            }
            return count;
        }
    }

    /// <summary>世界清理钩子：调度器是世界级 static，进出世界统一清零（服务端与单人都会走到）</summary>
    internal class NightPackSchedulerReset : ModSystem
    {
        public override void ClearWorld() => NightPackScheduler.ClearAll();
    }
}
