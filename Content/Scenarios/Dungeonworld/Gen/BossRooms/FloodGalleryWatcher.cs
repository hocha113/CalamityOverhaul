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
    /// 泄洪堂运行时看守 + 水位/封门控制器（服务器权威，镜像 GaolBossRoomWatcher）。
    /// 生成期 FloodGalleryRoom.Place 把房间坐标登记进来；运行时玩家接近且本次进入
    /// 尚未清剿时，写入踝水并在王座壁龛布置蛰伏体。
    /// 复位裁决：不溺者被击杀 → 本次进入该房永久熄灯（排水至干+解封由死亡演出执行，
    /// 此处幂等兜底）；脱战消散（未死）→ 排水至踝水+解封，冷却 300t 后重蛰伏。
    /// 世界 ShouldSave=false 回放制，注册表不持久化、只存在于权威端；
    /// 一切 tile 写入都是一次性事务 + 分块 SendTileSquare（无逐帧 tile 动画），
    /// 客户端的涨水/封门画面由 tile 快照直接呈现，迟入场玩家看到当前水位不重播警报。
    /// </summary>
    internal class FloodGalleryWatcher : UndrownedModSystem
    {
        private sealed class RoomState
        {
            internal Point Origin;
            /// <summary>本次进入已击杀，不再复燃</summary>
            internal bool Cleared;
            /// <summary>幂等兜底闩：清剿后的排水+解封只做一次</summary>
            internal bool ClearedSwept;
            /// <summary>上次巡检时不溺者在场（用于识别脱战消散/团灭）</summary>
            internal bool Engaged;
            /// <summary>重蛰伏冷却（tick）</summary>
            internal int Cooldown;
            /// <summary>当前已写入的水面行（rel；FloorRel=干）</summary>
            internal int SurfaceRel = FloodGalleryRoom.FloorRel;
            internal bool Sealed;
        }

        //==================== 参数 ====================

        /// <summary>玩家离王座多近时布置蛰伏体并写踝水（远于视野，到场时已就位）</summary>
        internal const float ArmDistance = 2000f;
        /// <summary>NPC/结算归属房间的判定半径（略大于房间对角线 ~1420px）</summary>
        internal const float RoomBindDistance = 2600f;
        private const int CheckInterval = 20;
        private const int RearmCooldown = 300;

        private static readonly List<RoomState> rooms = [];
        private int checkTimer;

        //==================== 登记与通报 ====================

        /// <summary>登记一间泄洪堂（生成期或测试钥匙调用；按坐标去重）</summary>
        internal static void RegisterRoom(Point origin) {
            if (!UndrownedGate.Enabled) {
                return;
            }
            foreach (RoomState room in rooms) {
                if (room.Origin == origin) {
                    return;
                }
            }
            rooms.Add(new RoomState { Origin = origin });
        }

        /// <summary>不溺者被击杀时由 Undrowned.OnKill 通报（服务器）；
        /// 命中最近的登记房间；野外测试召唤找不到房间则无事发生</summary>
        internal static void NotifyUndrownedDefeated(Vector2 where) {
            RoomState best = FindRoom(where);
            if (best != null) {
                best.Cleared = true;
                best.Engaged = false;
            }
        }

        /// <summary>按世界坐标找归属房间（绑定半径内最近者），服务器专用</summary>
        private static RoomState FindRoom(Vector2 where) {
            RoomState best = null;
            float bestDist = RoomBindDistance;
            foreach (RoomState room in rooms) {
                float dist = Vector2.Distance(RoomCenterWorld(room.Origin), where);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = room;
                }
            }
            return best;
        }

        internal static Vector2 RoomCenterWorld(Point origin)
            => new((origin.X + FloodGalleryRoom.Width * 0.5f) * 16f,
                (origin.Y + FloodGalleryRoom.Height * 0.5f) * 16f);

        /// <summary>死亡泄洪阶梯的出发水面（服务器查询；未登记返回刻度二）</summary>
        internal static int GetRoomSurfaceRel(Point origin) {
            foreach (RoomState room in rooms) {
                if (room.Origin == origin) {
                    return room.SurfaceRel;
                }
            }
            return FloodGalleryRoom.Scale2SurfaceRel;
        }

        public override void ClearWorld() {
            rooms.Clear();
            checkTimer = 0;
            DungeonworldBossRecords.ResetServerMirror();
        }

        //==================== 巡检 ====================

        public override void PostUpdateNPCs() {
            //服务器权威：客户端不做任何裁决，蛰伏体/不溺者实体乘 SyncNPC 过线
            if (VaultUtils.isClient || rooms.Count == 0) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            int throneType = ModContent.NPCType<UndrownedThrone>();
            int bossType = ModContent.NPCType<Undrowned>();

            bool[] hasThrone = new bool[rooms.Count];
            bool[] hasBoss = new bool[rooms.Count];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || (npc.type != throneType && npc.type != bossType)) {
                    continue;
                }
                for (int r = 0; r < rooms.Count; r++) {
                    if (Vector2.Distance(RoomCenterWorld(rooms[r].Origin), npc.Center) < RoomBindDistance) {
                        if (npc.type == throneType) {
                            hasThrone[r] = true;
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
                    //幂等兜底：死亡演出已做过排水+解封，这里只补漏（如超杀跳拍）
                    if (!room.ClearedSwept) {
                        room.ClearedSwept = true;
                        if (room.SurfaceRel < FloodGalleryRoom.FloorRel) {
                            ApplyWater(room.Origin, FloodGalleryRoom.FloorRel);
                        }
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
                    //不溺者没了却没收到击杀通报=脱战/团灭：排水至踝水+解封，冷却重蛰伏
                    room.Engaged = false;
                    room.Cooldown = RearmCooldown;
                    ApplyWater(room.Origin, FloodGalleryRoom.AnkleSurfaceRel, FloodGalleryRoom.AnkleAmount);
                    if (room.Sealed) {
                        SealDoors(room.Origin, false);
                    }
                }
                if (hasThrone[r]) {
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

        /// <summary>室内是否仍有存活玩家（封门裁决用，防锁空房）</summary>
        internal static bool AnyAlivePlayerInRoom(Point origin) {
            Rectangle worldRect = new(origin.X * 16, origin.Y * 16,
                FloodGalleryRoom.Width * 16, FloodGalleryRoom.Height * 16);
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && worldRect.Contains(player.Center.ToPoint())) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>闸门落锁播报（开战时在门外的玩家本场进不来，明说）</summary>
        internal static void AnnounceSealed() {
            LocalizedText text = Language.GetText("Mods.CalamityOverhaul.NPCs.Undrowned.GateSealed");
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(text.ToNetworkText(), new Color(88, 154, 148));
            }
            else {
                Main.NewText(text.Value, 88, 154, 148);
            }
        }

        /// <summary>arm：写踝水 + 王座壁龛布置蛰伏体（房间坐标字段先写后 SyncNPC，原子过线）</summary>
        private static void ArmRoom(RoomState room) {
            ApplyWater(room.Origin, FloodGalleryRoom.AnkleSurfaceRel, FloodGalleryRoom.AnkleAmount);
            Vector2 throne = FloodGalleryRoom.ThroneWorldPos(room.Origin);
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)throne.X, (int)throne.Y,
                ModContent.NPCType<UndrownedThrone>());
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC npc = Main.npc[idx];
                npc.Center = throne;
                if (npc.ModNPC is UndrownedThrone dormant) {
                    dormant.roomOriginX = room.Origin.X;
                    dormant.roomOriginY = room.Origin.Y;
                }
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }
        }

        //==================== 水位控制器（一次性事务 + 分块回播）====================

        /// <summary>
        /// 把整个内膛重写成指定水面行的液体版图（rel；FloorRel=干）。
        /// 全量重写幂等自愈，单次 ≤68×39≈2650 格，毫秒级。
        /// 本房水体不注册 L4WaterWorks 全局舱段表：全局阀切换永远碰不到它；
        /// NormalUpdates=false 下写完即静定（F17），破洞不漏水。
        /// 仅权威端执行，随后分块回播区块。
        /// </summary>
        internal static void ApplyWater(Point origin, int surfaceRel, byte topAmount = byte.MaxValue) {
            if (VaultUtils.isClient) {
                return;
            }
            for (int rx = FloodGalleryRoom.InteriorLeft; rx <= FloodGalleryRoom.InteriorRight; rx++) {
                for (int ry = FloodGalleryRoom.InteriorTop; ry < FloodGalleryRoom.FloorRel; ry++) {
                    int x = origin.X + rx;
                    int y = origin.Y + ry;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    //实心非平台格不持液体（与 WaterCheck 判据一致，防幽灵水）
                    if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                        continue;
                    }
                    if (ry < surfaceRel) {
                        t.LiquidAmount = 0;
                    }
                    else {
                        t.LiquidAmount = ry == surfaceRel ? topAmount : byte.MaxValue;
                        t.LiquidType = LiquidID.Water;
                    }
                }
            }
            foreach (RoomState room in rooms) {
                if (room.Origin == origin) {
                    room.SurfaceRel = surfaceRel;
                    break;
                }
            }
            BroadcastRoom(origin);
        }

        /// <summary>封门/解封：两个 3×4 门洞写实心绿砖或恢复空（水密语义只是戏剧，
        /// 冻结水本就不流动）。仅权威端执行 + 帧修 + 回播</summary>
        internal static void SealDoors(Point origin, bool seal) {
            if (VaultUtils.isClient) {
                return;
            }
            WriteDoor(origin, FloodGalleryRoom.LeftDoorOffset, seal);
            WriteDoor(origin, FloodGalleryRoom.RightDoorOffset, seal);
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
                for (int dy = 0; dy < FloodGalleryRoom.DoorHeight; dy++) {
                    int x = origin.X + doorOffset.X + dx;
                    int y = origin.Y + doorOffset.Y + dy;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (seal) {
                        tile.HasTile = true;
                        tile.TileType = TileID.GreenDungeonBrick;
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
                origin.X + doorOffset.X + 3, origin.Y + doorOffset.Y + FloodGalleryRoom.DoorHeight);
        }

        /// <summary>死亡演出：格栅换裂纹漆（棕漆盖灰漆），一次性小事务</summary>
        internal static void PaintGrateCracked(Point origin) {
            if (VaultUtils.isClient) {
                return;
            }
            int y = origin.Y + FloodGalleryRoom.FloorRel;
            for (int gx = FloodGalleryRoom.GrateLeft; gx <= FloodGalleryRoom.GrateRight; gx++) {
                int x = origin.X + gx;
                if (WorldGen.InWorld(x, y, 5)) {
                    WorldGen.paintTile(x, y, PaintID.BrownPaint);
                }
            }
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendTileSquare(-1, origin.X + FloodGalleryRoom.GrateLeft - 1, y - 1,
                    FloodGalleryRoom.GrateRight - FloodGalleryRoom.GrateLeft + 3, 3);
            }
        }

        /// <summary>整房分块回播（SendTileSquare 单次矩形有限，按 32 格分块，74×48→6 块）</summary>
        private static void BroadcastRoom(Point origin) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            const int Chunk = 32;
            for (int x = 0; x < FloodGalleryRoom.Width; x += Chunk) {
                for (int y = 0; y < FloodGalleryRoom.Height; y += Chunk) {
                    NetMessage.SendTileSquare(-1, origin.X + x, origin.Y + y,
                        System.Math.Min(Chunk, FloodGalleryRoom.Width - x),
                        System.Math.Min(Chunk, FloodGalleryRoom.Height - y));
                }
            }
        }
    }
}
