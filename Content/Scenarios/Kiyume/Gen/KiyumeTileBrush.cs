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

        //════════ 结构笔法（P3-A 追加）════════
        //家具/告示走原版放置函数：自带锚定校验，gen 线程安全性已被 Dungeonworld L2-L7 大规模验证

        //平台直写：frameX 留给收尾帧修，frameY 即平台样式（村落默认 KiyumeMetrics.PlatformFrameY）
        internal static void SetPlatform(int x, int y, short frameY) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Platforms;
            tile.TileFrameY = frameY;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
        }

        //绳链直写：只落进空格，遇实心即停手（镜像 L2 逃生通道成规）
        internal static void SetRope(int x, int y) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            if (tile.HasTile) {
                return;
            }
            tile.HasTile = true;
            tile.TileType = TileID.Rope;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.LiquidAmount = 0;
        }

        //单格放置+核对（镜像 L4Palette：落地后以场上出现为准，失败交调用方处置）
        internal static bool TryPlaceTile(int x, int y, int type, int style = 0) {
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        //多格家具锚定放置：纵向容错试探 2 格（沿 L4Palette 现值），拒绝交调用方处置
        internal static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }

        //告示牌+文本（镜像 L4Palette：PlaceSign→ReadSign→TextSign）
        internal static bool PlaceSignWithText(int x, int standRow, string text) {
            if (!WorldGen.PlaceSign(x, standRow, TileID.Signs)) {
                return false;
            }
            int sign = Sign.ReadSign(x, standRow);
            if (sign >= 0) {
                Sign.TextSign(sign, text);
            }
            return true;
        }
    }
}
