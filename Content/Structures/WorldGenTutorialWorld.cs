﻿using CalamityMod;
using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    public class WorldGenTutorialWorld : ModSystem
    {
        public static bool JustPressed(Keys key) {
            return Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
        }

        public override void PostUpdateWorld() {
            if (JustPressed(Keys.D1)) {
                TestMethod((int)Main.MouseWorld.X / 16, (int)Main.MouseWorld.Y / 16);
            }
        }

        private void TestMethod(int x, int y) {
            Point16 targetPos = new Point16(Main.maxTilesX / 2, 0);

            int maxFindWidth = 1000;
            int maxFindHeight = 500;
            targetPos -= new Point16(maxFindWidth / 2, maxFindHeight / 2);
            int tileIsAirCount = 0;
            bool dontFindByY = false;
            Tile tile = default;

            List<Point16> scheduledPosList = [];

            for (int i = 0; i < maxFindWidth; i++) {
                for (int j = 0; j < maxFindHeight; j++) {
                    Point16 newPos = targetPos + new Point16(i, j);

                    if (tile.IsTileSolid()) {
                        tileIsAirCount = 0;
                    }
                    else {
                        tileIsAirCount++;
                    }

                    tile = Framing.GetTileSafely(newPos);

                    if (tileIsAirCount > 12 && tile.IsTileSolid() && !dontFindByY) {
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
                    && Framing.GetTileSafely(pos2).IsTileSolid() && Framing.GetTileSafely(pos3).IsTileSolid()
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