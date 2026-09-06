using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>
    /// 刀縁的连续态账本（owner 端自治，不进存档不进网络）。<br/>
    /// 「静止多久」「离地多久」这类条件不适合每帧塞进事件参数，
    /// 统一由本账本按帧维护，縁只从这里读数
    /// </summary>
    internal sealed class OniMeiDeedTracker
    {
        /// <summary>与止足同一判据的低位移阈（速度平方）</summary>
        private const float StillSpeedSq = 2.25f;
        /// <summary>视作交战的近敌半径</summary>
        private const float FightRadius = 420f;
        /// <summary>无近敌超过此帧数即脱战，处决记忆随之作废</summary>
        private const int DisengageTicks = 240;

        //====静止（鬼丸）====
        /// <summary>持刀低位移连续帧</summary>
        internal int StillTicks { get; private set; }
        /// <summary>本段静止期内是否挨过敌手一记</summary>
        internal bool HurtWhileStill { get; private set; }

        //====滞空（空樋）====
        /// <summary>持刀离地连续帧</summary>
        internal int AirborneTicks { get; private set; }
        /// <summary>本段离地期内的直接刀击命中数</summary>
        internal int AirborneHits { get; private set; }

        //====雨中樱流（雨樋）====
        /// <summary>雨天樱流巡航累计帧，跨航程累计，满一秒归零并脉冲一次</summary>
        private int sakuraRainCarry;
        /// <summary>本帧恰好累满一整秒雨程（单帧脉冲，雨樋据此按秒推进）</summary>
        internal bool SakuraRainSecondTick { get; private set; }

        //====立定苦战（枯山水）====
        /// <summary>钉在原地且未受伤的交战连续帧</summary>
        internal int PlantedFightTicks { get; private set; }
        private Vector2 plantedAnchor;
        private bool plantedAnchored;

        //====本场处决记忆（梵鐘）====
        /// <summary>本场交战里是否动用过灭世或终结</summary>
        internal bool ExecutionUsedInFight { get; private set; }
        private int disengageTicks;

        //====了结去重：同一主体的死只报一次====
        private int lastKillRootId = -1;
        private int lastKillRootType = -1;
        private ulong lastKillTick;

        /// <summary>
        /// 认领一次了结。同一主体在短窗内的重复上报（副斩与主刀同帧）只放行第一次
        /// </summary>
        internal bool TryClaimKill(int rootId, int rootType) {
            ulong now = Main.GameUpdateCount;
            if (rootId == lastKillRootId && rootType == lastKillRootType && now - lastKillTick <= 30) {
                return false;
            }
            lastKillRootId = rootId;
            lastKillRootType = rootType;
            lastKillTick = now;
            return true;
        }

        /// <summary>持刀逐帧推进（缩放帧）；不持刀一律清账</summary>
        internal void Tick(Player player, bool holding) {
            if (player == null || !holding || player.dead) {
                ResetVolatile();
                return;
            }

            bool still = player.velocity.LengthSquared() <= StillSpeedSq;
            if (still) {
                StillTicks++;
            }
            else {
                StillTicks = 0;
                HurtWhileStill = false;
            }

            bool grounded = player.velocity.Y == 0f || player.sliding
                || player.mount?.Active == true || player.grappling[0] >= 0;
            if (grounded) {
                AirborneTicks = 0;
                AirborneHits = 0;
            }
            else {
                AirborneTicks++;
            }

            TickPlantedFight(player, still);
            TickDisengage(player);
        }

        /// <summary>
        /// 樱流巡航帧（由樱流经济层调用）。单次航程被 <c>OniSakuraFlight.MaxFlightFrames</c> 钳在 3 秒内，
        /// 雨程只能跨航程分段累计；不下雨的帧不计也不清
        /// </summary>
        internal void TickSakuraFlight(bool raining) {
            SakuraRainSecondTick = false;
            if (!raining) {
                return;
            }
            if (++sakuraRainCarry >= 60) {
                sakuraRainCarry = 0;
                SakuraRainSecondTick = true;
            }
        }

        /// <summary>受伤：断掉静止与立定两条连续条件，但静止段本身记一笔"挨过打"</summary>
        internal void NotifyHurt() {
            if (StillTicks > 0) {
                HurtWhileStill = true;
            }
            PlantedFightTicks = 0;
            plantedAnchored = false;
        }

        /// <summary>直接刀击命中：只有离地期的命中计入滞空账</summary>
        internal void NotifyBladeHit() {
            if (AirborneTicks > 0) {
                AirborneHits++;
            }
        }

        /// <summary>灭世/终结出手：本场交战从此不再算"全程未处决"</summary>
        internal void NotifyExecutionSpent() {
            ExecutionUsedInFight = true;
            disengageTicks = 0;
        }

        private void TickPlantedFight(Player player, bool still) {
            if (!still || !HasNearbyHostile(player)) {
                PlantedFightTicks = 0;
                plantedAnchored = false;
                return;
            }
            if (!plantedAnchored) {
                plantedAnchored = true;
                plantedAnchor = player.Center;
                PlantedFightTicks = 0;
            }
            //位移超过两格即视为换了位置，重新起算
            if (Vector2.DistanceSquared(player.Center, plantedAnchor) > 32f * 32f) {
                plantedAnchor = player.Center;
                PlantedFightTicks = 0;
                return;
            }
            PlantedFightTicks++;
        }

        private void TickDisengage(Player player) {
            if (!ExecutionUsedInFight) {
                return;
            }
            if (HasNearbyHostile(player)) {
                disengageTicks = 0;
                return;
            }
            if (++disengageTicks >= DisengageTicks) {
                ExecutionUsedInFight = false;
                disengageTicks = 0;
            }
        }

        private static bool HasNearbyHostile(Player player) {
            float radiusSq = FightRadius * FightRadius;
            Vector2 center = player.Center;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.damage <= 0 && !npc.boss) {
                    continue;
                }
                if (npc.DistanceSQ(center) <= radiusSq) {
                    return true;
                }
            }
            return false;
        }

        private void ResetVolatile() {
            StillTicks = 0;
            HurtWhileStill = false;
            AirborneTicks = 0;
            AirborneHits = 0;
            sakuraRainCarry = 0;
            SakuraRainSecondTick = false;
            PlantedFightTicks = 0;
            plantedAnchored = false;
        }

        internal void Reset() {
            ResetVolatile();
            ExecutionUsedInFight = false;
            disengageTicks = 0;
            lastKillRootId = -1;
            lastKillRootType = -1;
            lastKillTick = 0;
        }
    }

    /// <summary>刀縁用得上的环境判据；鬼切本体此前不读任何天候/时刻，集中在此以免散落</summary>
    internal static class OniMeiDeedEnvironment
    {
        /// <summary>雷暴：与斩雷多道同一判据，刀縁解锁时的天和解锁后能落多道雷的天是同一个天</summary>
        internal static bool IsStorming => OniMeiCombat.IsStorming;

        /// <summary>
        /// 头顶通天：地表线以上，且探顶口径与雷柱落雷（<c>OniMeiThunderColumn.TryProbeSky</c>）一致，
        /// 只有实心非平台砖算遮挡，背景墙不算。雷能落下来的地方就是雷切认的露天
        /// </summary>
        internal static bool HasOpenSky(Player player) {
            if (player == null || player.position.Y >= Main.worldSurface * 16.0) {
                return false;
            }
            Point tile = player.Top.ToTileCoordinates();
            int limit = Math.Max(0, tile.Y - OniMeiCombat.ThunderSkyProbeTiles);
            for (int y = tile.Y - 1; y >= limit; y--) {
                if (!WorldGen.InWorld(tile.X, y, 1)) {
                    break;
                }
                Tile probe = Framing.GetTileSafely(tile.X, y);
                if (probe.HasTile && Main.tileSolid[probe.TileType] && !Main.tileSolidTop[probe.TileType]) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>离地高度（像素）：向下探到实心地面，探不到按满程算</summary>
        internal static float HeightAboveGround(Player player, float maxProbe = 1200f) {
            if (player == null) {
                return 0f;
            }
            Vector2 foot = player.Bottom;
            Point tile = foot.ToTileCoordinates();
            int steps = (int)(maxProbe / 16f);
            for (int i = 0; i <= steps; i++) {
                int y = tile.Y + i;
                if (!WorldGen.InWorld(tile.X, y, 1)) {
                    break;
                }
                Tile probe = Framing.GetTileSafely(tile.X, y);
                if (probe.HasTile && Main.tileSolid[probe.TileType]) {
                    return Math.Max(0f, y * 16f - foot.Y);
                }
            }
            return maxProbe;
        }

        /// <summary>飞行体：无重力或长期悬空的敌手</summary>
        internal static bool IsFlyer(NPC npc)
            => npc != null && (npc.noGravity || npc.noTileCollide && npc.velocity.Y != 0f);
    }
}
