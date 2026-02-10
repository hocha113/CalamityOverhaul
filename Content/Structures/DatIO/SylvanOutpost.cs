using CalamityOverhaul.Content.UIs.OverhaulSettings;
using InnoVault.GameSystem;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    //护林者基地
    internal class SylvanOutpost : SaveStructure
    {
        public override string SavePath => Path.Combine(StructurePath, "SylvanOutpost_v1.nbt");
        public override void Load() => Mod.EnsureFileFromMod("Content/Structures/DatIO/SylvanOutpost_v1.nbt", SavePath);
        public override void SaveData(TagCompound tag)//测试用的复制代码，不用管
            => SaveRegion(tag, new Point16(4311, 478).GetRectangleFromPoints(new Point16(4407, 450)));
        public override void LoadData(TagCompound tag) {
            var density = WorldGenDensitySave.GetDensity("SylvanOutpost");
            if (density == StructureDensity.Extinction) {
                TagCache.Invalidate(SavePath);
                return;
            }

            int spawnCount = density switch {
                StructureDensity.Rare => 1,
                StructureDensity.Normal => WorldGen.genRand.Next(1, 3),
                StructureDensity.Common => WorldGen.genRand.Next(2, 4),
                StructureDensity.Flood => WorldGen.genRand.Next(3, 5),
                StructureDensity.Everywhere => WorldGen.genRand.Next(4, 7),
                _ => 1
            };

            RegionSaveData region = tag.GetRegionSaveData();
            for (int i = 0; i < spawnCount; i++) {
                Point16 startPos = FindForestSurfacePosition(region.Size, i);
                if (startPos == Point16.Zero) {
                    continue;
                }
                PrepareTerrainForOutpost(startPos, region.Size);
                var placed = LoadRegion(region, startPos);
                RepairFoundation(startPos, region.Size);
                SetChestItem(placed, startPos);
            }
            TagCache.Invalidate(SavePath);
        }

        /// <summary>
        /// 寻找森林环境下的地表位置，采用更激进的搜索策略
        /// </summary>
        private static Point16 FindForestSurfacePosition(Point16 regionSize, int instanceIndex = 0) {
            int width = regionSize.X;
            int height = regionSize.Y;
            int minDistFromSpawn = 180 + WorldGen.GetWorldSize() * 60
                + instanceIndex * 120;

            //第一阶段：优先在理想距离内搜索森林
            int searchMinDist = Math.Max(minDistFromSpawn, 200) + instanceIndex * 150;
            int searchMaxDist = 600 + instanceIndex * 200;
            Point16 result = SearchInRange(width, height, searchMinDist, searchMaxDist, true);
            if (result != Point16.Zero) {
                return result;
            }

            //第二阶段：扩大范围，放宽森林要求
            result = SearchInRange(width, height, Math.Max(minDistFromSpawn, 150), 900 + instanceIndex * 200, false);
            if (result != Point16.Zero) {
                return result;
            }

            //第三阶段：全地图扫描，只要是地表就行
            result = FullSurfaceScan(width, height, minDistFromSpawn);
            return result;
        }

        /// <summary>
        /// 在指定范围内搜索合适位置
        /// </summary>
        private static Point16 SearchInRange(int width, int height, int minDist, int maxDist, bool requireForest) {
            Point16 bestPos = Point16.Zero;
            int bestScore = -1;

            //从出生点向两侧系统性搜索，步长更小以找到更好的位置
            for (int dist = minDist; dist <= maxDist; dist += 15) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    int testX = Main.spawnTileX + dir * dist;
                    testX = Math.Clamp(testX, 150, Main.maxTilesX - 150 - width);

                    int surfaceY = FindBestSurfaceY(testX, width, out int flatnessScore);
                    if (surfaceY <= 0) {
                        continue;
                    }

                    //平坦度太低直接跳过，避免生成在山峰上
                    if (flatnessScore < 30) {
                        continue;
                    }

                    int placeX = testX;
                    int placeY = surfaceY - height + 2;

                    //基础边界检查
                    if (placeY < 50 || placeY + height > Main.worldSurface + 50) {
                        continue;
                    }

                    //检查是否在恶劣环境
                    if (IsInBadBiome(placeX, placeY, width, height)) {
                        continue;
                    }

                    //综合评分，平坦度权重很高
                    int baseScore = EvaluatePositionSimple(placeX, placeY, width, height, requireForest);
                    int score = baseScore + flatnessScore;//平坦度直接加到总分中

                    if (score > bestScore) {
                        bestScore = score;
                        bestPos = new Point16(placeX, placeY);
                    }

                    //分数够高直接返回，要求更高的分数以确保平坦
                    if (score >= 140) {
                        return bestPos;
                    }
                }
            }

            //只要找到了任何可用位置就返回
            if (bestScore >= 50) {
                return bestPos;
            }

            return Point16.Zero;
        }

        /// <summary>
        /// 全地图扫描寻找任意可用的地表位置
        /// </summary>
        private static Point16 FullSurfaceScan(int width, int height, int minDistFromSpawn) {
            //从世界中心向两侧扫描
            int centerX = Main.maxTilesX / 2;
            int scanStep = 30;//更小的步长以找到更平坦的位置

            Point16 bestPos = Point16.Zero;
            int bestFlatness = -1;

            for (int offset = 0; offset < Main.maxTilesX / 2 - 200; offset += scanStep) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    int testX = centerX + dir * offset;
                    if (testX < 200 || testX > Main.maxTilesX - 200 - width) {
                        continue;
                    }

                    //检查是否离出生点太近
                    if (Math.Abs(testX - Main.spawnTileX) < minDistFromSpawn) {
                        continue;
                    }

                    int surfaceY = FindBestSurfaceY(testX, width, out int flatnessScore);
                    if (surfaceY <= 0) {
                        continue;
                    }

                    int placeY = surfaceY - height + 2;
                    if (placeY < 50 || placeY + height > Main.worldSurface + 80) {
                        continue;
                    }

                    //最后阶段只排除最恶劣的环境
                    if (IsInBadBiome(testX, placeY, width, height)) {
                        continue;
                    }

                    //即使是全图扫描也要尽量选择平坦的位置
                    if (flatnessScore > bestFlatness) {
                        bestFlatness = flatnessScore;
                        bestPos = new Point16(testX, placeY);
                        //找到足够平坦的位置就返回
                        if (flatnessScore >= 60) {
                            return bestPos;
                        }
                    }
                }
            }

            return bestPos;
        }

        /// <summary>
        /// 寻找最佳地表Y坐标，会尝试找到相对平坦的区域并排除空岛
        /// </summary>
        private static int FindBestSurfaceY(int startX, int width, out int flatnessScore) {
            flatnessScore = 0;
            int[] surfaceHeights = new int[width / 4 + 1];//更密集的采样以准确评估平坦度
            int validCount = 0;

            //采样多个点获取地表高度
            for (int i = 0; i < surfaceHeights.Length; i++) {
                int checkX = startX + i * 4;
                if (checkX >= Main.maxTilesX) {
                    break;
                }

                for (int y = 50; y < Main.worldSurface + 100; y++) {
                    Tile tile = Framing.GetTileSafely(checkX, y);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        surfaceHeights[validCount++] = y;
                        break;
                    }
                }
            }

            if (validCount < 5) {
                return -1;
            }

            //计算中位数作为基准高度
            int[] sortedHeights = new int[validCount];
            Array.Copy(surfaceHeights, sortedHeights, validCount);
            Array.Sort(sortedHeights);
            int medianY = sortedHeights[validCount / 2];

            //计算平坦度分数，统计高度差异
            int totalDeviation = 0;
            int maxDeviation = 0;
            for (int i = 0; i < validCount; i++) {
                int deviation = Math.Abs(surfaceHeights[i] - medianY);
                totalDeviation += deviation;
                if (deviation > maxDeviation) {
                    maxDeviation = deviation;
                }
            }

            //平均偏差和最大偏差都会影响平坦度评分
            float avgDeviation = (float)totalDeviation / validCount;
            //平坦度分数：偏差越小分数越高，满分100
            //平均偏差小于2格为非常平坦，大于8格为很不平坦
            flatnessScore = Math.Max(0, 100 - (int)(avgDeviation * 12) - maxDeviation * 2);

            //如果最大偏差超过12格，说明地形起伏太大，直接判定为不平坦
            if (maxDeviation > 12) {
                flatnessScore = Math.Min(flatnessScore, 20);
            }

            //验证这个高度是否是真正的地表而非空岛
            if (!IsValidGroundLevel(startX, medianY, width)) {
                return -1;
            }

            return medianY;
        }

        /// <summary>
        /// 重载版本，不需要平坦度分数时使用
        /// </summary>
        private static int FindBestSurfaceY(int startX, int width) {
            return FindBestSurfaceY(startX, width, out _);
        }

        /// <summary>
        /// 验证指定高度是否为真正的地表而非空岛或高空建筑
        /// </summary>
        private static bool IsValidGroundLevel(int startX, int surfaceY, int width) {
            //如果高度明显高于世界地表线太多，很可能是空岛
            if (surfaceY < Main.worldSurface * 0.35) {
                return false;
            }

            //检查地表下方是否有足够的连续实心地层
            //真正的地表下方应该有大量连续的泥土石头，空岛下方则是空气
            int solidCount = 0;
            int airCount = 0;
            int checkDepth = 40;//检查深度

            for (int checkX = startX; checkX < startX + width; checkX += 12) {
                if (checkX >= Main.maxTilesX) {
                    break;
                }

                for (int dy = 5; dy < checkDepth; dy++) {
                    int checkY = surfaceY + dy;
                    if (!WorldGen.InWorld(checkX, checkY)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        solidCount++;
                    }
                    else {
                        airCount++;
                    }
                }
            }

            //如果下方空气占比超过60%，判定为空岛
            int total = solidCount + airCount;
            if (total > 0 && (float)airCount / total > 0.6f) {
                return false;
            }

            //额外检查：验证该位置下方在更深处是否能找到真正的地层
            //检查是否存在连续的实心层
            int consecutiveSolid = 0;
            int maxConsecutive = 0;
            int centerX = startX + width / 2;

            for (int dy = 0; dy < 80; dy++) {
                int checkY = surfaceY + dy;
                if (!WorldGen.InWorld(centerX, checkY)) {
                    break;
                }

                Tile tile = Framing.GetTileSafely(centerX, checkY);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    consecutiveSolid++;
                    maxConsecutive = Math.Max(maxConsecutive, consecutiveSolid);
                }
                else {
                    consecutiveSolid = 0;
                }
            }

            //真正的地表下方应该有至少15格连续实心层
            if (maxConsecutive < 15) {
                return false;
            }

            //检查是否是模组建筑方块(比如实验室镀板等)
            int modTileCount = 0;
            for (int checkX = startX; checkX < startX + width; checkX += 15) {
                for (int dy = 0; dy < 10; dy++) {
                    int checkY = surfaceY + dy;
                    if (!WorldGen.InWorld(checkX, checkY)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile) {
                        //原版方块ID上限约为700，超过的可能是模组方块
                        if (tile.TileType >= 700) {
                            modTileCount++;
                        }
                        //云块和日盘等空岛特有方块
                        if (tile.TileType == TileID.Cloud || tile.TileType == TileID.RainCloud
                            || tile.TileType == TileID.SnowCloud || tile.TileType == TileID.Sunplate
                            || tile.TileType == TileID.LivingWood || tile.TileType == TileID.LeafBlock) {
                            return false;
                        }
                    }
                }
            }

            //如果模组方块占比过高，可能是模组建筑
            if (modTileCount > 5) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查是否在不适合的生物群系中
        /// </summary>
        private static bool IsInBadBiome(int x, int y, int width, int height) {
            int badTileCount = 0;
            int checkCount = 0;

            for (int checkX = x; checkX < x + width; checkX += 15) {
                for (int checkY = y; checkY < y + height; checkY += 10) {
                    checkCount++;
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (!tile.HasTile) {
                        continue;
                    }

                    //腐化猩红神圣丛林雪地沙漠等
                    if (tile.TileType == TileID.CorruptGrass || tile.TileType == TileID.CrimsonGrass
                        || tile.TileType == TileID.Ebonstone || tile.TileType == TileID.Crimstone
                        || tile.TileType == TileID.JungleGrass || tile.TileType == TileID.SnowBlock
                        || tile.TileType == TileID.IceBlock || tile.TileType == TileID.Sand
                        || tile.TileType == TileID.Sandstone || tile.TileType == TileID.HardenedSand) {
                        badTileCount++;
                    }
                }
            }

            //超过30%是恶劣地形则排除
            return checkCount > 0 && (float)badTileCount / checkCount > 0.3f;
        }

        /// <summary>
        /// 简化的位置评分
        /// </summary>
        private static int EvaluatePositionSimple(int x, int y, int width, int height, bool requireForest) {
            int score = 50;

            //统计草地数量判断是否森林
            int grassCount = 0;
            int groundY = y + height - 2;
            for (int checkX = x; checkX < x + width; checkX += 8) {
                Tile tile = Framing.GetTileSafely(checkX, groundY);
                if (tile.HasTile && (tile.TileType == TileID.Grass || tile.TileType == TileID.Dirt)) {
                    grassCount++;
                }
            }

            if (requireForest && grassCount < 3) {
                return 10;//不是森林但也不完全排除
            }
            score += grassCount * 3;

            //检查液体
            for (int checkX = x; checkX < x + width; checkX += 12) {
                for (int checkY = y; checkY < y + height; checkY += 8) {
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.LiquidAmount > 100) {
                        score -= 10;
                    }
                }
            }

            //检查地基下方是否有足够的实心地层支撑
            //这能避免生成在悬崖边或地形断层处
            int solidFoundationCount = 0;
            int checkFoundationDepth = 15;
            for (int checkX = x; checkX < x + width; checkX += 10) {
                int solidInColumn = 0;
                for (int dy = 0; dy < checkFoundationDepth; dy++) {
                    int checkY = groundY + dy;
                    if (!WorldGen.InWorld(checkX, checkY)) {
                        break;
                    }
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        solidInColumn++;
                    }
                }
                //该列有超过一半是实心则计入
                if (solidInColumn > checkFoundationDepth / 2) {
                    solidFoundationCount++;
                }
            }

            //地基支撑越完整分数越高
            int expectedColumns = width / 10;
            if (expectedColumns > 0) {
                float foundationRatio = (float)solidFoundationCount / expectedColumns;
                score += (int)(foundationRatio * 20);//最多加20分
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// 准备地形，清理建筑放置区域
        /// </summary>
        private static void PrepareTerrainForOutpost(Point16 startPos, Point16 regionSize) {
            int x = startPos.X;
            int y = startPos.Y;
            int width = regionSize.X;
            int height = regionSize.Y;

            //清理建筑区域内的方块和墙壁
            for (int px = x; px < x + width; px++) {
                for (int py = y; py < y + height; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    //清除方块
                    if (tile.HasTile) {
                        WorldGen.KillTile(px, py, false, false, true);
                    }
                    //清除自然墙壁
                    if (tile.WallType > WallID.None && tile.WallType < WallID.Count) {
                        WorldGen.KillWall(px, py, false);
                    }
                    //清除液体
                    tile.LiquidAmount = 0;
                }
            }

            //清理上方可能遮挡的树木和方块
            for (int px = x - 5; px < x + width + 5; px++) {
                for (int py = y - 30; py < y; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile) {
                        int tileType = tile.TileType;
                        //清除树木及其相关物块
                        if (tileType == TileID.Trees || tileType == TileID.VanityTreeSakura
                            || tileType == TileID.VanityTreeYellowWillow || tileType == TileID.Sunflower) {
                            WorldGen.KillTile(px, py, false, false, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 修复地基，确保建筑下方完全填充且自然融入周围地形
        /// </summary>
        private static void RepairFoundation(Point16 startPos, Point16 regionSize) {
            int x = startPos.X;
            int y = startPos.Y;
            int width = regionSize.X;
            int height = regionSize.Y;
            int groundY = y + height - 2;//建筑底部(自带两层泥土)

            //第一步：深度填充建筑下方区域
            int fillDepth = 25;//向下填充深度
            for (int px = x; px < x + width; px++) {
                if (!WorldGen.InWorld(px, groundY)) {
                    continue;
                }

                //找到该列最深的实心方块位置
                int deepestSolid = groundY + fillDepth;
                for (int py = groundY; py < groundY + fillDepth + 10; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        break;
                    }
                    Tile checkTile = Framing.GetTileSafely(px, py);
                    if (checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                        deepestSolid = py;
                        break;
                    }
                }

                //从地基向下填充直到遇到实心方块或达到最大深度
                for (int py = groundY; py <= Math.Min(deepestSolid, groundY + fillDepth); py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (!tile.HasTile) {
                        //根据深度选择填充物
                        int fillType = TileID.Dirt;
                        if (py > groundY + 8) {
                            fillType = TileID.Stone;//深处用石头
                        }
                        WorldGen.PlaceTile(px, py, fillType, true, true);
                    }
                }
            }

            //第二步：处理两侧边缘的地形过渡
            int blendRange = 12;//融合范围
            int blendDepth = 20;//融合深度

            //左侧融合
            BlendEdge(x, groundY, blendRange, blendDepth, true);
            //右侧融合
            BlendEdge(x + width - 1, groundY, blendRange, blendDepth, false);

            //第三步：铺设草地表层
            for (int px = x - blendRange; px < x + width + blendRange; px++) {
                if (!WorldGen.InWorld(px, groundY)) {
                    continue;
                }

                //找到该位置的实际地表
                for (int py = groundY - 5; py < groundY + 10; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile && tile.TileType == TileID.Dirt) {
                        //检查上方是否露天
                        Tile above = Framing.GetTileSafely(px, py - 1);
                        if (!above.HasTile || !Main.tileSolid[above.TileType]) {
                            tile.TileType = TileID.Grass;
                            break;
                        }
                    }
                }
            }

            //第四步：添加自然装饰让地形更自然
            AddNaturalDecoration(x, groundY, width, blendRange);
        }

        /// <summary>
        /// 融合边缘地形
        /// </summary>
        private static void BlendEdge(int edgeX, int groundY, int blendRange, int blendDepth, bool isLeft) {
            int dir = isLeft ? -1 : 1;

            for (int offset = 1; offset <= blendRange; offset++) {
                int px = edgeX + dir * offset;
                if (!WorldGen.InWorld(px, groundY)) {
                    continue;
                }

                //融合因子：距离建筑越远融合程度越低
                float blendFactor = 1f - (float)offset / blendRange;

                //找到周围地形的自然高度
                int naturalSurfaceY = groundY;
                for (int searchY = groundY - 15; searchY < groundY + 15; searchY++) {
                    if (!WorldGen.InWorld(px + dir * 5, searchY)) {
                        continue;
                    }
                    Tile searchTile = Framing.GetTileSafely(px + dir * 5, searchY);
                    if (searchTile.HasTile && Main.tileSolid[searchTile.TileType]) {
                        naturalSurfaceY = searchY;
                        break;
                    }
                }

                //计算该位置应该的地表高度(在建筑地面和自然地面之间插值)
                int targetSurfaceY = (int)MathHelper.Lerp(naturalSurfaceY, groundY, blendFactor);

                //填充该列从目标地表到一定深度
                int currentFillDepth = (int)(blendDepth * blendFactor) + 5;
                for (int py = targetSurfaceY; py < targetSurfaceY + currentFillDepth; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (!tile.HasTile) {
                        int fillType = TileID.Dirt;
                        if (py > targetSurfaceY + 6) {
                            fillType = TileID.Stone;
                        }
                        //添加一些随机性让边缘更自然
                        if (WorldGen.genRand.NextFloat() < blendFactor * 0.9f) {
                            WorldGen.PlaceTile(px, py, fillType, true, true);
                        }
                    }
                }

                //清理地表上方可能存在的悬空方块
                for (int py = targetSurfaceY - 1; py > targetSurfaceY - 10; py--) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile && WorldGen.genRand.NextFloat() > blendFactor * 0.3f) {
                        //保留部分方块让过渡更自然
                        if (Main.tileSolid[tile.TileType] && tile.TileType != TileID.Trees) {
                            if (WorldGen.genRand.NextFloat() > blendFactor) {
                                WorldGen.KillTile(px, py, false, false, true);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 添加自然装饰
        /// </summary>
        private static void AddNaturalDecoration(int baseX, int groundY, int width, int blendRange) {
            //在建筑两侧随机生成一些草和小植物
            for (int px = baseX - blendRange; px < baseX + width + blendRange; px++) {
                if (!WorldGen.InWorld(px, groundY - 1)) {
                    continue;
                }

                //跳过建筑主体区域
                if (px >= baseX && px < baseX + width) {
                    continue;
                }

                Tile groundTile = Framing.GetTileSafely(px, groundY);
                Tile aboveTile = Framing.GetTileSafely(px, groundY - 1);

                //在草地上生成装饰
                if (groundTile.HasTile && groundTile.TileType == TileID.Grass && !aboveTile.HasTile) {
                    if (WorldGen.genRand.NextBool(4)) {
                        //生成短草
                        WorldGen.PlaceTile(px, groundY - 1, TileID.Plants, true, false, -1, WorldGen.genRand.Next(0, 44));
                    }
                    else if (WorldGen.genRand.NextBool(12)) {
                        //生成花朵
                        WorldGen.PlaceTile(px, groundY - 1, TileID.Plants2, true, false, -1, WorldGen.genRand.Next(0, 8));
                    }
                }
            }

            //在边缘区域尝试生成小树苗增加自然感
            for (int i = 0; i < 3; i++) {
                int treeX = WorldGen.genRand.NextBool()
                    ? baseX - WorldGen.genRand.Next(3, blendRange)
                    : baseX + width + WorldGen.genRand.Next(3, blendRange);

                if (!WorldGen.InWorld(treeX, groundY)) {
                    continue;
                }

                //找到该位置的地表
                for (int py = groundY - 5; py < groundY + 5; py++) {
                    Tile ground = Framing.GetTileSafely(treeX, py);
                    Tile above = Framing.GetTileSafely(treeX, py - 1);
                    if (ground.HasTile && ground.TileType == TileID.Grass && !above.HasTile) {
                        if (WorldGen.genRand.NextBool(3)) {
                            WorldGen.PlaceTile(treeX, py - 1, TileID.Saplings, true, false);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 设置箱子内容物
        /// </summary>
        private static void SetChestItem(RegionSaveData regionSaveData, Point16 orig) {
            //定义可能的战利品
            int[] commonItems = [
                ItemID.Acorn, ItemID.Wood, ItemID.Torch, ItemID.Rope,
                ItemID.HerbBag, ItemID.Daybloom, ItemID.Blinkroot
            ];
            int[] uncommonItems = [
                ItemID.SunflowerMinecart, ItemID.Mushroom, ItemID.GlowingMushroom,
                ItemID.RecallPotion, ItemID.WormholePotion, ItemID.LifeCrystal
            ];
            int[] rareItems = [
                ItemID.StaffofRegrowth, ItemID.FlowerBoots, ItemID.NaturesGift
            ];

            foreach (var chestTag in regionSaveData.Chests) {
                ChestSaveData chestSaveData = ChestSaveData.FromTag(chestTag);
                //需要注意这里chestSaveData拿到的坐标只是相对坐标，所以需要加上orig
                int chestIndex = Chest.FindChest(orig.X + chestSaveData.X, orig.Y + chestSaveData.Y);
                if (chestIndex < 0) {
                    continue;
                }

                Chest chest = Main.chest[chestIndex];
                int maxSlot = chest.item.Length;
                int slot = 0;

                //固定物品
                if (slot < maxSlot) chest.item[slot++] = new Item(ItemID.Wood, WorldGen.genRand.Next(50, 100));
                if (slot < maxSlot) chest.item[slot++] = new Item(ItemID.Acorn, WorldGen.genRand.Next(10, 20));
                if (slot < maxSlot) chest.item[slot++] = new Item(ItemID.HerbBag, WorldGen.genRand.Next(10, 16));
                if (slot < maxSlot && WorldGen.genRand.NextBool(3)) {
                    chest.item[slot++] = new Item(rareItems[WorldGen.genRand.Next(rareItems.Length)], 1);
                }

                //随机物品
                int itemCount = WorldGen.genRand.Next(4, 8);
                for (int i = 0; i < itemCount && slot < maxSlot; i++) {
                    int rand = WorldGen.genRand.Next(100);
                    if (rand < 60) {
                        chest.item[slot++] = new Item(commonItems[WorldGen.genRand.Next(commonItems.Length)]
                            , WorldGen.genRand.Next(5, 20));
                    }
                    else if (rand < 90) {
                        chest.item[slot++] = new Item(uncommonItems[WorldGen.genRand.Next(uncommonItems.Length)]
                            , WorldGen.genRand.Next(1, 5));
                    }
                    else {
                        chest.item[slot++] = new Item(rareItems[WorldGen.genRand.Next(rareItems.Length)], 1);
                    }
                }
            }
        }
    }
}
