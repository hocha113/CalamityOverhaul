using CalamityMod.Items.Materials;
using InnoVault.GameSystem;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    internal class JunkmanBase : SaveStructure
    {
        public override string SavePath => Path.Combine(StructurePath, "JunkmanBase_v1.nbt");
        public override void Load() => Mod.EnsureFileFromMod("Content/Structures/DatIO/JunkmanBase_v1.nbt", SavePath);
        public override void SaveData(TagCompound tag)
            => SaveRegion(tag, new Point16(4202, 989).GetRectangleFromPoints(new Point16(4392, 1024)));
        public override void LoadData(TagCompound tag) {
            RegionSaveData region = tag.GetRegionSaveData();
            //初始化起始位置
            Point16 startPos = new Point16(Main.spawnTileX + WorldGen.genRand.Next(-16, 16) * WorldGen.GetWorldSize() * 16
                , Main.spawnTileY + 420 + (WorldGen.GetWorldSize() * 100) + 20 + (WorldGen.GetWorldSize() * 2) + WorldGen.genRand.Next(116));
            startPos = FindSafePlacement(region.Size, startPos, 300, 300, 100, (Tile tile) => tile.TileType < TileID.Count && tile.LiquidAmount == 0);
            LoadChest(LoadRegion(region, startPos), startPos);
            TagCache.Invalidate(SavePath);//释放缓存
        }

        private static void LoadChest(RegionSaveData regionSaveData, Point16 orig) {
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

            int num = 0;
            foreach (var chestTag in regionSaveData.Chests) {
                num++;
                ChestSaveData chestSaveData = ChestSaveData.FromTag(chestTag);

                //需要注意这里chestSaveData拿到的坐标只是相对坐标，所以需要加上orig
                int chestIndex = Chest.FindChest(orig.X + chestSaveData.X, orig.Y + chestSaveData.Y);
                if (chestIndex < 0) {
                    continue;
                }

                //处理第一个箱子（偏向矿物）
                if (num == 1) {//运行第一个箱子时为1
                    Chest chest = Main.chest[chestIndex];
                    int itemCount = Math.Min(WorldGen.genRand.Next(30, 36), chest.item.Length); //随机 30-35 个物品
                    for (int i = 0; i < itemCount; i++) {
                        int itemType;
                        int stackSize;
                        //70% 矿物，20% 垃圾，10% 杂物
                        int rand = WorldGen.genRand.Next(100);
                        if (rand < 70) {
                            itemType = mineralItems[WorldGen.genRand.Next(mineralItems.Length)];
                            stackSize = WorldGen.genRand.Next(10, 51); //矿物堆叠 10-50
                        }
                        else if (rand < 90) {
                            itemType = junkItems[WorldGen.genRand.Next(junkItems.Length)];
                            stackSize = WorldGen.genRand.Next(1, 11); //垃圾堆叠 1-10
                        }
                        else {
                            itemType = miscItems[WorldGen.genRand.Next(miscItems.Length)];
                            stackSize = WorldGen.genRand.Next(5, 21); //杂物堆叠 5-20
                        }

                        chest.item[i] = new Item(itemType, stackSize);
                    }
                }
                else {//运行第两个箱子时为2
                    //处理第二个箱子（偏向垃圾）
                    Chest chest = Main.chest[chestIndex];
                    int itemCount = Math.Min(WorldGen.genRand.Next(30, 36), chest.item.Length); //随机 30-35 个物品
                    for (int i = 0; i < itemCount; i++) {
                        int itemType;
                        int stackSize;
                        // 60% 垃圾，30% 杂物，10% 矿物
                        int rand = WorldGen.genRand.Next(100);
                        if (rand < 60) {
                            itemType = junkItems[WorldGen.genRand.Next(junkItems.Length)];
                            stackSize = WorldGen.genRand.Next(1, 11); //垃圾堆叠 1-10
                        }
                        else if (rand < 90) {
                            itemType = miscItems[WorldGen.genRand.Next(miscItems.Length)];
                            stackSize = WorldGen.genRand.Next(5, 21); //杂物堆叠 5-20
                        }
                        else {
                            itemType = mineralItems[WorldGen.genRand.Next(mineralItems.Length)];
                            stackSize = WorldGen.genRand.Next(10, 51); //矿物堆叠 10-50
                        }

                        chest.item[i] = new Item(itemType, stackSize);
                    }
                }
            }
        }
    }
}
