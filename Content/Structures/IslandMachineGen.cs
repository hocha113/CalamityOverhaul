using CalamityMod.Items.Materials;
using CalamityMod.Tiles.DraedonStructures;
using CalamityMod.Tiles.Ores;
using CalamityMod.Tiles.Plates;
using CalamityMod.Walls;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Structures
{
    internal class IslandMachineGen : GenPass
    {
        public IslandMachineGen() : base("Terraria", 1) { }

        public static int Raycast(int x, int y) {
            while (Main.tile[x, y].HasSolidTile()) {
                y++;
            }
            return y;
        }
        public static void CreateIsland(Point position, int tileID, int tileID2, int size) {
            for (int i = -size / 2; i < size / 2; i++) {
                int repY = (size / 2) - Math.Abs(i);
                int num = repY / 5;
                repY += WorldGen.genRand.Next(size / 10);
                for (int j = -num; j < repY; j++) {
                    int newID = tileID;
                    if (repY - j < 6 && j > 3) {
                        newID = tileID2;
                    }
                    _ = WorldGen.PlaceTile(position.X + i, position.Y + j, newID);
                }
                int y = Raycast(position.X + i, position.Y - 5);
                _ = WorldGen.GrowTree(position.X + i, y);
            }
        }
        public static void SetLoot(int Item, Chest chest) {
            chest.item[0].SetDefaults(Item, false);
            chest.item[1].SetDefaults(ItemID.IronOre, false);
            chest.item[1].stack = WorldGen.genRand.Next(4, 6);
            Item item = chest.item[2];
            UnifiedRandom genRand = WorldGen.genRand;
            int[] array2 = [302, 2327, 2351, 304, 2329];
            item.SetDefaults(Utils.Next(genRand, array2), false);
            chest.item[2].stack = WorldGen.genRand.Next(1, 3);
            chest.item[3].SetDefaults(Utils.Next(WorldGen.genRand, [282, 286]), false);
            chest.item[3].stack = WorldGen.genRand.Next(15, 31);
            chest.item[4].SetDefaults(73, false);
            chest.item[4].stack = WorldGen.genRand.Next(1, 3);
        }
        public static void CreateHouses(int X, int Y, int type = 30, int sizeX = 10, int sizeY = 7) {
            int wallID = (ushort)ModContent.WallType<ExoPlatingWall>();
            for (int m = X; m < X + sizeX - 1; m++) {
                for (int n = Y; n < Y + sizeY; n++) {
                    Tile tile = Main.tile[m, n];
                    tile.HasTile = false;
                    tile.TileType = 0;
                }
            }
            for (int l = X + 1; l < X + sizeX - 2; l++) {
                for (int j2 = Y + 1; j2 < Y + sizeY - 1; j2++) {
                    if (WorldGen.genRand.Next(5) >= 1) {
                        Tile tile = Main.tile[l, j2];
                        tile.HasTile = false;
                        tile.TileType = 0;
                        WorldGen.PlaceWall(l, j2, wallID);
                    }
                }
            }
            for (int k = Y; k < Y + sizeY - 1; k++) {
                _ = WorldGen.PlaceTile(X, k, type);
                _ = WorldGen.PlaceTile(X + (sizeX - 2), k, type);
            }
            for (int j = X; j < X + sizeX - 2; j++) {
                _ = WorldGen.PlaceTile(j, Y, type);
                Tile tile = Main.tile[j, Y + (sizeY - 1)];
                tile.HasTile = true;
                tile.TileType = (ushort)type;
                WorldGen.KillTile(j, Y + (sizeY - 1));
            }

            WorldGen.PlaceTile(X + sizeX - 2, Y + sizeY - 1, type);

            int PlacementSuccess = WorldGen.PlaceChest(X + ((sizeX - 1) / 2), Y + sizeY - 2, (ushort)ModContent.TileType<AgedSecurityChestTile>(), notNearOtherChests: true);
            if (PlacementSuccess >= 0) {
                Chest chest = Main.chest[PlacementSuccess];
                SetLoot(ModContent.ItemType<SuspiciousScrap>(), chest);
            }

            int origTurretX = X + ((sizeX - 1) / 2) - 2;
            int origTurretY = Y + sizeY - 2;
            if (WorldGen.PlaceTile(origTurretX, origTurretY, ModContent.TileType<LaboratoryConsole>())) {
                TileObjectData data = TileObjectData.GetTileData(Main.tile[origTurretX, origTurretY]);
                if (data != null && VaultUtils.SafeGetTopLeft(origTurretX, origTurretY, out Point16 point)) {
                    for (int i = 0; i < data.Width; i++) {
                        for (int j = 0; j < data.Height; j++) {
                            Tile tile = Main.tile[point.X + i, point.Y + j];
                            tile.TileFrameX -= 36;
                        }
                    }
                }
            }
            origTurretX += 6;
            WorldGen.PlaceTile(origTurretX, origTurretY, ModContent.TileType<LaboratoryConsole>());
        }
        public static bool CanPlaceArea(Point center, int size) {
            for (int x = 0; x < size; x++) {
                for (int y = 0; y < size; y++) {
                    int tileX = center.X + x;
                    int tileY = center.Y + y;

                    // 确保不超出地图边界
                    if (!WorldGen.InWorld(tileX, tileY)) {
                        return false;
                    }

                    Tile tile = Main.tile[tileX, tileY];
                    if (tile.HasSolidTile()) {
                        return false;
                    }
                }
            }
            return true;
        }
        public static void DoPass(Point position, int islandTileID, int housesTileID, int oreTileID, int islengdSize = 40, int houseSize = 10) {
            CreateIsland(position, islandTileID, oreTileID, islengdSize);
            for (int FuckWorldGen = 0; FuckWorldGen < 6; FuckWorldGen++) {
                Point randompoint = new(position.X + WorldGen.genRand.Next(-30, 31), position.Y + WorldGen.genRand.Next(7, 42));
                WorldGen.TileRunner(randompoint.X, randompoint.Y, WorldGen.genRand.Next(5, 8), WorldGen.genRand.Next(6, 13)
                    , islandTileID, false, 0f, 0f, false, true);
            }
            Point newPos = position;
            newPos.X -= houseSize / 2 - 1;
            newPos.Y -= houseSize / 2;
            CreateHouses(newPos.X, newPos.Y, housesTileID, houseSize, houseSize);
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = VaultUtils.Translation("正在从破碎的虚空中凝聚岛屿...", "Islands are coalescing from the shattered void...");
            int isLandNum = WorldGen.GetWorldSize() * 4;
            int height = 120;
            Point center;
            int carteNum = 0;
            int safeCount = 0;
            progress.Set(0.1);
            while (carteNum < isLandNum && safeCount < 1000) {
                progress.Set(0.1 + carteNum / (double)carteNum);
                center = new Point((Main.maxTilesX / isLandNum * (carteNum)) + (Main.maxTilesX / 15 / 2) - 100, center.Y = height);
                center.X += WorldGen.genRand.Next(-260, 260);
                center.Y += WorldGen.genRand.Next(-10, 20);
                int size = 40 + WorldGen.genRand.Next(20);
                if (CanPlaceArea(center + new Point(10, -10), size + 10)) {
                    DoPass(center, ModContent.TileType<Navyplate>(), ModContent.TileType<PlagueContainmentCells>(), ModContent.TileType<AerialiteOre>(), size, 14);
                    carteNum++;
                }
                safeCount++;
            }
        }
    }
}
