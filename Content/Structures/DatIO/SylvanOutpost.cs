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
            RegionSaveData region = tag.GetRegionSaveData();//获取建筑数据
            Point16 startPos = FindForestSurfacePosition(region.Size);
            if (startPos == Point16.Zero) {
                TagCache.Invalidate(SavePath);
                return;//找不到合适的位置则跳过生成
            }
            //执行地形清理，让建筑自然融入周围
            PrepareTerrainForOutpost(startPos, region.Size);
            LoadRegion(region, startPos);//这个区域宽97高29(物块)
            SetChestItem(region, startPos);
            TagCache.Invalidate(SavePath);//释放缓存
        }

        /// <summary>
        /// 寻找森林环境下的地表位置
        /// </summary>
        private static Point16 FindForestSurfacePosition(Point16 regionSize) {
            int width = regionSize.X;//建筑宽度
            int height = regionSize.Y;//建筑高度
            int minDistFromSpawn = 200 + WorldGen.GetWorldSize() * 100;//离出生点最小距离
            int maxDistFromSpawn = 600 + WorldGen.GetWorldSize() * 200;//离出生点最大距离
            int searchAttempts = 800;//搜索尝试次数

            Point16 bestPos = Point16.Zero;
            int bestScore = -1;

            for (int attempt = 0; attempt < searchAttempts; attempt++) {
                //在出生点两侧随机选择一个方向
                int direction = WorldGen.genRand.NextBool() ? 1 : -1;
                int distFromSpawn = WorldGen.genRand.Next(minDistFromSpawn, maxDistFromSpawn);
                int testX = Main.spawnTileX + (direction * distFromSpawn);

                //确保X坐标在世界边界内
                testX = Math.Clamp(testX, 100 + width / 2, Main.maxTilesX - 100 - width / 2);

                //从地表往下搜索合适的地面位置
                int surfaceY = FindSurfaceAt(testX, width);
                if (surfaceY <= 0) {
                    continue;
                }

                //计算建筑放置原点(建筑底部中心对齐地面)
                int placeX = testX - width / 2;
                int placeY = surfaceY - height + 2;//+2是因为建筑自带两层泥土地基

                //验证位置是否合适
                int score = EvaluatePosition(placeX, placeY, width, height);
                if (score > bestScore) {
                    bestScore = score;
                    bestPos = new Point16(placeX, placeY);
                }

                //分数足够高就直接使用
                if (score >= 80) {
                    break;
                }
            }

            return bestPos;
        }

        /// <summary>
        /// 在指定X坐标处寻找地表Y坐标
        /// </summary>
        private static int FindSurfaceAt(int centerX, int width) {
            //检查多个点来确保地面相对平坦
            int[] checkPoints = [centerX - width / 3, centerX, centerX + width / 3];
            int avgSurfaceY = 0;
            int validPoints = 0;

            foreach (int x in checkPoints) {
                if (x < 0 || x >= Main.maxTilesX) {
                    continue;
                }

                //从天空往下搜索第一个实心方块
                for (int y = 50; y < Main.worldSurface + 50; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        avgSurfaceY += y;
                        validPoints++;
                        break;
                    }
                }
            }

            if (validPoints < 2) {
                return -1;//地面不够平坦
            }

            return avgSurfaceY / validPoints;
        }

        /// <summary>
        /// 评估位置的适合程度，返回0到100的分数
        /// </summary>
        private static int EvaluatePosition(int x, int y, int width, int height) {
            int score = 100;

            //检查是否在世界边界内
            if (x < 50 || x + width > Main.maxTilesX - 50) {
                return 0;
            }
            if (y < 50 || y + height > Main.worldSurface + 100) {
                return 0;
            }

            //检查是否是森林环境(主要看地面是否是草地)
            int grassCount = 0;
            int groundY = y + height - 2;
            for (int checkX = x; checkX < x + width; checkX += 5) {
                Tile groundTile = Framing.GetTileSafely(checkX, groundY);
                if (groundTile.HasTile && groundTile.TileType == TileID.Grass) {
                    grassCount++;
                }
            }
            if (grassCount < 3) {
                score -= 40;//不是森林环境扣分
            }

            //检查地面平坦度
            int prevY = -1;
            int heightVariation = 0;
            for (int checkX = x; checkX < x + width; checkX += 3) {
                for (int checkY = y; checkY < y + height + 10; checkY++) {
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        if (prevY >= 0) {
                            heightVariation += Math.Abs(checkY - prevY);
                        }
                        prevY = checkY;
                        break;
                    }
                }
            }
            score -= Math.Min(30, heightVariation * 2);//地形起伏太大扣分

            //检查上方空间是否足够
            int obstructionCount = 0;
            for (int checkX = x; checkX < x + width; checkX += 4) {
                for (int checkY = y - 10; checkY < y + height / 2; checkY++) {
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        obstructionCount++;
                    }
                }
            }
            score -= Math.Min(20, obstructionCount);//上方有障碍物扣分

            //避开恶意生物群系
            for (int checkX = x; checkX < x + width; checkX += 10) {
                for (int checkY = y; checkY < y + height; checkY += 5) {
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile) {
                        //腐化、猩红、神圣等特殊地形
                        if (tile.TileType == TileID.CorruptGrass || tile.TileType == TileID.CrimsonGrass
                            || tile.TileType == TileID.HallowedGrass || tile.TileType == TileID.Ebonstone
                            || tile.TileType == TileID.Crimstone || tile.TileType == TileID.JungleGrass) {
                            return 0;//直接排除
                        }
                    }
                }
            }

            //检查是否有液体
            for (int checkX = x; checkX < x + width; checkX += 8) {
                for (int checkY = y; checkY < y + height; checkY += 4) {
                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.LiquidAmount > 50) {
                        score -= 15;//有液体扣分
                        break;
                    }
                }
            }

            return Math.Max(0, score);
        }

        /// <summary>
        /// 准备地形，清理建筑放置区域并自然融入周围
        /// </summary>
        private static void PrepareTerrainForOutpost(Point16 startPos, Point16 regionSize) {
            int x = startPos.X;
            int y = startPos.Y;
            int width = regionSize.X;
            int height = regionSize.Y;

            //清理建筑区域内的方块(保留底部两层作为地基融合)
            for (int px = x; px < x + width; px++) {
                for (int py = y; py < y + height - 2; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(px, py);
                    if (tile.HasTile) {
                        //清除方块但保留自然生成的树木根基附近的方块
                        WorldGen.KillTile(px, py, false, false, true);
                    }
                    //清除墙壁
                    if (tile.WallType > WallID.None && tile.WallType < WallID.Count) {
                        WorldGen.KillWall(px, py, false);
                    }
                }
            }

            //处理边缘融合，让建筑与周围地形自然过渡
            int blendRange = 8;

            //左侧边缘融合
            for (int bx = 0; bx < blendRange; bx++) {
                int worldX = x - blendRange + bx;
                if (!WorldGen.InWorld(worldX, y)) {
                    continue;
                }

                float blendFactor = bx / (float)blendRange;
                int clearHeight = (int)(height * (1 - blendFactor * 0.7f));

                for (int py = y; py < y + clearHeight; py++) {
                    if (!WorldGen.InWorld(worldX, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(worldX, py);
                    if (tile.HasTile && WorldGen.genRand.NextFloat() > blendFactor * 0.5f) {
                        WorldGen.KillTile(worldX, py, false, false, true);
                    }
                }
            }

            //右侧边缘融合
            for (int bx = 0; bx < blendRange; bx++) {
                int worldX = x + width + bx;
                if (!WorldGen.InWorld(worldX, y)) {
                    continue;
                }

                float blendFactor = 1 - bx / (float)blendRange;
                int clearHeight = (int)(height * (1 - blendFactor * 0.7f));

                for (int py = y; py < y + clearHeight; py++) {
                    if (!WorldGen.InWorld(worldX, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(worldX, py);
                    if (tile.HasTile && WorldGen.genRand.NextFloat() > blendFactor * 0.5f) {
                        WorldGen.KillTile(worldX, py, false, false, true);
                    }
                }
            }

            //在建筑底部补充泥土层确保地基稳固
            int groundY = y + height - 2;
            for (int px = x; px < x + width; px++) {
                for (int py = groundY; py < groundY + 4; py++) {
                    if (!WorldGen.InWorld(px, py)) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(px, py);
                    if (!tile.HasTile) {
                        WorldGen.PlaceTile(px, py, TileID.Dirt, true, true);
                    }
                }
            }

            //在地基顶部铺设草地
            for (int px = x - 3; px < x + width + 3; px++) {
                if (!WorldGen.InWorld(px, groundY - 1)) {
                    continue;
                }

                Tile surfaceTile = Framing.GetTileSafely(px, groundY);
                if (surfaceTile.HasTile && surfaceTile.TileType == TileID.Dirt) {
                    //检查上方是否露天
                    Tile aboveTile = Framing.GetTileSafely(px, groundY - 1);
                    if (!aboveTile.HasTile) {
                        surfaceTile.TileType = TileID.Grass;
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

            int chestNum = 0;
            foreach (var chestTag in regionSaveData.Chests) {
                chestNum++;
                ChestSaveData chestSaveData = ChestSaveData.FromTag(chestTag);
                //需要注意这里chestSaveData拿到的坐标只是相对坐标，所以需要加上orig
                int chestIndex = Chest.FindChest(orig.X + chestSaveData.X, orig.Y + chestSaveData.Y);
                if (chestIndex < 0) {
                    continue;
                }

                Chest chest = Main.chest[chestIndex];
                int slot = 0;

                //根据箱子序号放置不同的物品
                if (chestNum == 1) {
                    //第一个箱子放一些基础资源
                    chest.item[slot++] = new Item(ItemID.Wood, WorldGen.genRand.Next(50, 100));
                    chest.item[slot++] = new Item(ItemID.Acorn, WorldGen.genRand.Next(10, 20));
                    chest.item[slot++] = new Item(ItemID.Torch, WorldGen.genRand.Next(20, 40));
                    if (WorldGen.genRand.NextBool(3)) {
                        chest.item[slot++] = new Item(rareItems[WorldGen.genRand.Next(rareItems.Length)], 1);
                    }
                }
                else {
                    //其他箱子随机放置物品
                    int itemCount = WorldGen.genRand.Next(4, 8);
                    for (int i = 0; i < itemCount && slot < chest.item.Length; i++) {
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
}
