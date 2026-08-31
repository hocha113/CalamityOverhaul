using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>鸟居选址层级，越靠后约束越宽松，但始终保证世界坐标有效</summary>
    internal enum ToriiShrinePlacementTier
    {
        StrictTerrain,
        SpawnGround,
        SpawnPoint,
        WorldSurface
    }

    /// <summary>出生点附近寻找平整干燥、上方开阔的地面，并提供可靠的位置兜底</summary>
    internal static class ToriiShrineLocationFinder
    {
        private const int WorldEdgeMargin = 40;
        //横向搜索格数，避开复活落点与初始建筑
        private const int MinOffsetX = 14;
        private const int MaxOffsetX = 160;
        //上方净空格数
        private const int RequiredClearance = 18;
        //鸟居约18格宽，按半宽检查整片落地区域
        private const int FlatSampleRadius = 9;
        //相邻列允许高差(格)
        private const int MaxGroundDeviation = 3;
        //出生地面上方允许的自然抬升(格)，再高的地表进入空岛甄别
        private const int MaxRiseAboveSpawn = 45;
        //悬空判定：实心体厚度不足此值且下方为空气，视为空岛或浮空建筑
        private const int MaxFloatingMassThickness = 40;

        /// <summary>世界尺寸是否足以安全解析鸟居位置</summary>
        public static bool WorldGeometryReady
            => Main.maxTilesX > WorldEdgeMargin * 2 && Main.maxTilesY > WorldEdgeMargin * 2;

        /// <summary>
        /// 解析保证有效的锚点：严格地形 → 出生点地面 → 出生点原位 → 世界地表中心。
        /// 世界数据尚未就绪时返回false，由调用方下一帧重试。
        /// </summary>
        public static bool TryResolveGuaranteedLocation(out Vector2 position, out ToriiShrinePlacementTier tier) {
            position = default;
            tier = ToriiShrinePlacementTier.StrictTerrain;
            if (!WorldGeometryReady) {
                return false;
            }

            Vector2? bestPosition = FindBestLocation();
            if (bestPosition.HasValue && IsValidWorldPosition(bestPosition.Value)) {
                position = bestPosition.Value;
                return true;
            }

            if (TryGetSpawnWorldPosition(out Vector2 spawnPosition)) {
                if (TrySnapToGround(spawnPosition, out Vector2 snappedPosition)) {
                    position = snappedPosition;
                    tier = ToriiShrinePlacementTier.SpawnGround;
                    return true;
                }

                //终极出生点兜底允许无视地形，但不允许无效出生点或世界原点。
                position = spawnPosition;
                tier = ToriiShrinePlacementTier.SpawnPoint;
                return true;
            }

            int tileX = Math.Clamp(Main.maxTilesX / 2, WorldEdgeMargin, Main.maxTilesX - WorldEdgeMargin);
            int preferredY = double.IsFinite(Main.worldSurface)
                ? (int)Math.Round(Main.worldSurface)
                : Main.maxTilesY / 3;
            int tileY = Math.Clamp(preferredY, WorldEdgeMargin, Main.maxTilesY - WorldEdgeMargin);
            position = new Vector2(tileX * 16f + 8f, tileY * 16f);
            tier = ToriiShrinePlacementTier.WorldSurface;
            return IsValidWorldPosition(position);
        }

        /// <summary>有限且位于世界内部安全区域的像素坐标</summary>
        public static bool IsValidWorldPosition(Vector2 position) {
            if (!WorldGeometryReady || !float.IsFinite(position.X) || !float.IsFinite(position.Y)) {
                return false;
            }

            const float TileSize = 16f;
            float margin = WorldEdgeMargin * TileSize;
            return position.X >= margin && position.X <= Main.maxTilesX * TileSize - margin
                && position.Y >= margin && position.Y <= Main.maxTilesY * TileSize - margin;
        }

        /// <summary>最佳地面锚点像素坐标，找不到返回null</summary>
        public static Vector2? FindBestLocation() {
            if (!WorldGeometryReady) {
                return null;
            }

            int spawnX = Main.spawnTileX;
            Vector2? bestPosition = null;
            int bestScore = int.MinValue;

            for (int offset = MinOffsetX; offset <= MaxOffsetX; offset += 4) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    int x = spawnX + offset * dir;
                    if (!TryEvaluateColumn(x, out Vector2 position, out int score)) {
                        continue;
                    }

                    //越近出生点越好
                    score -= offset;
                    if (score > bestScore) {
                        bestScore = score;
                        bestPosition = position;
                    }
                }
            }

            return bestPosition;
        }

        /// <summary>向下吸附最近实心地面，调试重建与出生点兜底复用</summary>
        public static bool TrySnapToGround(Vector2 worldPos, out Vector2 groundPos) {
            groundPos = default;
            if (!WorldGeometryReady || !float.IsFinite(worldPos.X) || !float.IsFinite(worldPos.Y)) {
                return false;
            }

            int tileX = (int)(worldPos.X / 16f);
            if (tileX < WorldEdgeMargin || tileX >= Main.maxTilesX - WorldEdgeMargin) {
                return false;
            }

            int startY = Math.Clamp((int)(worldPos.Y / 16f) - 30,
                WorldEdgeMargin, Main.maxTilesY - WorldEdgeMargin);
            for (int y = startY; y < Main.maxTilesY - WorldEdgeMargin; y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile != null && tile.HasSolidTile()) {
                    Vector2 candidate = new(tileX * 16f + 8f, y * 16f);
                    if (IsValidWorldPosition(candidate)) {
                        groundPos = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>评估完整鸟居占地区域的净空、液体、危险块与平整度</summary>
        private static bool TryEvaluateColumn(int tileX, out Vector2 position, out int score) {
            position = default;
            score = 0;

            if (!TryFindSurfaceGround(tileX, out int groundY)) {
                return false;
            }

            //须在地表附近，不接受洞穴；出生点本身在地下的世界按出生高度放行。
            if (groundY > Main.worldSurface + 40 && Math.Abs(groundY - Main.spawnTileY) > 60) {
                return false;
            }

            int totalDeviation = 0;
            for (int offsetX = -FlatSampleRadius; offsetX <= FlatSampleRadius; offsetX++) {
                int sampleX = tileX + offsetX;
                if (!TryFindSurfaceGround(sampleX, out int sideGroundY)) {
                    return false;
                }

                int deviation = Math.Abs(sideGroundY - groundY);
                if (deviation > MaxGroundDeviation) {
                    return false;
                }
                totalDeviation += deviation;

                Tile ground = Main.tile[sampleX, sideGroundY];
                if (ground == null || Main.tileDungeon[ground.TileType] || Main.tileLavaDeath[ground.TileType]) {
                    return false;
                }

                for (int y = sideGroundY - 1; y >= sideGroundY - RequiredClearance; y--) {
                    if (y < WorldEdgeMargin) {
                        return false;
                    }
                    Tile tile = Main.tile[sampleX, y];
                    if (tile == null || tile.HasSolidTile() || tile.LiquidAmount > 0) {
                        return false;
                    }
                }
            }

            position = new Vector2(tileX * 16f + 8f, groundY * 16f);
            if (!IsValidWorldPosition(position)) {
                return false;
            }

            score = 100 - totalDeviation * 6;
            return true;
        }

        /// <summary>
        /// 出生高度附近向下扫描地表。明显高于出生地面的命中先做悬空甄别：
        /// 薄悬空体（空岛、浮空建筑）跳过后继续向下找玩家同层的真实地面；
        /// 厚实体视为相连的高地形，仍算真实地面。
        /// </summary>
        private static bool TryFindSurfaceGround(int tileX, out int groundY) {
            groundY = 0;
            if (tileX < WorldEdgeMargin || tileX >= Main.maxTilesX - WorldEdgeMargin) {
                return false;
            }

            int startY = Math.Max(WorldEdgeMargin, Main.spawnTileY - 120);
            int endY = Math.Min(Main.maxTilesY - WorldEdgeMargin, Main.spawnTileY + 100);
            //高于此线的地表按疑似高空处理
            int suspectCeiling = Main.spawnTileY - MaxRiseAboveSpawn;

            int y = startY;
            while (y < endY) {
                Tile tile = Main.tile[tileX, y];
                if (tile == null || !tile.HasSolidTile()) {
                    y++;
                    continue;
                }

                if (y >= suspectCeiling) {
                    groundY = y;
                    return true;
                }

                //疑似高空地表：探明实心体厚度再定性
                int massBottom = y;
                while (massBottom < endY && Main.tile[tileX, massBottom] != null
                    && Main.tile[tileX, massBottom].HasSolidTile()) {
                    massBottom++;
                }

                if (massBottom - y >= MaxFloatingMassThickness) {
                    groundY = y;
                    return true;
                }

                //薄悬空体，跳过后继续向下
                y = massBottom;
            }

            return false;
        }

        private static bool TryGetSpawnWorldPosition(out Vector2 position) {
            position = default;
            if (Main.spawnTileX < WorldEdgeMargin || Main.spawnTileX >= Main.maxTilesX - WorldEdgeMargin
                || Main.spawnTileY < WorldEdgeMargin || Main.spawnTileY >= Main.maxTilesY - WorldEdgeMargin) {
                return false;
            }

            Vector2 candidate = new(Main.spawnTileX * 16f + 8f, Main.spawnTileY * 16f);
            if (!IsValidWorldPosition(candidate)) {
                return false;
            }

            position = candidate;
            return true;
        }
    }
}
