using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using System.Collections.Generic;
using System;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using CalamityMod.Tiles.DraedonStructures;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.Structures
{
    internal class IndustrializationGen
    {
        public static void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = CWRLocText.Instance.IndustrializationGenMessage.Value;
            SpawnWindGrivenGenerator();
        }

        public static int GetWorldSize() {
            return Main.maxTilesX <= 4200 ? 1 : Main.maxTilesX <= 6400 ? 2 : Main.maxTilesX <= 8400 ? 3 : 1;
        }

        internal static void SpawnWindGrivenGenerator() {
            Point16 asteroidCoreTopPoint = default;
            int labHologramProjector = ModContent.TileType<LabHologramProjector>();
            for (int x = 0; x < Main.maxTilesX; x++) {
                for (int y = 0; y < 500; y++) {
                    Point16 newPoint = new Point16(x, y);
                    if (Framing.GetTileSafely(newPoint).TileType == labHologramProjector) {
                        asteroidCoreTopPoint = new Point16(newPoint.X, (short)0);
                    }
                }
            }
            
            int maxFindWidth = 600 + GetWorldSize() * 200;
            int maxFindHeight = 500;

            Point16 asteroidCoreTopPoint2 = asteroidCoreTopPoint;
            asteroidCoreTopPoint -= new Point16(maxFindWidth / 2, maxFindHeight / 2);
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

            Point16 mainPos = scheduledPosList.Count == 0 ? default : scheduledPosList[0]; // 初始化为第一个点

            foreach (var point in scheduledPosList) {
                if (Math.Abs(point.X - asteroidCoreTopPoint2.X) < Math.Abs(mainPos.X - asteroidCoreTopPoint2.X)) {
                    mainPos = point; // 选择 X 轴距离更小的点
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
                if (pos.Y == pos2.Y && pos2.Y == pos3.Y
                    && Framing.GetTileSafely(pos2).HasSolidTile() && Framing.GetTileSafely(pos3).HasSolidTile()
                    && Math.Abs(oldPos.X - pos.X) > 32) {
                    WorldGen.KillTile(pos.X, pos3.Y - 1);
                    WorldGen.KillTile(pos2.X, pos2.Y - 1);
                    WorldGen.KillTile(pos3.X, pos3.Y - 1);
                    Tile tileFind = Framing.GetTileSafely(pos);
                    tileFind.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(pos.X, pos.Y, tileFind.TileType);
                    tileFind = Framing.GetTileSafely(pos2);
                    tileFind.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(pos2.X, pos2.Y, tileFind.TileType);
                    tileFind = Framing.GetTileSafely(pos3);
                    tileFind.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(pos3.X, pos3.Y, tileFind.TileType);

                    if (pos != mainPos) {
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
                if (tileID <= 2 || y < 6) {
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
