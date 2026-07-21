using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>出生点附近找平整干燥、上方开阔的地面</summary>
    internal static class ToriiShrineLocationFinder
    {
        //横向搜索格数，避开复活落点与初始建筑
        private const int MinOffsetX = 14;
        private const int MaxOffsetX = 160;
        //上方净空格数
        private const int RequiredClearance = 18;
        //平整度采样半径(格)
        private const int FlatSampleRadius = 8;
        //相邻列允许高差(格)
        private const int MaxGroundDeviation = 3;

        /// <summary>最佳地面锚点像素坐标，找不到返回null</summary>
        public static Vector2? FindBestLocation() {
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

        /// <summary>向下吸附最近实心地面，调试重建等复用</summary>
        public static bool TrySnapToGround(Vector2 worldPos, out Vector2 groundPos) {
            int tileX = (int)(worldPos.X / 16f);
            int startY = Math.Max(20, (int)(worldPos.Y / 16f) - 30);

            for (int y = startY; y < Main.maxTilesY - 40; y++) {
                if (!WorldGen.InWorld(tileX, y)) {
                    continue;
                }
                if (Main.tile[tileX, y].HasSolidTile()) {
                    groundPos = new Vector2(tileX * 16f + 8f, y * 16f);
                    return true;
                }
            }

            groundPos = default;
            return false;
        }

        /// <summary>评估一列净空/液体/危险块/平整度，通过则给锚点与分数</summary>
        private static bool TryEvaluateColumn(int tileX, out Vector2 position, out int score) {
            position = default;
            score = 0;

            if (!TryFindSurfaceGround(tileX, out int groundY)) {
                return false;
            }

            //须在地表附近，不接受洞穴；出生点本身在地下的世界按出生高度放行
            if (groundY > Main.worldSurface + 40 && Math.Abs(groundY - Main.spawnTileY) > 60) {
                return false;
            }

            Tile ground = Main.tile[tileX, groundY];
            if (Main.tileDungeon[ground.TileType] || Main.tileLavaDeath[ground.TileType]) {
                return false;
            }

            //上方净空，无实心/液体
            for (int y = groundY - 1; y >= groundY - RequiredClearance; y--) {
                if (y < 0) {
                    return false;
                }
                Tile tile = Main.tile[tileX, y];
                if (tile.HasSolidTile() || tile.LiquidAmount > 0) {
                    return false;
                }
            }

            //两侧地面贴近中心列
            int totalDeviation = 0;
            for (int offsetX = -FlatSampleRadius; offsetX <= FlatSampleRadius; offsetX++) {
                if (offsetX == 0) {
                    continue;
                }
                if (!TryFindSurfaceGround(tileX + offsetX, out int sideGroundY)) {
                    return false;
                }
                int deviation = Math.Abs(sideGroundY - groundY);
                if (deviation > MaxGroundDeviation) {
                    return false;
                }
                totalDeviation += deviation;
            }

            position = new Vector2(tileX * 16f + 8f, groundY * 16f);
            score = 100 - totalDeviation * 6;
            return true;
        }

        /// <summary>出生高度附近向下扫到第一块实心地面</summary>
        private static bool TryFindSurfaceGround(int tileX, out int groundY) {
            groundY = 0;
            if (tileX < 40 || tileX >= Main.maxTilesX - 40) {
                return false;
            }

            int startY = Math.Max(40, Main.spawnTileY - 120);
            int endY = Math.Min(Main.maxTilesY - 40, Main.spawnTileY + 100);

            for (int y = startY; y < endY; y++) {
                if (Main.tile[tileX, y].HasSolidTile()) {
                    groundY = y;
                    return true;
                }
            }

            return false;
        }
    }
}
