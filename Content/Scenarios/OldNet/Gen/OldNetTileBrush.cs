using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen
{
    //gen期唯一tile写入口，pass里散写Main.tile视为违规
    //gen线程单线程使用，静态计数器供P80报告。镜像 Dungeonworld TileBrush，不引用
    internal static class OldNetTileBrush
    {
        internal static long SolidWrites;
        internal static long ClearWrites;
        internal static long PlatformWrites;

        internal static void ResetForNewGen() {
            SolidWrites = ClearWrites = PlatformWrites = 0;
        }

        internal static void SetSolid(int x, int y, ushort type) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
            SolidWrites++;
        }

        internal static void ClearCell(int x, int y, ushort wall = WallID.None) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
            ClearWrites++;
        }

        //斜切砖：拱角/坡道收角用
        internal static void SetSloped(int x, int y, ushort type, SlopeType slope) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            tile.IsHalfBlock = false;
            tile.Slope = slope;
            tile.LiquidAmount = 0;
            SolidWrites++;
        }

        //frameX留给P80的RangeFrame，frameY即平台样式
        internal static void SetPlatform(int x, int y, short frameY) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Platforms;
            tile.TileFrameY = frameY;
            tile.LiquidAmount = 0;
            PlatformWrites++;
        }

        //矩形内膛清空+刷墙，区间半开
        internal static void CarveRect(int left, int top, int right, int bottom, ushort wall) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    ClearCell(x, y, wall);
                }
            }
        }

        //矩形实心填充，区间半开
        internal static void FillRect(int left, int top, int right, int bottom, ushort type) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    SetSolid(x, y, type);
                }
            }
        }

        internal static void PlatformRow(int left, int right, int y, short frameY) {
            for (int x = left; x < right; x++) {
                SetPlatform(x, y, frameY);
            }
        }
    }
}
