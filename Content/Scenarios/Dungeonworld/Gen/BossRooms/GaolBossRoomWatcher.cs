using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 深牢禁室运行时看守（服务器权威）。生成期 GaolBossRoom.Place 把房间坐标登记进来；
    /// 运行时玩家接近且本次进入尚未战斗时，在祭坛槽生成蛰伏骷髅头。
    /// 复位裁决（最简）：怨灵被击杀 → 本次进入该房永久熄灯；怨灵脱战消散（未死）→
    /// 冷却 5 秒后骷髅头重新蛰伏。世界为 ShouldSave=false 回放制，注册表不持久化，
    /// 每次进世界随生成期重登记、骷髅头重新蛰伏，属预期行为。
    /// </summary>
    internal class GaolBossRoomWatcher : GaolModSystem
    {
        private sealed class RoomState
        {
            internal Point Origin;
            /// <summary>本次进入已击杀怨灵，不再复燃</summary>
            internal bool Cleared;
            /// <summary>上次巡检时怨灵在场（用于识别脱战消散）</summary>
            internal bool Engaged;
            /// <summary>重蛰伏冷却（tick）</summary>
            internal int Cooldown;
        }

        //==================== 参数（建议值，验收再调）====================

        /// <summary>玩家离祭坛多近时布置蛰伏骷髅头（远于视野，玩家到场时它已就位）</summary>
        internal const float ArmDistance = 2000f;
        /// <summary>NPC 归属房间的判定半径（略大于房间对角线 1180px）</summary>
        private const float RoomBindDistance = 2600f;
        /// <summary>巡检间隔（tick）</summary>
        private const int CheckInterval = 20;
        /// <summary>脱战消散后的重蛰伏冷却</summary>
        private const int RearmCooldown = 300;

        private static readonly List<RoomState> rooms = [];
        private int checkTimer;

        //==================== 登记与通报 ====================

        /// <summary>登记一间禁室（生成期或测试物品调用；按坐标去重）</summary>
        internal static void RegisterRoom(Point origin) {
            if (!DeepGaolWraithGate.Enabled) {
                return;
            }
            foreach (RoomState room in rooms) {
                if (room.Origin == origin) {
                    return;
                }
            }
            rooms.Add(new RoomState { Origin = origin });
        }

        /// <summary>怨灵被击杀时由 DeepGaolWraith.OnKill 通报（服务器）；
        /// 命中最近的登记房间；测试物品在野外召的怨灵找不到房间则无事发生</summary>
        internal static void NotifyWraithDefeated(Vector2 where) {
            RoomState best = null;
            float bestDist = RoomBindDistance;
            foreach (RoomState room in rooms) {
                float dist = Vector2.Distance(GaolBossRoom.AltarWorldPos(room.Origin), where);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = room;
                }
            }
            if (best != null) {
                best.Cleared = true;
                best.Engaged = false;
            }
        }

        public override void ClearWorld() {
            rooms.Clear();
            checkTimer = 0;
        }

        //==================== 巡检 ====================

        public override void PostUpdateNPCs() {
            //服务器权威：客户端不做任何裁决，骷髅头/怨灵实体乘 SyncNPC 过线
            if (VaultUtils.isClient || rooms.Count == 0) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            int skullType = ModContent.NPCType<GaolDormantSkull>();
            int wraithType = ModContent.NPCType<DeepGaolWraith>();

            //一遍扫场：把在场骷髅头/怨灵按距离归属到房间（房间数个位数，巡检 20 tick 一次，分配可忽略）
            bool[] hasSkull = new bool[rooms.Count];
            bool[] hasWraith = new bool[rooms.Count];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || (npc.type != skullType && npc.type != wraithType)) {
                    continue;
                }
                for (int r = 0; r < rooms.Count; r++) {
                    if (Vector2.Distance(GaolBossRoom.AltarWorldPos(rooms[r].Origin), npc.Center) < RoomBindDistance) {
                        if (npc.type == skullType) {
                            hasSkull[r] = true;
                        }
                        else {
                            hasWraith[r] = true;
                        }
                        break;
                    }
                }
            }

            for (int r = 0; r < rooms.Count; r++) {
                RoomState room = rooms[r];
                if (room.Cleared) {
                    continue;
                }
                if (hasWraith[r]) {
                    room.Engaged = true;
                    continue;
                }
                if (room.Engaged) {
                    //怨灵没了却没收到击杀通报=脱战消散，冷却后重蛰伏
                    room.Engaged = false;
                    room.Cooldown = RearmCooldown;
                }
                if (hasSkull[r]) {
                    continue;
                }
                if (room.Cooldown > 0) {
                    room.Cooldown -= CheckInterval;
                    continue;
                }
                if (AnyPlayerNear(GaolBossRoom.AltarWorldPos(room.Origin), ArmDistance)) {
                    SpawnDormantSkull(room);
                }
            }
        }

        private static bool AnyPlayerNear(Vector2 pos, float dist) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && Vector2.Distance(player.Center, pos) < dist) {
                    return true;
                }
            }
            return false;
        }

        private static void SpawnDormantSkull(RoomState room) {
            Vector2 altar = GaolBossRoom.AltarWorldPos(room.Origin);
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)altar.X, (int)altar.Y,
                ModContent.NPCType<GaolDormantSkull>());
            if (idx >= 0 && idx < Main.maxNPCs) {
                Main.npc[idx].Center = altar;
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }
        }
    }
}
