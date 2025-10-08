using CalamityMod.Tiles.DraedonStructures;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Structures.DatIO;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Structures
{
    internal class IndustrializationGen
    {
        public static void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = CWRLocText.Instance.IndustrializationGenMessage.Value;
            SpawnWindGrivenGenerator();
            if (Main.getGoodWorld) {
                SpawnWGGCollectorTile();
            }
            JunkmanBase.DoLoad<JunkmanBase>();
            RocketHut.DoLoad<RocketHut>();
        }

        public static void Shuffle<T>(IList<T> list) {
            int n = list.Count;
            while (n > 1) {
                n--;
                int k = WorldGen.genRand.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void SpawnWGGCollectorTile() {
            int minY = (int)(Main.worldSurface + 50);
            int maxY = (int)(Main.maxTilesY * 0.6);
            int minX = 100;
            int maxX = Main.maxTilesX - 100;

            int wggCollectorTile = ModContent.TileType<WGGCollectorTile>();

            //支撑地块类型（用于判断底部是否稳定）
            int[] validGroundTiles = [
                TileID.Stone, TileID.Mud, TileID.JungleGrass,
                TileID.ClayBlock, TileID.Silt, TileID.Sandstone
            ];

            List<Point16> candidateSpots = new();

            //=== 第一步：收集所有可能放置的平地点 ===
            for (int x = minX; x < maxX - 2; x++) {
                for (int y = minY; y < maxY - 4; y++) {
                    bool valid = true;

                    //检查底部 3 个支撑块是否稳定
                    for (int i = 0; i < 3; i++) {
                        Point16 bottom = new(x + i, y + 1);
                        if (!WorldGen.InWorld(bottom.X, bottom.Y)) {
                            valid = false;
                            break;
                        }

                        Tile tile = Framing.GetTileSafely(bottom);
                        if (!tile.HasTile || !tile.HasSolidTile() || !validGroundTiles.Contains(tile.TileType)) {
                            valid = false;
                            break;
                        }
                    }

                    if (!valid) {
                        continue;
                    }

                    //检查 3x5 区域是否为空（用于建筑空间）
                    for (int i = 0; i < 3; i++) {
                        for (int j = -4; j <= 0; j++) {
                            Point16 check = new(x + i, y + j);
                            if (!WorldGen.InWorld(check.X, check.Y)) {
                                valid = false;
                                break;
                            }

                            Tile tile = Framing.GetTileSafely(check);
                            if (tile.HasTile && tile.HasSolidTile()) {
                                valid = false;
                                break;
                            }
                        }

                        if (!valid) {
                            break;
                        }
                    }

                    if (valid) {
                        candidateSpots.Add(new Point16(x, y));
                    }
                }
            }

            //=== 第二步：稀疏性筛选，过滤靠得太近的点位 ===
            List<Point16> sparseFiltered = new();
            int minDistance = 60; //曼哈顿距离最小值

            Shuffle(candidateSpots); //打乱点位以避免集中排序偏差

            foreach (var pos in candidateSpots) {
                bool tooClose = false;

                foreach (var existing in sparseFiltered) {
                    int dist = Math.Abs(pos.X - existing.X) + Math.Abs(pos.Y - existing.Y);
                    if (dist < minDistance) {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose) {
                    sparseFiltered.Add(pos);
                }
            }

            //=== 第三步：根据深度与地形做进一步筛选（丛林/深度优先） ===
            List<Point16> finalSpots = new();

            foreach (var pos in sparseFiltered) {
                Tile below = Framing.GetTileSafely(pos.X + 1, pos.Y + 1);
                bool isJungle = below.TileType == TileID.Mud || below.TileType == TileID.JungleGrass;

                //深度因子（越深越容易留下）
                float depth = (float)(pos.Y - minY) / (maxY - minY);
                float keepChance = 0.1f + depth * 0.9f; //0.1 ~ 1.0

                if (isJungle) {
                    keepChance += 0.2f; //丛林额外提升概率
                }

                if (Main.rand.NextFloat() < keepChance) {
                    finalSpots.Add(pos);
                }
            }

            //最多保留300个（世界级限制）
            if (finalSpots.Count > 300) {
                Shuffle(finalSpots);
                finalSpots = finalSpots.Take(300).ToList();
            }

            //=== 最后正式放置 ===
            foreach (var pos in finalSpots) {
                //清理区域
                for (int i = 0; i < 3; i++) {
                    for (int j = -4; j <= 0; j++) {
                        Point16 clear = new(pos.X + i, pos.Y + j);
                        if (WorldGen.InWorld(clear.X, clear.Y)) {
                            Tile tile = Framing.GetTileSafely(clear);
                            if (tile.HasTile && tile.HasSolidTile()) {
                                WorldGen.KillTile(clear.X, clear.Y, noItem: true);
                            }
                        }
                    }
                }

                //放置拾荒者（偏移：原点(1,3)）
                WorldGen.PlaceTile(pos.X + 1, pos.Y - 1, wggCollectorTile, mute: true);
            }
        }

        internal static void SpawnWindGrivenGenerator() {
            Point16 asteroidCoreTopPoint = new Point16(Main.maxTilesX / 2, 0);
            int labHologramProjector = ModContent.TileType<LabHologramProjector>();
            for (int x = 0; x < Main.maxTilesX; x++) {
                for (int y = 0; y < 500; y++) {
                    Point16 newPoint = new Point16(x, y);
                    if (Framing.GetTileSafely(newPoint).TileType == labHologramProjector) {
                        asteroidCoreTopPoint = new Point16(newPoint.X, (short)0);
                    }
                }
            }

            int maxFindWidth = 600 + WorldGen.GetWorldSize() * 200;
            int maxFindHeight = 150 + WorldGen.GetWorldSize() * 100;

            Point16 asteroidCoreTopPoint2 = asteroidCoreTopPoint;
            asteroidCoreTopPoint -= new Point16(maxFindWidth / 2, 0);
            int tileIsAirCount = 0;
            bool dontFindByY = false;
            Tile tile = default;

            List<Point16> scheduledPosList = [];

            for (int i = 0; i < maxFindWidth; i++) {
                for (int j = 0; j < maxFindHeight; j++) {
                    Point16 newPos = asteroidCoreTopPoint + new Point16(i, j);

                    if (tile.HasSolidTile()) {
                        tileIsAirCount = 0;
                    }
                    else {
                        tileIsAirCount++;
                    }

                    tile = Framing.GetTileSafely(newPos);

                    if (tileIsAirCount > 12 && tile.HasSolidTile() && !dontFindByY
                        && tile.TileType != 189 && tile.TileType != 460
                        && tile.TileType != 196 && tile.TileType != 202) {
                        scheduledPosList.Add(newPos);
                        dontFindByY = true;
                    }
                }
                dontFindByY = false;
            }

            Point16 mainPos = scheduledPosList.Count == 0 ? default : scheduledPosList[0]; //初始化为第一个点

            foreach (var point in scheduledPosList) {
                if (Math.Abs(point.X - asteroidCoreTopPoint2.X) < Math.Abs(mainPos.X - asteroidCoreTopPoint2.X)) {
                    mainPos = point; //选择 X 轴距离更小的点
                }
            }

            Point16 oldPos = default;

            for (int i = 0; i < scheduledPosList.Count; i++) {
                if (i == 0 || i == scheduledPosList.Count - 1) {
                    continue;
                }

                Point16 pos = scheduledPosList[i];
                Point16 pos2 = scheduledPosList[i - 1];
                Point16 pos3 = scheduledPosList[i + 1];

                if (!WorldGen.InWorld(pos.X, pos.Y)//检测这三个点是否在世界内
                    || !WorldGen.InWorld(pos2.X, pos2.Y)
                    || !WorldGen.InWorld(pos3.X, pos3.Y)) {
                    continue;
                }

                if (pos.Y == pos2.Y && pos2.Y == pos3.Y
                    && Framing.GetTileSafely(pos2).HasSolidTile() && Framing.GetTileSafely(pos3).HasSolidTile()
                    && Math.Abs(oldPos.X - pos.X) > 32) {
                    if (WorldGen.InWorld(pos.X, pos3.Y - 1)) {
                        WorldGen.KillTile(pos.X, pos3.Y - 1);
                    }
                    if (WorldGen.InWorld(pos2.X, pos2.Y - 1)) {
                        WorldGen.KillTile(pos2.X, pos2.Y - 1);
                    }
                    if (WorldGen.InWorld(pos3.X, pos3.Y - 1)) {
                        WorldGen.KillTile(pos3.X, pos3.Y - 1);
                    }
                    Tile tileFind = Framing.GetTileSafely(pos);
                    tileFind.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(pos.X, pos.Y, tileFind.TileType);
                    tileFind = Framing.GetTileSafely(pos2);
                    tileFind.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(pos2.X, pos2.Y, tileFind.TileType);
                    tileFind = Framing.GetTileSafely(pos3);
                    tileFind.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(pos3.X, pos3.Y, tileFind.TileType);

                    if (pos != mainPos && WorldGen.InWorld(pos.X, pos.Y - 1)) {
                        WorldGen.PlaceTile(pos.X, pos.Y - 1, ModContent.TileType<WGGWildernessTile>());
                    }
                    oldPos = pos;
                }
            }

            int maxExcavateY = 2;
            for (int z = -10; z < maxExcavateY + 2; z++) {
                for (int q = 0; q < 5; q++) {
                    Point16 newPos = mainPos + new Point16(q - 2, z - 1);
                    if (!WorldGen.InWorld(newPos.X, newPos.Y)) {
                        continue;
                    }
                    WorldGen.KillTile(newPos.X, newPos.Y);
                    WorldGen.KillWall(newPos.X, newPos.Y);
                }
            }

            //放置底座
            int laboratoryPipePlating = ModContent.TileType<LaboratoryPipePlating>();
            for (int z = 0; z < 2; z++) {
                for (int q = 0; q < 5; q++) {
                    Point16 newPos = mainPos + new Point16(q - 2, z + maxExcavateY - 1);
                    if (!WorldGen.InWorld(newPos.X, newPos.Y)) {
                        continue;
                    }
                    WorldGen.PlaceTile(newPos.X, newPos.Y, laboratoryPipePlating);
                }
            }

            //找到实验室镀板
            int laboratoryPlating = ModContent.TileType<LaboratoryPlating>();
            //放置管道
            int uePipelineTile = ModContent.TileType<UEPipelineTile>();
            for (int y = 0; y < 55; y++) {
                Point16 newPos = mainPos + new Point16(-3, y + maxExcavateY - 3);
                if (!WorldGen.InWorld(newPos.X, newPos.Y)) {
                    continue;
                }
                int tileID = Framing.GetTileSafely(newPos).TileType;
                if (y == 0) {
                    newPos = mainPos + new Point16(-2, y + maxExcavateY - 2);
                    WorldGen.PlaceTile(newPos.X, newPos.Y, uePipelineTile);
                }
                if (tileID <= 2 || y < 6 || tileID == laboratoryPlating) {
                    WorldGen.KillTile(newPos.X, newPos.Y);
                    WorldGen.PlaceTile(newPos.X, newPos.Y, uePipelineTile);
                }
            }

            //我不太清除为什么要减3，一般来讲减2就够了，可能是因为建筑太大的原因吧
            if (WorldGen.InWorld(mainPos.X, mainPos.Y + maxExcavateY - 3)) {
                WorldGen.PlaceTile(mainPos.X, mainPos.Y + maxExcavateY - 3, ModContent.TileType<WGGMK2WildernessTile>());
            }
        }
    }
}
