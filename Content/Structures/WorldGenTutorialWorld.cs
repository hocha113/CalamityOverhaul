using Terraria.ModLoader;

namespace WorldGenTutorial
{
    public class WorldGenTutorialWorld : ModSystem
    {
        //public static bool JustPressed(Keys key) {
        //    return Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
        //}

        //public override void PostUpdateWorld() {
        //    if (JustPressed(Keys.D1))
        //        TestMethod((int)Main.MouseWorld.X / 16, (int)Main.MouseWorld.Y / 16);
        //}

        //// 检查指定中心点及周围区域是否为空
        //private bool CanPlaceArea(Point center, int size) {
        //    for (int x = 0; x < size; x++) {
        //        for (int y = 0; y < size; y++) {
        //            int tileX = center.X + x;
        //            int tileY = center.Y + y;

        //            // 确保不超出地图边界
        //            if (!WorldGen.InWorld(tileX, tileY)) {
        //                return false;
        //            }

        //            Tile tile = Main.tile[tileX, tileY];
        //            if (tile.HasSolidTile()) {
        //                return false;
        //            }
        //        }
        //    }
        //    return true;
        //}

        //private void TestMethod(int x, int y) {
        //    Dust.QuickBox(new Vector2(x, y) * 16, new Vector2(x + 1, y + 1) * 16, 2, Color.YellowGreen, null);
        //    int isLandNum = WorldGen.GetWorldSize() * 4;
        //    int height = 120;
        //    Point center;
        //    int carteNum = 0;
        //    int safeCount = 0;
        //    while (carteNum < isLandNum && safeCount < 1000) {
        //        center = new Point((Main.maxTilesX / isLandNum * (carteNum)) + (Main.maxTilesX / 15 / 2) - 100, center.Y = height);
        //        center.X += WorldGen.genRand.Next(-260, 260);
        //        center.Y += WorldGen.genRand.Next(-10, 20);
        //        int size = 40 + WorldGen.genRand.Next(20);
        //        if (CanPlaceArea(center + new Point(10, -10), size + 10)) {
        //            DoPass(center, ModContent.TileType<Navyplate>(), ModContent.TileType<PlagueContainmentCells>(), ModContent.TileType<AerialiteOre>(), size, 14);
        //            carteNum++;
        //        }
        //        safeCount++;
        //    }
        //}
    }
}