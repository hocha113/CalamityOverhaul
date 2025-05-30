using CalamityMod.Items.Materials;
using CalamityMod.Tiles.DraedonStructures;
using CalamityMod.Walls.DraedonStructures;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    internal class JunkmanBase : ICWRLoader
    {
        private static Dictionary<ushort, ushort> SafeNewIDToBlockID = new();
        private static Dictionary<ushort, ushort> SafeBlockIDToNewID = new();
        void ICWRLoader.SetupData() => InitializeData();
        internal static void InitializeData() {
            SafeNewIDToBlockID.Clear();
            SafeBlockIDToNewID.Clear();

            // 注册所有 Tile 和 Wall 的映射
            AddMapping((ushort)0, 0);
            AddMapping((ushort)1, (ushort)ModContent.TileType<HazardChevronPanels>());
            AddMapping((ushort)2, (ushort)ModContent.TileType<LaboratoryPanels>());
            AddMapping((ushort)3, (ushort)92);
            AddMapping((ushort)4, (ushort)4);
            AddMapping((ushort)5, (ushort)ModContent.TileType<RustedPipes>());
            AddMapping((ushort)6, (ushort)ModContent.TileType<LaboratoryShelf>());
            AddMapping((ushort)7, (ushort)54);
            AddMapping((ushort)8, (ushort)336);
            AddMapping((ushort)9, (ushort)42);
            AddMapping((ushort)10, (ushort)ModContent.TileType<AgedLaboratoryElectricPanel>());
            AddMapping((ushort)11, (ushort)2687);
            AddMapping((ushort)12, (ushort)ModContent.TileType<AgedLaboratoryDoorClosed>());
            AddMapping((ushort)13, (ushort)ModContent.TileType<LaboratoryPipePlating>());
            AddMapping((ushort)14, (ushort)ModContent.TileType<LaboratoryPlating>());
            AddMapping((ushort)15, (ushort)ModContent.TileType<WGGCollectorTile>());
            AddMapping((ushort)16, (ushort)ModContent.TileType<LaboratoryScreen>());
            AddMapping((ushort)17, (ushort)ModContent.TileType<SecurityChestTile>());
            AddMapping((ushort)18, (ushort)ModContent.TileType<LaboratoryContainmentBox>());
            AddMapping((ushort)19, (ushort)ModContent.TileType<LaboratoryTerminal>());
            AddMapping((ushort)20, (ushort)ModContent.TileType<AgedLaboratoryScreen>());
            AddMapping((ushort)21, (ushort)ModContent.TileType<LaboratoryConsole>());
            AddMapping((ushort)22, (ushort)ModContent.TileType<AgedLaboratoryContainmentBox>());
            AddMapping((ushort)23, (ushort)93);

            AddMapping((ushort)24, (ushort)145);
            AddMapping((ushort)25, (ushort)ModContent.WallType<LaboratoryPlateBeam>());
            AddMapping((ushort)26, (ushort)ModContent.WallType<LaboratoryPanelWall>());
            AddMapping((ushort)27, (ushort)ModContent.WallType<LaboratoryPlatePillar>());
            AddMapping((ushort)28, (ushort)ModContent.WallType<HazardChevronWall>());
            AddMapping((ushort)29, (ushort)ModContent.WallType<RustedPlateBeam>());
            AddMapping((ushort)30, (ushort)ModContent.WallType<LaboratoryPlatingWall>());
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

            if (tileType > 0) {
                tile.TileType = tileType;
                tile.TileFrameX = frameX;
                tile.TileFrameY = frameY;
            }
        }

        public static void Spawn() {
            // 初始化起始位置
            Point startPos = new Point(Main.spawnTileX, Main.spawnTileY + 420 + (WorldGen.GetWorldSize() * 100));
            startPos.X += WorldGen.genRand.Next(-16, 16) * WorldGen.GetWorldSize() * 16;
            startPos.Y += 20 + (WorldGen.GetWorldSize() * 2) + WorldGen.genRand.Next(116);
            Create(startPos);
            LoadChest(startPos);
        }

        public static void LoadChest(Point startPos) {
            int chestID = ModContent.TileType<SecurityChestTile>();
            Point chestPos1 = startPos + new Point(90, 24);
            Point chestPos2 = startPos + new Point(99, 24);

            // 清除并放置箱子
            WorldGen.KillTile(chestPos1.X, chestPos1.Y);
            WorldGen.KillTile(chestPos2.X, chestPos2.Y);
            WorldGen.PlaceTile(chestPos1.X, chestPos1.Y, chestID);
            WorldGen.PlaceTile(chestPos2.X, chestPos2.Y, chestID);

            // 定义物品池
            int[] mineralItems = [
                ItemID.CopperOre, ItemID.TinOre, ItemID.IronOre, ItemID.LeadOre,
                ItemID.SilverOre, ItemID.TungstenOre, ItemID.GoldOre, ItemID.PlatinumOre,
                ItemID.JungleSpores, ItemID.Moonglow, ModContent.ItemType<DubiousPlating>()
                , ModContent.ItemType<MysteriousCircuitry>()
            ];

            int[] junkItems = [
                ItemID.OldShoe, ItemID.TinCan, ItemID.LesserHealingPotion, ItemID.EmptyBucket,
                ItemID.Rope, ItemID.Wood, ItemID.Bottle // 添加更多垃圾物品
            ];

            int[] miscItems = [
                ItemID.LesserHealingPotion, ItemID.Torch, ItemID.WoodenArrow,
                ItemID.Shuriken, ItemID.Glowstick, ItemID.CopperCoin,
                ItemID.Vine, ItemID.Stinger
            ];

            // 处理第一个箱子（偏向矿物）
            int chestIndex = Chest.FindChest(chestPos1.X, chestPos1.Y - 1);
            if (chestIndex >= 0) {
                Chest chest = Main.chest[chestIndex];
                int itemCount = Math.Min(WorldGen.genRand.Next(30, 36), chest.item.Length); // 随机 30-35 个物品
                for (int i = 0; i < itemCount; i++) {
                    int itemType;
                    int stackSize;
                    // 70% 矿物，20% 垃圾，10% 杂物
                    int rand = WorldGen.genRand.Next(100);
                    if (rand < 70) {
                        itemType = mineralItems[WorldGen.genRand.Next(mineralItems.Length)];
                        stackSize = WorldGen.genRand.Next(10, 51); // 矿物堆叠 10-50
                    }
                    else if (rand < 90) {
                        itemType = junkItems[WorldGen.genRand.Next(junkItems.Length)];
                        stackSize = WorldGen.genRand.Next(1, 11); // 垃圾堆叠 1-10
                    }
                    else {
                        itemType = miscItems[WorldGen.genRand.Next(miscItems.Length)];
                        stackSize = WorldGen.genRand.Next(5, 21); // 杂物堆叠 5-20
                    }

                    chest.item[i] = new Item(itemType, stackSize);
                }
            }

            // 处理第二个箱子（偏向垃圾）
            chestIndex = Chest.FindChest(chestPos2.X, chestPos2.Y - 1);
            if (chestIndex >= 0) {
                Chest chest = Main.chest[chestIndex];
                int itemCount = Math.Min(WorldGen.genRand.Next(30, 36), chest.item.Length); // 随机 30-35 个物品
                for (int i = 0; i < itemCount; i++) {
                    int itemType;
                    int stackSize;
                    // 60% 垃圾，30% 杂物，10% 矿物
                    int rand = WorldGen.genRand.Next(100);
                    if (rand < 60) {
                        itemType = junkItems[WorldGen.genRand.Next(junkItems.Length)];
                        stackSize = WorldGen.genRand.Next(1, 11); // 垃圾堆叠 1-10
                    }
                    else if (rand < 90) {
                        itemType = miscItems[WorldGen.genRand.Next(miscItems.Length)];
                        stackSize = WorldGen.genRand.Next(5, 21); // 杂物堆叠 5-20
                    }
                    else {
                        itemType = mineralItems[WorldGen.genRand.Next(mineralItems.Length)];
                        stackSize = WorldGen.genRand.Next(10, 51); // 矿物堆叠 10-50
                    }

                    chest.item[i] = new Item(itemType, stackSize);
                }
            }
        }

        public static void Create(Point startPos) {
            using var stream = CWRMod.Instance.GetFileStream("Content/Structures/DatIO/JunkmanBase.dat", true);
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
