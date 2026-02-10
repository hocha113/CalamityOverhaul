using CalamityOverhaul.Content.UIs.OverhaulSettings;
using InnoVault.GameSystem;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    internal class RocketHut : SaveStructure
    {
        public override string SavePath => Path.Combine(StructurePath, "RocketHut_v1.nbt");
        public override void Load() => Mod.EnsureFileFromMod("Content/Structures/DatIO/RocketHut_v1.nbt", SavePath);
        public override void SaveData(TagCompound tag)
            => SaveRegion(tag, new Point16(4187, 576).GetRectangleFromPoints(new Point16(4202, 586)));
        public override void LoadData(TagCompound tag) {
            var density = WorldGenDensitySave.GetDensity("RocketHut");
            if (density == StructureDensity.Extinction) {
                TagCache.Invalidate(SavePath);
                return;
            }

            int spawnCount = density switch {
                StructureDensity.Rare => 1,
                StructureDensity.Normal => WorldGen.genRand.Next(1, 3),
                StructureDensity.Common => WorldGen.genRand.Next(2, 4),
                StructureDensity.Flood => WorldGen.genRand.Next(3, 6),
                StructureDensity.Everywhere => WorldGen.genRand.Next(5, 9),
                _ => 1
            };

            RegionSaveData region = tag.GetRegionSaveData();
            for (int i = 0; i < spawnCount; i++) {
                int offsetX = WorldGen.genRand.Next(-16, 16)
                    + (i > 0 ? WorldGen.genRand.Next(-200, 200) * (i + 1) : 0);
                int offsetY = 20 + (WorldGen.GetWorldSize() * 2) + WorldGen.genRand.Next(6)
                    + (i > 0 ? WorldGen.genRand.Next(20, 60) * i : 0);
                Point16 startPos = new(Main.spawnTileX + offsetX, Main.spawnTileY + offsetY);
                startPos = FindSafePlacement(region.Size, startPos, 300 + i * 80, 60, 10);
                SetChestItem(LoadRegion(region, startPos), startPos);
            }
            TagCache.Invalidate(SavePath);
        }

        private static void SetChestItem(RegionSaveData regionSaveData, Point16 orig) {
            foreach (var chestTag in regionSaveData.Chests) {
                ChestSaveData chestSaveData = ChestSaveData.FromTag(chestTag);
                //需要注意这里chestSaveData拿到的坐标只是相对坐标，所以需要加上orig
                int chestIndex = Chest.FindChest(orig.X + chestSaveData.X, orig.Y + chestSaveData.Y);
                if (chestIndex < 0) {
                    continue;
                }

                Chest chest = Main.chest[chestIndex];

                chest.item[0].stack = WorldGen.genRand.Next(12, 26);
                chest.item[1].stack = WorldGen.genRand.Next(12, 26);
                chest.item[2].stack = WorldGen.genRand.Next(155, 208);
            }
        }
    }
}
