using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.OtherMods.SubWorld;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 替代原版 town spawn；主端寻位后生成 <see cref="VictorRiftPortalProj"/>，弹幕中段 NewNPC
    /// </summary>
    internal class VictorPortalSpawner : ModSystem
    {
        /// <summary>距玩家最小水平 tile</summary>
        private const int MinTileDistance = 11;
        /// <summary>距玩家最大水平 tile</summary>
        private const int MaxTileDistance = 17;
        /// <summary>地面上方空气格，约够 Victor+门</summary>
        private const int RequiredHeadroom = 9;
        /// <summary>地面下方连续固体格</summary>
        private const int RequiredFloorThickness = 2;
        private const int FindAttempts = 12;
        /// <summary>巡检间隔帧</summary>
        private const int ScanInterval = 600;
        /// <summary>成功后冷却帧</summary>
        private const int SuccessCooldown = 1800;
        /// <summary>寻位失败重试帧</summary>
        private const int RetryDelay = 90;

        private int cooldown;

        public override void OnWorldLoad() => cooldown = 240;
        public override void OnWorldUnload() => cooldown = 0;

        public override void PostUpdateEverything() {
            //主端驱动，客户端不参与
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }

            cooldown = TryScheduleSpawn() ? SuccessCooldown : ScanInterval;
        }

        /// <summary>成功生成传送门返回 true（进长冷却）</summary>
        public bool TryScheduleSpawn() {
            //传送门只负责首次登场，之后重生交给原版住房系统
            if (VictorWorldState.HasArrived) return false;
            if (Main.dayTime) return false;
            if (CWRWorld.HasBoss) return false;
            int victorType = ModContent.NPCType<Victor>();
            if (NPC.AnyNPCs(victorType)) return false;
            if (SubWorldRef.AnyActiveSubWorld()) return false;
            int portalType = ModContent.ProjectileType<VictorRiftPortalProj>();
            if (HasActivePortal(portalType)) return false;

            Player target = PickCandidatePlayer();
            if (target == null) return false;

            if (!TryFindSpawnPoint(target, out Vector2 spawnPos, out int facing)) {
                cooldown = RetryDelay;
                return false;
            }

            SpawnPortalAt(spawnPos, facing);
            return true;
        }

        private static bool HasActivePortal(int portalType) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == portalType) {
                    return true;
                }
            }
            return false;
        }

        private static Player PickCandidatePlayer() {
            Player best = null;
            foreach (Player p in Main.ActivePlayers) {
                if (p == null || !p.active || p.dead || p.ghost) continue;
                if (!IsConditionMet(p)) continue;
                if (p.Center.Y > Main.worldSurface * 16f + 1200f) continue;//过深跳过
                if (best == null || Math.Abs(p.velocity.X) < Math.Abs(best.velocity.X)) {
                    best = p;//偏好少动
                }
            }
            return best;
        }

        /// <summary>HackTime 解锁 或 已击败克眼</summary>
        public static bool IsConditionMet(Player p) {
            if (HackTimeAccess.CanUse(p)) return true;
            return NPC.downedBoss1;
        }

        private static bool TryFindSpawnPoint(Player player, out Vector2 spawnPos, out int facing) {
            spawnPos = Vector2.Zero;
            facing = 1;

            int px = (int)(player.Center.X / 16f);
            int py = (int)(player.Center.Y / 16f);

            for (int i = 0; i < FindAttempts; i++) {
                int side = Main.rand.NextBool() ? 1 : -1;
                int dist = Main.rand.Next(MinTileDistance, MaxTileDistance + 1);
                int tx = px + side * dist;

                int ty = py;
                if (!TryFindFloorY(tx, ty, out int floorY)) continue;

                if (!IsOpenAir(tx, floorY)) continue;
                if (!IsOpenAir(tx - 1, floorY)) continue;
                if (!IsOpenAir(tx + 1, floorY)) continue;

                if (HasBadLiquid(tx, floorY)) continue;

                if (tx < 60 || tx > Main.maxTilesX - 60) continue;
                if (floorY < 40 || floorY > Main.maxTilesY - 40) continue;

                //portal 下沿贴地面顶
                spawnPos = new Vector2(tx * 16f + 8f, floorY * 16f - VictorRiftPortalProj.BaseHalfHeight);
                facing = -side;//朝向玩家
                return true;
            }
            return false;
        }

        /// <summary>向下找可立人固体地面 Y</summary>
        private static bool TryFindFloorY(int tx, int startY, out int floorY) {
            floorY = -1;
            int limit = Math.Min(Main.maxTilesY - 5, startY + 40);
            for (int y = Math.Max(2, startY); y < limit; y++) {
                if (!IsSolidGround(tx, y)) continue;
                int thick = 0;
                for (int k = 0; k < RequiredFloorThickness; k++) {
                    if (IsSolidGround(tx, y + k)) thick++;
                }
                if (thick < RequiredFloorThickness) continue;
                if (IsSolidGround(tx, y - 1)) continue;
                floorY = y;
                return true;
            }
            return false;
        }

        /// <summary>实心 collider，排除平台</summary>
        private static bool IsSolidGround(int tx, int ty) {
            if (tx < 0 || ty < 0 || tx >= Main.maxTilesX || ty >= Main.maxTilesY) return false;
            Tile t = Main.tile[tx, ty];
            if (!t.HasTile) return false;
            if (!Main.tileSolid[t.TileType]) return false;
            //平台不算
            if (Main.tileSolidTop[t.TileType]) return false;
            return true;
        }

        /// <summary>floorY 上 RequiredHeadroom 格须空气</summary>
        private static bool IsOpenAir(int tx, int floorY) {
            for (int dy = 1; dy <= RequiredHeadroom; dy++) {
                int y = floorY - dy;
                if (y < 0) return false;
                Tile t = Main.tile[tx, y];
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>头顶有液即否（含水）</summary>
        private static bool HasBadLiquid(int tx, int floorY) {
            for (int dy = 0; dy <= RequiredHeadroom; dy++) {
                int y = floorY - dy;
                if (y < 0) break;
                Tile t = Main.tile[tx, y];
                if (t.LiquidAmount <= 0) continue;
                return true;
            }
            return false;
        }

        private static void SpawnPortalAt(Vector2 worldPos, int facing) {
            int type = ModContent.ProjectileType<VictorRiftPortalProj>();
            //owner=全局；ai0=facing ai1=-1 未绑 ai2=尺寸
            int idx = Projectile.NewProjectile(new EntitySource_WorldEvent(),
                worldPos, Vector2.Zero, type, 0, 0f, Main.myPlayer,
                facing, -1f, 0f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile pr = Main.projectile[idx];
                pr.netImportant = true;
                pr.netUpdate = true;
            }
        }
    }
}
