using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //gen期唯一tile写入口(§4.4),pass里散写Main.tile视为违规
    //gen线程单线程使用,静态计数器供P80报告
    internal static class TileBrush
    {
        internal static long SolidWrites;
        internal static long ClearWrites;
        internal static long PlatformWrites;

        private static int _minX, _minY, _maxX, _maxY;

        internal static void ResetForNewGen() {
            SolidWrites = ClearWrites = PlatformWrites = 0;
            _minX = int.MaxValue;
            _minY = int.MaxValue;
            _maxX = int.MinValue;
            _maxY = int.MinValue;
        }

        /// <summary>已写区域,P80帧修范围</summary>
        internal static Rectangle WrittenBounds
            => _maxX < _minX ? Rectangle.Empty
            : new Rectangle(_minX, _minY, _maxX - _minX + 1, _maxY - _minY + 1);

        internal static void SetSolid(int x, int y, ushort type) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            //预览模式会盖进现存世界,残留slope/半砖必须归零
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
            SolidWrites++;
            Touch(x, y);
        }

        internal static void ClearCell(int x, int y, ushort wall) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
            ClearWrites++;
            Touch(x, y);
        }

        //斜切砖,拱角/坡道收角用(F24;垂直镜像对偶1↔3,2↔4见Prefab.FlipY)
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
            Touch(x, y);
        }

        //半砖(无垂直镜像对偶,镜像规则见Prefab.FlipY:换平台或删除)
        internal static void SetHalfBrick(int x, int y, ushort type) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = true;
            tile.LiquidAmount = 0;
            SolidWrites++;
            Touch(x, y);
        }

        //frameX留给P80的RangeFrame,frameY即平台样式
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
            Touch(x, y);
        }

        //矩形内膛清空+刷墙,区间半开
        internal static void CarveRect(int left, int top, int right, int bottom, ushort wall) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    ClearCell(x, y, wall);
                }
            }
        }

        internal static void PlatformRow(int left, int right, int y, short frameY) {
            for (int x = left; x < right; x++) {
                SetPlatform(x, y, frameY);
            }
        }

        private static void Touch(int x, int y) {
            if (x < _minX) _minX = x;
            if (x > _maxX) _maxX = x;
            if (y < _minY) _minY = y;
            if (y > _maxY) _maxY = y;
        }
    }
}
