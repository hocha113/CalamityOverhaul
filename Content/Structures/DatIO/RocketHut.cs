using InnoVault.GameSystem;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader.IO;
using Terraria.DataStructures;
using System.IO;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    internal class RocketHut : SaveStructure
    {
        public override string SavePath => Path.Combine(StructurePath, "RocketHut_v1.nbt");
        public override void Load() => Mod.EnsureFileFromMod("Content/Structures/DatIO/RocketHut_v1.nbt", SavePath);
        public override void SaveData(TagCompound tag) { }
        public override void LoadData(TagCompound tag) {
            LoadRegion(tag, GetSpawnPoint());
            TagCache.Invalidate(SavePath);//释放缓存
        }

        private static Point16 GetSpawnPoint() {
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
                        //如果 TileType 是非模组物块或者是岩浆等不适合的地形
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

            return new Point16(startPos.X, startPos.Y);
        }
    }
}
