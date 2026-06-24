using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.OtherMods.SubWorld;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// Victor 自定义生成器：替代原版城镇 NPC 生成路径
    /// <br/>主端定期扫描候选玩家 → 在其旁边找开放地面 → 生成 <see cref="VictorRiftPortalProj"/>，由弹幕在演出中段创建 NPC
    /// </summary>
    internal class VictorPortalSpawner : ModSystem
    {
        /// <summary>距玩家的最小水平 tile 距离（避免直接砸到玩家头上）</summary>
        private const int MinTileDistance = 11;
        /// <summary>最大水平 tile 距离（仍然在玩家视野内）</summary>
        private const int MaxTileDistance = 17;
        /// <summary>地面上方必须留出的空气格数（要够 Victor + 传送门高度 ≈ 12 tile）</summary>
        private const int RequiredHeadroom = 9;
        /// <summary>地面下方至少要的连续固体 tile 数（避免单层悬空地板）</summary>
        private const int RequiredFloorThickness = 2;
        /// <summary>每次寻位尝试的随机次数</summary>
        private const int FindAttempts = 12;
        /// <summary>正常巡检间隔（帧）</summary>
        private const int ScanInterval = 600;
        /// <summary>生成成功后的冷却（帧）</summary>
        private const int SuccessCooldown = 1800;
        /// <summary>寻位失败时的快速重试（帧）</summary>
        private const int RetryDelay = 90;

        private int cooldown;

        public override void OnWorldLoad() => cooldown = 240;
        public override void OnWorldUnload() => cooldown = 0;

        public override void PostUpdateEverything() {
            //主端驱动（单机/服务器/主机），客户端不参与生成决策
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }

            //结果决定下一次再次扫描的间隔
            cooldown = TryScheduleSpawn() ? SuccessCooldown : ScanInterval;
        }

        /// <summary>尝试一次生成；返回 true 表示已生成传送门并应进入长冷却</summary>
        public bool TryScheduleSpawn() {
            if (Main.dayTime) return false;
            if (CWRWorld.HasBoss) return false;
            //世界里已经有 Victor（或他的传送门）就不再生成
            int victorType = ModContent.NPCType<Victor>();
            if (NPC.AnyNPCs(victorType)) return false;
            if (SubWorldRef.AnyActiveSubWorld()) return false;
            int portalType = ModContent.ProjectileType<VictorRiftPortalProj>();
            if (HasActivePortal(portalType)) return false;

            //找一个满足条件的玩家
            Player target = PickCandidatePlayer();
            if (target == null) return false;

            //找位置
            if (!TryFindSpawnPoint(target, out Vector2 spawnPos, out int facing)) {
                cooldown = RetryDelay;
                return false;
            }

            SpawnPortalAt(spawnPos, facing);
            return true;
        }

        /// <summary>世界内已存在 Victor 传送门弹幕？</summary>
        private static bool HasActivePortal(int portalType) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == portalType) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>挑一个满足出场条件、地表(且非死亡)的玩家</summary>
        private static Player PickCandidatePlayer() {
            Player best = null;
            foreach (Player p in Main.ActivePlayers) {
                if (p == null || !p.active || p.dead || p.ghost) continue;
                if (!IsConditionMet(p)) continue;
                //优先非空中、非液体、地表 Y 坐标合理的玩家
                if (p.Center.Y > Main.worldSurface * 16f + 1200f) continue;//不在过深的地下生成
                if (best == null || Math.Abs(p.velocity.X) < Math.Abs(best.velocity.X)) {
                    best = p;//简单偏好不太移动的玩家
                }
            }
            return best;
        }

        /// <summary>条件：HackTime 已解锁的玩家 OR 已击败克苏鲁之眼</summary>
        public static bool IsConditionMet(Player p) {
            if (HackTimeAccess.CanUse(p)) return true;
            return NPC.downedBoss1;
        }

        /// <summary>在玩家旁随机找一处开放地面，返回世界坐标 + Victor 出场方向</summary>
        private static bool TryFindSpawnPoint(Player player, out Vector2 spawnPos, out int facing) {
            spawnPos = Vector2.Zero;
            facing = 1;

            int px = (int)(player.Center.X / 16f);
            int py = (int)(player.Center.Y / 16f);

            for (int i = 0; i < FindAttempts; i++) {
                //随机左右 + 随机距离
                int side = Main.rand.NextBool() ? 1 : -1;
                int dist = Main.rand.Next(MinTileDistance, MaxTileDistance + 1);
                int tx = px + side * dist;

                //y 起点取玩家脚下附近，向下扫到第一块固体
                int ty = py;
                if (!TryFindFloorY(tx, ty, out int floorY)) continue;

                //开放空间检查（NPC 头顶要够高，且左右两格也通畅）
                if (!IsOpenAir(tx, floorY)) continue;
                if (!IsOpenAir(tx - 1, floorY)) continue;
                if (!IsOpenAir(tx + 1, floorY)) continue;

                //避免液体（脚下方块所在格的液体量 > 0 也不行）
                if (HasBadLiquid(tx, floorY)) continue;

                //世界边界
                if (tx < 60 || tx > Main.maxTilesX - 60) continue;
                if (floorY < 40 || floorY > Main.maxTilesY - 40) continue;

                //合格：portal 下沿（中心 + halfH）对齐地面顶面，Victor 脚部恰好踏在地面
                spawnPos = new Vector2(tx * 16f + 8f, floorY * 16f - VictorRiftPortalProj.BaseHalfHeight);
                facing = -side;//让 Victor 朝向玩家
                return true;
            }
            return false;
        }

        /// <summary>从 (tx, startY) 向下扫描，找到第一格上表面可立人的固体地面 Y</summary>
        private static bool TryFindFloorY(int tx, int startY, out int floorY) {
            floorY = -1;
            int limit = Math.Min(Main.maxTilesY - 5, startY + 40);
            for (int y = Math.Max(2, startY); y < limit; y++) {
                if (!IsSolidGround(tx, y)) continue;
                //地面下方要有足够厚度
                int thick = 0;
                for (int k = 0; k < RequiredFloorThickness; k++) {
                    if (IsSolidGround(tx, y + k)) thick++;
                }
                if (thick < RequiredFloorThickness) continue;
                //上一格必须是空气
                if (IsSolidGround(tx, y - 1)) continue;
                floorY = y;
                return true;
            }
            return false;
        }

        /// <summary>tile 是带 collider 的实心物块（排除平台、活动门等）</summary>
        private static bool IsSolidGround(int tx, int ty) {
            if (tx < 0 || ty < 0 || tx >= Main.maxTilesX || ty >= Main.maxTilesY) return false;
            Tile t = Main.tile[tx, ty];
            if (!t.HasTile) return false;
            if (!Main.tileSolid[t.TileType]) return false;
            //平台不算地面，避免传送门挂在平台上
            if (Main.tileSolidTop[t.TileType]) return false;
            return true;
        }

        /// <summary>地面正上方 <paramref name="floorY"/> -1 起，向上 RequiredHeadroom 格内必须全是空气</summary>
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

        /// <summary>地面上方区域是否落入熔岩/蜂蜜（水勉强允许）</summary>
        private static bool HasBadLiquid(int tx, int floorY) {
            for (int dy = 0; dy <= RequiredHeadroom; dy++) {
                int y = floorY - dy;
                if (y < 0) break;
                Tile t = Main.tile[tx, y];
                if (t.LiquidAmount <= 0) continue;
                //熔岩(1)/蜂蜜(2) 直接排除；水也尽量避免（NPC 容易溺水）
                return true;
            }
            return false;
        }

        /// <summary>主端生成传送门弹幕，自动同步到客户端</summary>
        private static void SpawnPortalAt(Vector2 worldPos, int facing) {
            int type = ModContent.ProjectileType<VictorRiftPortalProj>();
            //owner 256 = 全局；ai[0]=facing ai[1]=-1 (尚未绑定) ai[2]=0(默认尺寸)
            //SyncProjectile 由 NewProjectile 自动发出
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
