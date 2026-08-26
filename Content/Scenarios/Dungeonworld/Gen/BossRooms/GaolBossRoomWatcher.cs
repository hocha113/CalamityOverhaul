using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>禁室房态：蛰伏（无骷髅头）→ 布防（骷髅头就位）→ 封禁（怨灵在场，门洞封死）
    /// → 清剿（本次进入永久熄灯）。脱战消散走 封禁→蛰伏 并吃冷却。</summary>
    internal enum GaolRoomPhase : byte
    {
        Dormant,
        Armed,
        Sealed,
        Cleared,
    }

    /// <summary>
    /// 深牢禁室运行时看守（服务器权威）。生成期 GaolBossRoom.Place 把房间坐标登记进来；
    /// 运行时按 <see cref="GaolRoomPhase"/> 状态机巡检：玩家接近时布防蛰伏骷髅头，
    /// 怨灵现身时用 <see cref="GaolRoomSealTile"/> 封死两侧门洞（tile 事务，整块回播），
    /// 击杀走 <see cref="NotifyWraithDefeated"/> 解封并永久熄灯，脱战消散解封后冷却重蛰伏。
    /// 客户端不做任何裁决：tile 随区块自然同步，房态靠 <see cref="GaolRoomNet"/> 全量/增量下发,
    /// 仅供本地演出（氛围层、封门能量栅）取用。世界为 ShouldSave=false 回放制，
    /// 注册表不持久化，每次进世界随生成期重登记，属预期行为。
    /// </summary>
    internal class GaolBossRoomWatcher : GaolModSystem
    {
        internal sealed class RoomState
        {
            internal Point Origin;
            internal GaolRoomPhase Phase;
            /// <summary>重蛰伏冷却（tick，仅 Dormant 相计数）</summary>
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

        /// <summary>房间注册表：服务器权威态；客户端为 GaolRoomNet 下发的只读镜像</summary>
        internal static readonly List<RoomState> Rooms = [];
        private int checkTimer;

        //==================== 登记与通报（跨单元契约，签名冻结）====================

        /// <summary>登记一间禁室（生成期或测试物品调用；按坐标去重）</summary>
        internal static void RegisterRoom(Point origin) {
            if (!DeepGaolWraithGate.Enabled) {
                return;
            }
            foreach (RoomState room in Rooms) {
                if (room.Origin == origin) {
                    return;
                }
            }
            Rooms.Add(new RoomState { Origin = origin });
        }

        /// <summary>怨灵被击杀时由 DeepGaolWraith.OnKill 通报（服务器）；
        /// 命中最近的登记房间；测试物品在野外召的怨灵找不到房间则无事发生</summary>
        internal static void NotifyWraithDefeated(Vector2 where) {
            RoomState best = null;
            float bestDist = RoomBindDistance;
            foreach (RoomState room in Rooms) {
                float dist = Vector2.Distance(GaolBossRoom.AltarWorldPos(room.Origin), where);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = room;
                }
            }
            if (best == null || best.Phase == GaolRoomPhase.Cleared) {
                return;
            }
            UnsealDoors(best);
            ExtinguishLights(best);
            SetPhase(best, GaolRoomPhase.Cleared);
        }

        public override void ClearWorld() {
            Rooms.Clear();
            checkTimer = 0;
        }

        //==================== 巡检（服务器权威状态机）====================

        public override void PostUpdateNPCs() {
            //服务器权威：客户端不做任何裁决，骷髅头/怨灵实体乘 SyncNPC 过线
            if (VaultUtils.isClient || Rooms.Count == 0) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            int skullType = ModContent.NPCType<GaolDormantSkull>();
            int wraithType = ModContent.NPCType<DeepGaolWraith>();

            //一遍扫场：把在场骷髅头/怨灵按距离归属到房间（房间数个位数，巡检 20 tick 一次，分配可忽略）
            bool[] hasSkull = new bool[Rooms.Count];
            bool[] hasWraith = new bool[Rooms.Count];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || (npc.type != skullType && npc.type != wraithType)) {
                    continue;
                }
                for (int r = 0; r < Rooms.Count; r++) {
                    if (Vector2.Distance(GaolBossRoom.AltarWorldPos(Rooms[r].Origin), npc.Center) < RoomBindDistance) {
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

            for (int r = 0; r < Rooms.Count; r++) {
                RoomState room = Rooms[r];
                switch (room.Phase) {
                    case GaolRoomPhase.Dormant:
                        //野外测试召进房里的怨灵也算开战：直接过封禁裁决
                        if (hasWraith[r]) {
                            SealDoors(room);
                            SetPhase(room, GaolRoomPhase.Sealed);
                            break;
                        }
                        if (room.Cooldown > 0) {
                            room.Cooldown -= CheckInterval;
                            break;
                        }
                        if (!hasSkull[r]
                            && AnyPlayerNear(GaolBossRoom.AltarWorldPos(room.Origin), ArmDistance)) {
                            SpawnDormantSkull(room);
                            SetPhase(room, GaolRoomPhase.Armed);
                        }
                        break;

                    case GaolRoomPhase.Armed:
                        if (hasWraith[r]) {
                            //骷髅头已苏醒成怨灵：封门开战
                            SealDoors(room);
                            SetPhase(room, GaolRoomPhase.Sealed);
                        }
                        else if (!hasSkull[r]) {
                            //骷髅头无战斗地消失（区块卸载/管理清场）：回蛰伏吃冷却
                            SetPhase(room, GaolRoomPhase.Dormant);
                            room.Cooldown = RearmCooldown;
                        }
                        break;

                    case GaolRoomPhase.Sealed:
                        if (!hasWraith[r]) {
                            //怨灵没了却没收到击杀通报=脱战消散：解封、冷却后重蛰伏
                            UnsealDoors(room);
                            SetPhase(room, GaolRoomPhase.Dormant);
                            room.Cooldown = RearmCooldown;
                        }
                        else {
                            //封禁期补漏：布封时被玩家占位跳过的格子，等人挪开随巡检补上
                            SealDoors(room);
                        }
                        break;

                    case GaolRoomPhase.Cleared:
                        break;
                }
            }
        }

        /// <summary>状态迁移唯一入口：改相位 + 联机广播 + 本地演出钩子</summary>
        private static void SetPhase(RoomState room, GaolRoomPhase phase) {
            if (room.Phase == phase) {
                return;
            }
            room.Phase = phase;
            GaolRoomNet.BroadcastPhase(room.Origin, phase);
            //单人在此直接出演出；联机客户端走 GaolRoomNet 收包侧的同名钩子
            if (VaultUtils.isSinglePlayer) {
                PlayPhaseCue(room.Origin, phase);
            }
        }

        /// <summary>相位切换的端上演出（封门锁响/解封开锁），服务器端跳过</summary>
        internal static void PlayPhaseCue(Point origin, GaolRoomPhase phase) {
            if (Main.dedServ) {
                return;
            }
            Vector2 altar = GaolBossRoom.AltarWorldPos(origin);
            if (Vector2.Distance(Main.LocalPlayer.Center, altar) > 2400f) {
                return;
            }
            if (phase == GaolRoomPhase.Sealed) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.6f, Volume = 1.2f }, altar);
            }
            else if (phase is GaolRoomPhase.Dormant or GaolRoomPhase.Cleared) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.2f }, altar);
            }
        }

        //==================== 封门/解封/熄灯（服务器 tile 事务，执行后整块回播）====================

        /// <summary>门洞矩形（世界 tile 坐标）：左右各 3 宽 x DoorHeight 高</summary>
        private static Rectangle[] DoorRects(Point origin) => [
            new(origin.X + GaolBossRoom.LeftDoorOffset.X, origin.Y + GaolBossRoom.LeftDoorOffset.Y,
                3, GaolBossRoom.DoorHeight),
            new(origin.X + GaolBossRoom.RightDoorOffset.X, origin.Y + GaolBossRoom.RightDoorOffset.Y,
                3, GaolBossRoom.DoorHeight),
        ];

        /// <summary>
        /// 封门事务：门洞空格填封门砖。多人公平：被玩家碰撞箱压住的格子先跳过
        /// （绝不把人夹进砖里），封禁期巡检会持续补漏直到封满。幂等，可重入。
        /// </summary>
        private static void SealDoors(RoomState room) {
            int sealType = ModContent.TileType<GaolRoomSealTile>();
            foreach (Rectangle door in DoorRects(room.Origin)) {
                bool changed = false;
                for (int x = door.Left; x < door.Right; x++) {
                    for (int y = door.Top; y < door.Bottom; y++) {
                        if (!WorldGen.InWorld(x, y, 5)) {
                            continue;
                        }
                        Tile tile = Main.tile[x, y];
                        if (tile.HasTile || AnyPlayerTouchesCell(x, y)) {
                            continue;
                        }
                        tile.HasTile = true;
                        tile.TileType = (ushort)sealType;
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;
                        changed = true;
                    }
                }
                if (changed) {
                    ReplayDoor(door);
                }
            }
        }

        /// <summary>解封事务：只拆本看守放的封门砖，异物不动（照样挡路，但不归我们负责）</summary>
        private static void UnsealDoors(RoomState room) {
            int sealType = ModContent.TileType<GaolRoomSealTile>();
            foreach (Rectangle door in DoorRects(room.Origin)) {
                bool changed = false;
                for (int x = door.Left; x < door.Right; x++) {
                    for (int y = door.Top; y < door.Bottom; y++) {
                        if (!WorldGen.InWorld(x, y, 5)) {
                            continue;
                        }
                        Tile tile = Main.tile[x, y];
                        if (!tile.HasTile || tile.TileType != sealType) {
                            continue;
                        }
                        tile.HasTile = false;
                        changed = true;
                    }
                }
                if (changed) {
                    ReplayDoor(door);
                }
            }
        }

        /// <summary>
        /// 清房熄灯：扫房内笼式吊灯拉灭（原版接线开关，跳过导线联动）、水蜡烛拔除。
        /// 永久性由 Cleared 相保证：本次进入不再有任何布防路径。
        /// </summary>
        private static void ExtinguishLights(RoomState room) {
            Rectangle bounds = GaolBossRoom.Bounds(room.Origin);
            for (int x = bounds.Left; x < bounds.Right; x++) {
                for (int y = bounds.Top; y < bounds.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) {
                        continue;
                    }
                    if (tile.TileType == TileID.HangingLanterns) {
                        //forced=false 幂等灭灯；上下半格各调一次也只翻一次帧
                        Wiring.ToggleHangingLantern(x, y, tile, forcedStateWhereTrueIsOn: false,
                            doSkipWires: true);
                        ReplayCell(x, y, 1, 2);
                    }
                    else if (tile.TileType == TileID.WaterCandle) {
                        WorldGen.KillTile(x, y, noItem: true);
                        ReplayCell(x, y, 1, 1);
                    }
                }
            }
        }

        /// <summary>门洞整块帧修 + 联机回播</summary>
        private static void ReplayDoor(Rectangle door) {
            WorldGen.RangeFrame(door.Left - 1, door.Top - 1, door.Right + 1, door.Bottom + 1);
            if (VaultUtils.isServer) {
                NetMessage.SendTileSquare(-1, door.Left, door.Top, door.Width, door.Height);
            }
        }

        private static void ReplayCell(int x, int y, int w, int h) {
            WorldGen.RangeFrame(x - 1, y - 1, x + w + 1, y + h + 1);
            if (VaultUtils.isServer) {
                NetMessage.SendTileSquare(-1, x, y, w, h);
            }
        }

        /// <summary>玩家碰撞箱是否压住该 tile 格（封门防夹裁决）</summary>
        private static bool AnyPlayerTouchesCell(int x, int y) {
            Rectangle cell = new(x * 16, y * 16, 16, 16);
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Hitbox.Intersects(cell)) {
                    return true;
                }
            }
            return false;
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
