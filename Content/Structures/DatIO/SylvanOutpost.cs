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

            try {
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
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[SylvanOutpost:LoadData] Failed to load/place structure: {ex.Message}");
            }
            TagCache.Invalidate(SavePath);
        }

        private static Point16 FindForestSurfacePosition(Point16 regionSize, int instanceIndex = 0) {
            int width = regionSize.X;
            int height = regionSize.Y;
            int minDistFromSpawn = 180 + WorldGen.GetWorldSize() * 60
                + instanceIndex * 120;

            //阶段1 理想距森林
            int searchMinDist = Math.Max(minDistFromSpawn, 200) + instanceIndex * 150;
            int searchMaxDist = 600 + instanceIndex * 200;
            Point16 result = SearchInRange(width, height, searchMinDist, searchMaxDist, true);
            if (result != Point16.Zero) {
                return result;
            }

            //阶段2 放宽森林
            result = SearchInRange(width, height, Math.Max(minDistFromSpawn, 150), 900 + instanceIndex * 200, false);
            if (result != Point16.Zero) {
                return result;
            }

            //阶段3 全图地表
            result = FullSurfaceScan(width, height, minDistFromSpawn);
            return result;
        }

        private static Point16 SearchInRange(int width, int height, int minDist, int maxDist, bool requireForest) {
            Point16 bestPos = Point16.Zero;
            int bestScore = -1;

            //自出生点两侧搜，小步长
            for (int dist = minDist; dist <= maxDist; dist += 15) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    int testX = Main.spawnTileX + dir * dist;
                    testX = Math.Clamp(testX, 150, Main.maxTilesX - 150 - width);

                    int surfaceY = FindBestSurfaceY(testX, width, out int flatnessScore);
                    if (surfaceY <= 0) {
                        continue;
                    }

                    //太平坦度不够则跳
                    if (flatnessScore < 30) {
                        continue;
                    }

                    int placeX = testX;
                    int placeY = surfaceY - height + 2;

                    if (placeY < 50 || placeY + height > Main.worldSurface + 50) {
                        continue;
                    }

                    if (IsInBadBiome(placeX, placeY, width, height)) {
                        continue;
                    }

                    //评分，平坦权重高
                    int baseScore = EvaluatePositionSimple(placeX, placeY, width, height, requireForest);
                    int score = baseScore + flatnessScore;//平坦度直接加到总分中

                    if (score > bestScore) {
                        bestScore = score;
                        bestPos = new Point16(placeX, placeY);
                    }

                    //分够高直接返回
                    if (score >= 140) {
                        return bestPos;
                    }
                }
            }

            if (bestScore >= 50) {
                return bestPos;
            }

            return Point16.Zero;
        }

        private static Point16 FullSurfaceScan(int width, int height, int minDistFromSpawn) {
            int centerX = Main.maxTilesX / 2;
            int scanStep = 30;

            Point16 bestPos = Point16.Zero;
            int bestFlatness = -1;

            for (int offset = 0; offset < Main.maxTilesX / 2 - 200; offset += scanStep) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    int testX = centerX + dir * offset;
                    if (testX < 200 || testX > Main.maxTilesX - 200 - width) {
                        continue;
                    }

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

                    //只排除最恶劣环境
                    if (IsInBadBiome(testX, placeY, width, height)) {
                        continue;
                    }

                    if (flatnessScore > bestFlatness) {
                        bestFlatness = flatnessScore;
                        bestPos = new Point16(testX, placeY);
                        if (flatnessScore >= 60) {
                            return bestPos;
                        }
                    }
                }
            }

            return bestPos;
        }

        private static int FindBestSurfaceY(int startX, int width, out int flatnessScore) {
            flatnessScore = 0;
            int[] surfaceHeights = new int[width / 4 + 1];
            int validCount = 0;

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

            //中位数作基准高
            int[] sortedHeights = new int[validCount];
            Array.Copy(surfaceHeights, sortedHeights, validCount);
            Array.Sort(sortedHeights);
            int medianY = sortedHeights[validCount / 2];

            int totalDeviation = 0;
            int maxDeviation = 0;
            for (int i = 0; i < validCount; i++) {
                int deviation = Math.Abs(surfaceHeights[i] - medianY);
                totalDeviation += deviation;
                if (deviation > maxDeviation) {
                    maxDeviation = deviation;
                }
            }

            float avgDeviation = (float)totalDeviation / validCount;
            //平坦分，偏差小更高，满分100
            //均偏<2很好，>8很差
            flatnessScore = Math.Max(0, 100 - (int)(avgDeviation * 12) - maxDeviation * 2);

            //最大偏>12直接不平坦
            if (maxDeviation > 12) {
                flatnessScore = Math.Min(flatnessScore, 20);
            }

            if (!IsValidGroundLevel(startX, medianY, width)) {
                return -1;
            }

            return medianY;
        }

        private static int FindBestSurfaceY(int startX, int width) {
            return FindBestSurfaceY(startX, width, out _);
        }

        private static bool IsValidGroundLevel(int startX, int surfaceY, int width) {
            //明显高于地表线→疑空岛
            if (surfaceY < Main.worldSurface * 0.35) {
                return false;
            }

            //下方需连续实心层
            int solidCount = 0;
            int airCount = 0;
            int checkDepth = 40;

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

            //下方空气>60%→空岛
            int total = solidCount + airCount;
            if (total > 0 && (float)airCount / total > 0.6f) {
                return false;
            }

            //深处再验实心层
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

            //至少15格连续实心
            if (maxConsecutive < 15) {
                return false;
            }

            //模组建筑方块？
            int modTileCount = 0;
            for (int checkX = startX; checkX < startX + width; checkX += 15) {
                for (int dy = 0; dy < 10; dy++) {
                    int checkY = surfaceY + dy;
                    if (!WorldGen.InWorld(checkX, checkY)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile) {
                        //ID>700 疑模组方块
                        if (tile.TileType >= 700) {
                            modTileCount++;
                        }
                        if (tile.TileType == TileID.Cloud || tile.TileType == TileID.RainCloud
                            || tile.TileType == TileID.SnowCloud || tile.TileType == TileID.Sunplate
                            || tile.TileType == TileID.LivingWood || tile.TileType == TileID.LeafBlock) {
                            return false;
                        }
                    }
                }
            }

            //模组方块过多→模组建筑
            if (modTileCount > 5) {
                return false;
            }

            return true;
        }

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

                    if (tile.TileType == TileID.CorruptGrass || tile.TileType == TileID.CrimsonGrass
                        || tile.TileType == TileID.Ebonstone || tile.TileType == TileID.Crimstone
                        || tile.TileType == TileID.JungleGrass || tile.TileType == TileID.SnowBlock
                        || tile.TileType == TileID.IceBlock || tile.TileType == TileID.Sand
                        || tile.TileType == TileID.Sandstone || tile.TileType == TileID.HardenedSand) {
                        badTileCount++;
                    }
                }
            }

            //恶劣地形>30%排除
            return checkCount > 0 && (float)badTileCount / checkCount > 0.3f;
        }

        private static int EvaluatePositionSimple(int x, int y, int width, int height, bool requireForest) {
            int score = 50;

            int grassCount = 0;
            int groundY = y + height - 2;
            for (int checkX = x; checkX < x + width; checkX += 8) {
                Tile tile = Framing.GetTileSafely(checkX, groundY);
                if (tile.HasTile && (tile.TileType == TileID.Grass || tile.TileType == TileID.Dirt)) {
                    grassCount++;
                }
            }

            if (requireForest && grassCount < 3) {
                return 10;//非森林，弱保留
            }
            score += grassCount * 3;

            for (int checkX = x; checkX < x + width; checkX += 12) {
                for (int checkY = y; checkY < y + height; checkY += 8) {
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.LiquidAmount > 100) {
                        score -= 10;
                    }
                }
            }

            //地基下实心支撑
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
                if (solidInColumn > checkFoundationDepth / 2) {
                    solidFoundationCount++;
                }
            }

            int expectedColumns = width / 10;
            if (expectedColumns > 0) {
                float foundationRatio = (float)solidFoundationCount / expectedColumns;
                score += (int)(foundationRatio * 20);
            }

            return Math.Max(0, Math.Min(100, score));
        }

        private static void PrepareTerrainForOutpost(Point16 startPos, Point16 regionSize) {
            int x = startPos.X;
            int y = startPos.Y;
            int width = regionSize.X;
            int height = regionSize.Y;

            //清建筑区
            for (int px = x; px < x + width; px++) {
                for (int py = y; py < y + height; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile) {
                        WorldGen.KillTile(px, py, false, false, true);
                    }
                    if (tile.WallType > WallID.None && tile.WallType < WallID.Count) {
                        WorldGen.KillWall(px, py, false);
                    }
                    tile.LiquidAmount = 0;
                }
            }

            //清上方树木
            for (int px = x - 5; px < x + width + 5; px++) {
                for (int py = y - 30; py < y; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile) {
                        int tileType = tile.TileType;
                        if (tileType == TileID.Trees || tileType == TileID.VanityTreeSakura
                            || tileType == TileID.VanityTreeYellowWillow || tileType == TileID.Sunflower) {
                            WorldGen.KillTile(px, py, false, false, true);
                        }
                    }
                }
            }
        }

        private static void RepairFoundation(Point16 startPos, Point16 regionSize) {
            int x = startPos.X;
            int y = startPos.Y;
            int width = regionSize.X;
            int height = regionSize.Y;
            int groundY = y + height - 2;//建筑底(含两层土)

            //1 深填下方
            int fillDepth = 25;
            for (int px = x; px < x + width; px++) {
                if (!WorldGen.InWorld(px, groundY)) {
                    continue;
                }

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

                for (int py = groundY; py <= Math.Min(deepestSolid, groundY + fillDepth); py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (!tile.HasTile) {
                        int fillType = TileID.Dirt;
                        if (py > groundY + 8) {
                            fillType = TileID.Stone;
                        }
                        WorldGen.PlaceTile(px, py, fillType, true, true);
                    }
                }
            }

            //2 两侧过渡
            int blendRange = 12;
            int blendDepth = 20;

            BlendEdge(x, groundY, blendRange, blendDepth, true);
            BlendEdge(x + width - 1, groundY, blendRange, blendDepth, false);

            //3 草地表层
            for (int px = x - blendRange; px < x + width + blendRange; px++) {
                if (!WorldGen.InWorld(px, groundY)) {
                    continue;
                }

                for (int py = groundY - 5; py < groundY + 10; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile && tile.TileType == TileID.Dirt) {
                        Tile above = Framing.GetTileSafely(px, py - 1);
                        if (!above.HasTile || !Main.tileSolid[above.TileType]) {
                            tile.TileType = TileID.Grass;
                            break;
                        }
                    }
                }
            }

            //4 自然装饰
            AddNaturalDecoration(x, groundY, width, blendRange);
        }

        private static void BlendEdge(int edgeX, int groundY, int blendRange, int blendDepth, bool isLeft) {
            int dir = isLeft ? -1 : 1;

            for (int offset = 1; offset <= blendRange; offset++) {
                int px = edgeX + dir * offset;
                if (!WorldGen.InWorld(px, groundY)) {
                    continue;
                }

                //越远融合越弱
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
                //chestSaveData 相对坐标，需加 orig
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
