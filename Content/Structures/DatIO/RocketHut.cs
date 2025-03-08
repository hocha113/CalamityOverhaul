using System.IO;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Structures.DatIO
{
    internal static class RocketHut
    {
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
                DatIOLoader.SetTile(reader, startPos);
            }
        }
    }
}
