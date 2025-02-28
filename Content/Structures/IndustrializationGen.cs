using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using System.Collections.Generic;
using System;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Structures
{
    internal class IndustrializationGen
    {
        public static void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "正在让世界变得工业化";
            SpawnWindGrivenGenerator();
        }

        public static int GetWorldSize() {
            return Main.maxTilesX <= 4200 ? 1 : Main.maxTilesX <= 6400 ? 2 : Main.maxTilesX <= 8400 ? 3 : 1;
        }

        private static void SpawnWindGrivenGenerator() {
            Point16 targetPos = new Point16(WorldGen.genRand.Next(Main.maxTilesX / 2 - 300, Main.maxTilesX / 2 + 300), 0);

            int maxFindWidth = 600 + GetWorldSize() * 200;
            int maxFindHeight = 500;
            targetPos -= new Point16(maxFindWidth / 2, maxFindHeight / 2);
            int tileIsAirCount = 0;
            bool dontFindByY = false;
            Tile tile = default;

            List<Point16> scheduledPosList = [];

            for (int i = 0; i < maxFindWidth; i++) {
                for (int j = 0; j < maxFindHeight; j++) {
                    Point16 newPos = targetPos + new Point16(i, j);

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
                    WorldGen.PlaceTile(pos.X, pos.Y - 1, ModContent.TileType<WindGrivenGeneratorTile>());
                    oldPos = pos;
                }
            }
        }
    }
}
