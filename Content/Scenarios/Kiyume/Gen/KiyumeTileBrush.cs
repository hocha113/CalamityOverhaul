using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    //gen 期唯一 tile 写入口，pass 里散写 Main.tile 视为违规
    //gen 线程单线程使用，静态计数器供收尾日志。镜像 OldNetTileBrush，不引用
    internal static class KiyumeTileBrush
    {
        internal static long SolidWrites;
        internal static long ClearWrites;
        internal static long LiquidWrites;

        internal static void ResetForNewGen() {
            SolidWrites = ClearWrites = LiquidWrites = 0;
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

        //火把直写：不走 PlaceTile/SquareTileFrame，避免 gen 线程上触发坠落砖弹幕
        internal static void SetTorch(int x, int y) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Torches;
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
        }

        //空腔灌水：NormalUpdates=false 下液体不流动，靠构造性铺设定住
        internal static void SetWater(int x, int y, byte amount = 255) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.LiquidAmount = amount;
            tile.LiquidType = LiquidID.Water;
            LiquidWrites++;
        }

        //斜切砖：岸线/坡脚收角用
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

        internal static void SetWall(int x, int y, ushort wall) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Main.tile[x, y].WallType = wall;
        }

        //矩形内膛清空+刷墙，区间半开
        internal static void CarveRect(int left, int top, int right, int bottom, ushort wall = WallID.None) {
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
    }
}
