using CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors;
using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Structures.DatIO;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
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
            progress.Message = WorldGenSystem.IndustrializationGenMessage.Value;
            progress.Set(0f);

            //SHPC 坠舱空岛是武器的开局获取点，与灾厄无关，放在 CWRRef.Has 门外无条件尝试生成
            if (SHPCCradleGen.Enabled) {
                SHPCCradleGen.Generate();
            }
            progress.Set(0.2f);

            if (WorldGenDensitySave.GetDensity("WindGrivenGenerator") != StructureDensity.Extinction) {
                if (CWRRef.Has) {
                    //灾厄在场：以空岛实验室全息投影仪为锚点的原方案
                    SpawnWindGrivenGenerator();
                }
                else {
                    //无灾厄回退：程序化生成工业空岛承载风机
                    IndustrialSkyIslandGen.Generate();
                }
            }
            progress.Set(0.4f);
            if (CWRRef.Has && Main.getGoodWorld && WorldGenDensitySave.GetDensity("WGGCollector") != StructureDensity.Extinction) {
                SpawnWGGCollectorTile();
            }
            progress.Set(0.6f);

            //三座 DatIO 建筑已全原版化（_v2 NBT），不依赖灾厄，常态生成
            if (WorldGenDensitySave.GetDensity("JunkmanBase") != StructureDensity.Extinction) {
                JunkmanBase.DoLoad<JunkmanBase>();
            }
            progress.Set(0.75f);
            if (WorldGenDensitySave.GetDensity("RocketHut") != StructureDensity.Extinction) {
                RocketHut.DoLoad<RocketHut>();
            }
            progress.Set(0.9f);
            if (WorldGenDensitySave.GetDensity("SylvanOutpost") != StructureDensity.Extinction) {
                SylvanOutpost.DoLoad<SylvanOutpost>();
            }
            progress.Set(1f);
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

            //底部支撑
            int[] validGroundTiles = [
                TileID.Stone, TileID.Mud, TileID.JungleGrass,
                TileID.ClayBlock, TileID.Silt, TileID.Sandstone
            ];

            List<Point16> candidateSpots = new();

            //收集平地点
            for (int x = minX; x < maxX - 2; x++) {
                for (int y = minY; y < maxY - 4; y++) {
                    bool valid = true;

                    //底 3 块支撑
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

                    //3x5 空区
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

            //稀疏筛选
            List<Point16> sparseFiltered = new();
            float distanceFactor = WorldGenDensitySave.GetDistanceFactor("WGGCollector");
            int minDistance = (int)(60 * distanceFactor); //曼哈顿距离最小值，受密度等级影响

            Shuffle(candidateSpots); //打乱防排序扎堆

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

            //深度/丛林筛选
            List<Point16> finalSpots = new();

            foreach (var pos in sparseFiltered) {
                Tile below = Framing.GetTileSafely(pos.X + 1, pos.Y + 1);
                bool isJungle = below.TileType == TileID.Mud || below.TileType == TileID.JungleGrass;

                //越深越易留
                float depth = (float)(pos.Y - minY) / (maxY - minY);
                float keepChance = 0.1f + depth * 0.9f; //0.1 ~ 1.0

                if (isJungle) {
                    keepChance += 0.2f; //丛林额外提升概率
                }

                if (Main.rand.NextFloat() < keepChance) {
                    finalSpots.Add(pos);
                }
            }

            //上限跟密度
            float densityMultiplier = WorldGenDensitySave.GetMultiplier("WGGCollector");
            int maxCount = (int)(300 * densityMultiplier);
            if (maxCount <= 0) return;
            if (finalSpots.Count > maxCount) {
                Shuffle(finalSpots);
                finalSpots = finalSpots.Take(maxCount).ToList();
            }

            //正式放置
            foreach (var pos in finalSpots) {
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

                //拾荒者，原点偏移(1,3)
                WorldGen.PlaceTile(pos.X + 1, pos.Y - 1, wggCollectorTile, mute: true);
            }
        }

        internal static void SpawnWindGrivenGenerator() {
            Point16 asteroidCoreTopPoint = new Point16(Main.maxTilesX / 2, 0);
            int labHologramProjector = CWRID.Tile_LabHologramProjector;
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
                        && tile.TileType != TileID.Cloud && tile.TileType != TileID.SnowCloud && tile.TileType != TileID.RainCloud && tile.TileType != TileID.Sunplate) {
                        scheduledPosList.Add(newPos);
                        dontFindByY = true;
                    }
                }
                dontFindByY = false;
            }

            Point16 mainPos = scheduledPosList.Count == 0 ? default : scheduledPosList[0];

            foreach (var point in scheduledPosList) {
                if (Math.Abs(point.X - asteroidCoreTopPoint2.X) < Math.Abs(mainPos.X - asteroidCoreTopPoint2.X)) {
                    mainPos = point; //取更近 X
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

                float windGenDistanceFactor = WorldGenDensitySave.GetDistanceFactor("WindGrivenGenerator");
                int windGenMinSpacing = (int)(32 * windGenDistanceFactor);

                if (pos.Y == pos2.Y && pos2.Y == pos3.Y
                    && Framing.GetTileSafely(pos2).HasSolidTile() && Framing.GetTileSafely(pos3).HasSolidTile()
                    && Math.Abs(oldPos.X - pos.X) > windGenMinSpacing) {
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

            int laboratoryPipePlating = CWRID.Tile_LaboratoryPipePlating;
            for (int z = 0; z < 2; z++) {
                for (int q = 0; q < 5; q++) {
                    Point16 newPos = mainPos + new Point16(q - 2, z + maxExcavateY - 1);
                    if (!WorldGen.InWorld(newPos.X, newPos.Y)) {
                        continue;
                    }
                    WorldGen.PlaceTile(newPos.X, newPos.Y, laboratoryPipePlating);
                }
            }

            int laboratoryPlating = CWRID.Tile_LaboratoryPlating;
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

            //减3 原因未清，建筑偏大？
            if (WorldGen.InWorld(mainPos.X, mainPos.Y + maxExcavateY - 3)) {
                WorldGen.PlaceTile(mainPos.X, mainPos.Y + maxExcavateY - 3, ModContent.TileType<WGGMK2WildernessTile>());
            }
        }
    }
}
