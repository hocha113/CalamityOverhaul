using CalamityMod.Tiles.DraedonStructures;
using CalamityMod.Walls.DraedonStructures;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    internal class RocketHut : ICWRLoader
    {
        private static Dictionary<ushort, ushort> SafeNewIDToBlockID = new();
        private static Dictionary<ushort, ushort> SafeBlockIDToNewID = new();
        void ICWRLoader.SetupData() => InitializeData();
        internal static void InitializeData() {
            SafeNewIDToBlockID.Clear();
            SafeBlockIDToNewID.Clear();

            // 注册所有 Tile 和 Wall 的映射
            AddMapping((ushort)0, 0);
            AddMapping((ushort)1, (ushort)ModContent.TileType<LaboratoryPlating>());
            AddMapping((ushort)2, (ushort)ModContent.TileType<LaboratoryPipePlating>());
            AddMapping((ushort)3, (ushort)ModContent.TileType<HazardChevronPanels>());
            AddMapping((ushort)4, (ushort)ModContent.TileType<LaboratoryDoorClosed>());
            AddMapping((ushort)5, (ushort)ModContent.TileType<LaboratoryServer>());
            AddMapping((ushort)6, (ushort)2037);
            AddMapping((ushort)7, (ushort)ModContent.TileType<LaboratoryShelf>());
            AddMapping((ushort)8, (ushort)ModContent.TileType<RustedPipes>());
            AddMapping((ushort)9, (ushort)CWRMod.Instance.musicMod.Find<ModTile>("BioLabMusicBox").Type);
            AddMapping((ushort)10, (ushort)ModContent.WallType<HazardChevronWall>());
            AddMapping((ushort)11, (ushort)2687);
            AddMapping((ushort)12, (ushort)ModContent.WallType<LaboratoryPlatingWall>());
            AddMapping((ushort)13, (ushort)ModContent.WallType<LaboratoryPlatePillar>());
            AddMapping((ushort)14, (ushort)ModContent.WallType<LaboratoryPlateBeam>());
            AddMapping((ushort)15, (ushort)ModContent.TileType<ElectricMinRocketTile>());
            AddMapping((ushort)16, (ushort)165);
            AddMapping((ushort)17, (ushort)42);
            AddMapping((ushort)18, (ushort)ModContent.TileType<LaboratoryPanels>());
        }

        private static void AddMapping(ushort newID, ushort blockID) {
            SafeNewIDToBlockID[newID] = blockID;
            SafeBlockIDToNewID[blockID] = newID;
        }

        public static void WriteTile(BinaryWriter writer, Tile tile, Point offsetPoint) {
            writer.Write(offsetPoint.X);
            writer.Write(offsetPoint.Y);

            if (SafeBlockIDToNewID.TryGetValue(tile.WallType, out ushort wallID)) {
                writer.Write(wallID);
            }
            else {
                writer.Write((ushort)0);
            }


            writer.Write(tile.LiquidAmount);

            if (SafeBlockIDToNewID.TryGetValue(tile.TileType, out ushort tileID)) {
                writer.Write(tileID);
            }
            else {
                writer.Write((ushort)0);
            }

            // 处理其他属性
            writer.Write(tile.TileFrameX);
            writer.Write(tile.TileFrameY);
            writer.Write(tile.HasTile);
            writer.Write((byte)tile.Slope);
        }

        public static void ReadTile(BinaryReader reader, Point point) {
            int tilePosX = reader.ReadInt32() + point.X;
            int tilePosY = reader.ReadInt32() + point.Y;

            ushort wallType = 0;
            SafeNewIDToBlockID.TryGetValue(reader.ReadUInt16(), out wallType);

            byte liquidAmount = reader.ReadByte();

            ushort tileType = 0;
            SafeNewIDToBlockID.TryGetValue(reader.ReadUInt16(), out tileType);

            short frameX = reader.ReadInt16();
            short frameY = reader.ReadInt16();
            bool hasTile = reader.ReadBoolean();
            byte slope = reader.ReadByte();

            if (!WorldGen.InWorld(tilePosX, tilePosY)) {
                return;//如果不在世界里面，就不要试图进行设置
            }

            Tile tile = Main.tile[tilePosX, tilePosY];

            //只在需要设置墙的时候才会去覆盖他，如果是0，说明无墙，在小屋这种情况下不需要去设置
            if (wallType > 0) {
                tile.WallType = wallType;
            }
            
            tile.LiquidAmount = liquidAmount;

            //只在有墙或者有方块的时候才设置物块的存在性
            if (wallType > 0 || tileType > 0) {
                tile.HasTile = hasTile;
            }
            
            tile.Slope = (SlopeType)slope;

            tile.TileType = tileType;
            tile.TileFrameX = frameX;
            tile.TileFrameY = frameY;
        }

        public static void SpawnRocketHut() {
            Point startPos = new Point(Main.spawnTileX, Main.spawnTileY);
            startPos.X += WorldGen.genRand.Next(-16, 16);
            startPos.Y += 20 + (WorldGen.GetWorldSize() * 2) + WorldGen.genRand.Next(6);

            bool safe = false;
            int attempts = 0;

            while (!safe && attempts < 120) {
                safe = true;

                for (int i = 0; i < 16; i++) {
                    for (int j = 0; j < 10; j++) {
                        Tile tile = Framing.GetTileSafely(startPos + new Point(i, j));
                        // 如果 TileType 是非模组物块或者是岩浆等不适合的地形
                        if (tile.TileType >= TileID.Count || tile.LiquidType == LiquidID.Lava) {
                            safe = false;
                        }
                    }
                }

                if (!safe) {
                    startPos.Y--;//继续往上找合适的位置
                }

                attempts++;//确保循环不会卡住
            }

            Create(startPos);
        }

        public static void Create(Point startPos) {
            using var stream = CWRMod.Instance.GetFileStream("Content/Structures/DatIO/RocketHut.dat", true);
            if (stream == null) {
                CWRMod.Instance.Logger.Info("ERROR:RocketHut.Create在试图创建文件流时返回Null");
                return;
            }

            using BinaryReader reader = new BinaryReader(stream);
            int count = reader.ReadInt32();

            for (int x = 0; x < count; x++) {
                ReadTile(reader, startPos);
            }
        }
    }
}
