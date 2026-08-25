using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using System.Collections.Generic;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 验收堂运行时看守（服务器权威，镜像 FloodGalleryWatcher 减去水位控制器）。
    /// 生成期 ProofingHallRoom.Place 登记房间；玩家接近且本次进入尚未清剿时，
    /// 在天轨右端布置蒙尘吊臂。复位裁决：监工被击杀 → 本次进入永久熄灯
    /// （解封由死亡演出执行，此处幂等兜底）；脱战消散 → 解封 + 冷却 300t 重蛰伏。
    /// 封门/解封=一次性 tile 事务 + 分块 SendTileSquare（无逐帧 tile 动画）
    /// </summary>
    internal class ProofingHallWatcher : OverseerModSystem
    {
        private sealed class RoomState
        {
            internal Point Origin;
            internal bool Cleared;
            internal bool ClearedSwept;
            internal bool Engaged;
            internal int Cooldown;
            internal bool Sealed;
        }

        internal const float ArmDistance = 2000f;
        internal const float RoomBindDistance = 2600f;
        private const int CheckInterval = 20;
        private const int RearmCooldown = 300;

        private static readonly List<RoomState> rooms = [];
        private int checkTimer;

        //==================== 登记与通报 ====================

        internal static void RegisterRoom(Point origin) {
            if (!FoundryOverseerGate.Enabled) {
                return;
            }
            foreach (RoomState room in rooms) {
                if (room.Origin == origin) {
                    return;
                }
            }
            rooms.Add(new RoomState { Origin = origin });
        }

        internal static void NotifyOverseerDefeated(Vector2 where) {
            RoomState best = null;
            float bestDist = RoomBindDistance;
            foreach (RoomState room in rooms) {
                float dist = Vector2.Distance(RoomCenterWorld(room.Origin), where);
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

        internal static Vector2 RoomCenterWorld(Point origin)
            => new((origin.X + ProofingHallRoom.Width * 0.5f) * 16f,
                (origin.Y + ProofingHallRoom.Height * 0.5f) * 16f);

        public override void ClearWorld() {
            rooms.Clear();
            checkTimer = 0;
            //记录镜像复位：两座 Boss 共用一张表，两个看守各自兜一次（幂等）
            DungeonworldBossRecords.ResetServerMirror();
        }

        //==================== 巡检 ====================

        public override void PostUpdateNPCs() {
            if (VaultUtils.isClient || rooms.Count == 0) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            int rigType = ModContent.NPCType<OverseerDormantRig>();
            int bossType = ModContent.NPCType<FoundryOverseer>();

            bool[] hasRig = new bool[rooms.Count];
            bool[] hasBoss = new bool[rooms.Count];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || (npc.type != rigType && npc.type != bossType)) {
                    continue;
                }
                for (int r = 0; r < rooms.Count; r++) {
                    if (Vector2.Distance(RoomCenterWorld(rooms[r].Origin), npc.Center) < RoomBindDistance) {
                        if (npc.type == rigType) {
                            hasRig[r] = true;
                        }
                        else {
                            hasBoss[r] = true;
                        }
                        break;
                    }
                }
            }

            for (int r = 0; r < rooms.Count; r++) {
                RoomState room = rooms[r];
                if (room.Cleared) {
                    if (!room.ClearedSwept) {
                        room.ClearedSwept = true;
                        if (room.Sealed) {
                            SealDoors(room.Origin, false);
                        }
                    }
                    continue;
                }
                if (hasBoss[r]) {
                    room.Engaged = true;
                    continue;
                }
                if (room.Engaged) {
                    //监工没了却没收到击杀通报=脱战/团灭：解封 + 冷却重蛰伏
                    room.Engaged = false;
                    room.Cooldown = RearmCooldown;
                    if (room.Sealed) {
                        SealDoors(room.Origin, false);
                    }
                }
                if (hasRig[r]) {
                    continue;
                }
                if (room.Cooldown > 0) {
                    room.Cooldown -= CheckInterval;
                    continue;
                }
                if (AnyPlayerNear(RoomCenterWorld(room.Origin), ArmDistance)) {
                    ArmRoom(room);
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

        internal static bool AnyAlivePlayerInRoom(Point origin) {
            Rectangle worldRect = new(origin.X * 16, origin.Y * 16,
                ProofingHallRoom.Width * 16, ProofingHallRoom.Height * 16);
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && worldRect.Contains(player.Center.ToPoint())) {
                    return true;
                }
            }
            return false;
        }

        internal static void AnnounceSealed() {
            LocalizedText text = Language.GetText("Mods.CalamityOverhaul.NPCs.FoundryOverseer.GateSealed");
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(text.ToNetworkText(), new Color(222, 138, 58));
            }
            else {
                Main.NewText(text.Value, 222, 138, 58);
            }
        }

        /// <summary>arm：天轨右端布置蒙尘吊臂（房间坐标字段先写后 SyncNPC，原子过线）</summary>
        private static void ArmRoom(RoomState room) {
            Vector2 rig = ProofingHallRoom.RigWorldPos(room.Origin);
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)rig.X, (int)rig.Y,
                ModContent.NPCType<OverseerDormantRig>());
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC npc = Main.npc[idx];
                npc.Center = rig;
                if (npc.ModNPC is OverseerDormantRig dormant) {
                    dormant.roomOriginX = room.Origin.X;
                    dormant.roomOriginY = room.Origin.Y;
                }
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }
        }

        //==================== 封门（一次性事务 + 分块回播）====================

        /// <summary>封门/解封：两个 3×4 门洞写实心蓝砖或恢复空。仅权威端执行 + 帧修 + 回播</summary>
        internal static void SealDoors(Point origin, bool seal) {
            if (VaultUtils.isClient) {
                return;
            }
            WriteDoor(origin, ProofingHallRoom.LeftDoorOffset, seal);
            WriteDoor(origin, ProofingHallRoom.RightDoorOffset, seal);
            foreach (RoomState room in rooms) {
                if (room.Origin == origin) {
                    room.Sealed = seal;
                    break;
                }
            }
            BroadcastRoom(origin);
        }

        private static void WriteDoor(Point origin, Point doorOffset, bool seal) {
            for (int dx = 0; dx < 3; dx++) {
                for (int dy = 0; dy < ProofingHallRoom.DoorHeight; dy++) {
                    int x = origin.X + doorOffset.X + dx;
                    int y = origin.Y + doorOffset.Y + dy;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (seal) {
                        tile.HasTile = true;
                        tile.TileType = TileID.BlueDungeonBrick;
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;
                        tile.LiquidAmount = 0;
                    }
                    else {
                        tile.HasTile = false;
                    }
                }
            }
            WorldGen.RangeFrame(origin.X + doorOffset.X - 1, origin.Y + doorOffset.Y - 1,
                origin.X + doorOffset.X + 3, origin.Y + doorOffset.Y + ProofingHallRoom.DoorHeight);
        }

        /// <summary>整房分块回播（78×36 → 32 格分块 6 块）</summary>
        private static void BroadcastRoom(Point origin) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            const int Chunk = 32;
            for (int x = 0; x < ProofingHallRoom.Width; x += Chunk) {
                for (int y = 0; y < ProofingHallRoom.Height; y += Chunk) {
                    NetMessage.SendTileSquare(-1, origin.X + x, origin.Y + y,
                        System.Math.Min(Chunk, ProofingHallRoom.Width - x),
                        System.Math.Min(Chunk, ProofingHallRoom.Height - y));
                }
            }
        }
    }
}
