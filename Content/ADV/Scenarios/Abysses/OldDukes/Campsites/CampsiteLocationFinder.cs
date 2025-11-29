using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 营地位置查找器，负责搜索和验证营地生成位置
    /// </summary>
    internal static class CampsiteLocationFinder
    {
        /// <summary>
        /// 寻找最佳营地位置
        /// </summary>
        public static Vector2? FindBestLocation() {
            //第一阶段：在世界右侧海岸区域搜索
            Vector2? position = SearchInCoastalArea();
            if (position.HasValue) {
                return position;
            }

            //第二阶段：扩大搜索范围
            position = SearchInExtendedArea();
            if (position.HasValue) {
                return position;
            }

            //第三阶段：在玩家附近寻找
            position = SearchNearPlayer();
            if (position.HasValue) {
                return position;
            }

            return null;
        }

        /// <summary>
        /// 在海岸区域搜索最佳位置
        /// </summary>
        private static Vector2? SearchInCoastalArea() {
            int searchStartX = Main.maxTilesX - 400;
            int searchEndX = Main.maxTilesX - 150;
            int startY = (int)(Main.worldSurface * 0.4f);
            int endY = (int)(Main.worldSurface * 1.1f);

            return SearchWithScoring(searchStartX, searchEndX, startY, endY, stepX: 20);
        }

        /// <summary>
        /// 在扩展区域搜索
        /// </summary>
        private static Vector2? SearchInExtendedArea() {
            int searchStartX = Main.maxTilesX - 500;
            int searchEndX = Main.maxTilesX - 150;
            int startY = (int)(Main.worldSurface * 0.4f);
            int endY = (int)(Main.worldSurface * 1.1f);

            return SearchWithScoring(searchStartX, searchEndX, startY, endY, stepX: 10);
        }

        /// <summary>
        /// 在玩家附近搜索
        /// </summary>
        private static Vector2? SearchNearPlayer() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return null;
            }

            int playerTileX = (int)(player.Center.X / 16);
            int playerTileY = (int)(player.Center.Y / 16);

            for (int offsetX = 0; offsetX < 100; offsetX += 10) {
                for (int offsetY = -50; offsetY < 50; offsetY += 5) {
                    int checkX = playerTileX + offsetX;
                    int checkY = playerTileY + offsetY;
                    Vector2? candidatePos = ValidateLocation(checkX, checkY);
                    if (candidatePos.HasValue) {
                        return candidatePos;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 使用评分系统搜索最佳位置
        /// </summary>
        private static Vector2? SearchWithScoring(int startX, int endX, int startY, int endY, int stepX) {
            Vector2? bestPosition = null;
            int bestScore = -1;

            for (int searchX = startX; searchX < endX; searchX += stepX) {
                for (int y = startY; y < endY; y++) {
                    Vector2? candidatePos = ValidateLocation(searchX, y);
                    if (candidatePos.HasValue) {
                        int score = EvaluateLocation(searchX, y);
                        if (score > bestScore) {
                            bestScore = score;
                            bestPosition = candidatePos;
                        }
                    }
                }
            }

            return bestPosition;
        }

        /// <summary>
        /// 验证位置是否适合生成营地
        /// </summary>
        public static Vector2? ValidateLocation(int tileX, int tileY) {
            //边界检查
            if (!IsWithinBounds(tileX, tileY)) {
                return null;
            }

            Tile tile = Main.tile[tileX, tileY];
            if (tile == null) {
                return null;
            }

            //必须是实心地面
            if (!tile.HasTile || !Main.tileSolid[tile.TileType]) {
                return null;
            }

            //不能是危险的方块类型
            if (IsDangerousTile(tile)) {
                return null;
            }

            //水域检查
            if (IsUnderwater(tileX, tileY)) {
                return null;
            }

            //空间检查
            if (!HasVerticalSpace(tileX, tileY, 10)) {
                return null;
            }

            if (!HasHorizontalSpace(tileX, tileY, 5)) {
                return null;
            }

            //水包围检查
            if (IsSurroundedByWater(tileX, tileY)) {
                return null;
            }

            //海洋区域特殊检查
            if (IsInOceanBiome(tileX, tileY) && tileY > Main.worldSurface * 0.8f) {
                return null;
            }

            //通过所有检查，返回像素坐标
            return new Vector2(
                tileX * 16 + 8,
                tileY * 16 - 48
            );
        }

        #region 位置验证辅助方法

        /// <summary>
        /// 检查是否在有效边界内
        /// </summary>
        private static bool IsWithinBounds(int tileX, int tileY) {
            return tileX >= 50 && tileX < Main.maxTilesX - 50 &&
                   tileY >= 50 && tileY < Main.maxTilesY - 50;
        }

        /// <summary>
        /// 检查是否是危险方块
        /// </summary>
        private static bool IsDangerousTile(Tile tile) {
            return Main.tileDungeon[tile.TileType] || Main.tileLavaDeath[tile.TileType];
        }

        /// <summary>
        /// 检查是否在水下
        /// </summary>
        private static bool IsUnderwater(int tileX, int tileY) {
            for (int checkY = tileY - 3; checkY <= tileY; checkY++) {
                if (checkY < 0 || checkY >= Main.maxTilesY) {
                    continue;
                }

                Tile checkTile = Main.tile[tileX, checkY];
                if (checkTile != null && checkTile.LiquidAmount > 0 && checkTile.LiquidType == LiquidID.Water) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查垂直方向是否有足够空间
        /// </summary>
        private static bool HasVerticalSpace(int tileX, int tileY, int requiredHeight) {
            for (int checkY = tileY - 1; checkY >= tileY - requiredHeight; checkY--) {
                if (checkY < 0) {
                    return false;
                }

                Tile checkTile = Main.tile[tileX, checkY];
                if (checkTile == null) {
                    continue;
                }

                if (checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                    return false;
                }

                if (checkTile.LiquidAmount > 0 && checkTile.LiquidType == LiquidID.Water) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 检查水平方向是否有足够空间
        /// </summary>
        private static bool HasHorizontalSpace(int tileX, int tileY, int requiredWidth) {
            for (int offsetX = -requiredWidth; offsetX <= requiredWidth; offsetX++) {
                int checkX = tileX + offsetX;
                if (checkX < 0 || checkX >= Main.maxTilesX) {
                    return false;
                }

                bool foundSolid = false;
                for (int offsetY = -2; offsetY <= 2; offsetY++) {
                    int checkY = tileY + offsetY;
                    if (checkY < 0 || checkY >= Main.maxTilesY) {
                        continue;
                    }

                    Tile checkTile = Main.tile[checkX, checkY];
                    if (checkTile != null && checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                        foundSolid = true;
                        break;
                    }
                }

                if (!foundSolid) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 检查是否被水包围
        /// </summary>
        private static bool IsSurroundedByWater(int tileX, int tileY) {
            int waterCount = 0;
            int totalChecks = 0;
            const int checkRadius = 15;

            for (int offsetX = -checkRadius; offsetX <= checkRadius; offsetX += 3) {
                for (int offsetY = -checkRadius; offsetY <= checkRadius; offsetY += 3) {
                    int checkX = tileX + offsetX;
                    int checkY = tileY + offsetY;

                    if (checkX < 0 || checkX >= Main.maxTilesX || checkY < 0 || checkY >= Main.maxTilesY) {
                        continue;
                    }

                    totalChecks++;
                    Tile checkTile = Main.tile[checkX, checkY];
                    if (checkTile != null && checkTile.LiquidAmount > 0 && checkTile.LiquidType == LiquidID.Water) {
                        waterCount++;
                    }
                }
            }

            return totalChecks > 0 && (float)waterCount / totalChecks > 0.4f;
        }

        /// <summary>
        /// 检查是否在海洋生物群系
        /// </summary>
        private static bool IsInOceanBiome(int tileX, int tileY) {
            const int oceanThreshold = 380;
            return tileX < oceanThreshold || tileX > Main.maxTilesX - oceanThreshold;
        }

        #endregion

        #region 位置评分系统

        /// <summary>
        /// 评估位置质量，返回分数（越高越好）
        /// </summary>
        private static int EvaluateLocation(int tileX, int tileY) {
            int score = 100;

            //地表距离评分
            score += ScoreBySurfaceDistance(tileY);

            //地面类型评分
            score += ScoreByTileType(tileX, tileY);

            //周围水量评分
            score += ScoreByNearbyWater(tileX, tileY);

            return score;
        }

        /// <summary>
        /// 根据与地表的距离评分
        /// </summary>
        private static int ScoreBySurfaceDistance(int tileY) {
            float surfaceDistance = (float)Math.Abs(tileY - Main.worldSurface);
            return -(int)(surfaceDistance * 0.5f);
        }

        /// <summary>
        /// 根据地面类型评分
        /// </summary>
        private static int ScoreByTileType(int tileX, int tileY) {
            Tile tile = Main.tile[tileX, tileY];
            if (tile == null || !tile.HasTile) {
                return 0;
            }

            if (tile.TileType == TileID.Grass || tile.TileType == TileID.Sand) {
                return 50;
            }

            if (tile.TileType == TileID.Dirt || tile.TileType == TileID.Stone) {
                return 20;
            }

            return 0;
        }

        /// <summary>
        /// 根据周围水量评分（水越少越好）
        /// </summary>
        private static int ScoreByNearbyWater(int tileX, int tileY) {
            int waterCount = 0;

            for (int offsetX = -10; offsetX <= 10; offsetX += 2) {
                for (int offsetY = -5; offsetY <= 5; offsetY += 2) {
                    int checkX = tileX + offsetX;
                    int checkY = tileY + offsetY;

                    if (checkX >= 0 && checkX < Main.maxTilesX && checkY >= 0 && checkY < Main.maxTilesY) {
                        Tile checkTile = Main.tile[checkX, checkY];
                        if (checkTile != null && checkTile.LiquidAmount > 0) {
                            waterCount++;
                        }
                    }
                }
            }

            return -waterCount * 2;
        }

        #endregion
    }
}
