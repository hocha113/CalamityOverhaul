using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen
{
    //====================================================================
    //基调层染:把层强调色刷到已开凿区的墙面与其贴邻砖面上。
    //
    //与"做旧签名"是两件事,别混:做旧管局部叙事痕迹(墨渍/焦痕/水线/锈垂),
    //层染管整层底色。层染只填未上漆的空白格,所以两者叠加时做旧永远在上,
    //调用顺序随便排都不会把已有痕迹洗掉。
    //
    //为什么用paint而不是换砖:原版只有蓝/绿/粉三种地牢砖,七层不够分;
    //而原版漆是按亮度重映射色相而非叠色,蓝砖刷棕漆会真的变成褐砖,
    //足以扛住层身份且零自制资产。paint是Tile上的独立字节字段(STRUCTURES F10),
    //不影响碰撞、群系计数与帧修。
    //
    //随机源=确定性块散列,零genRand消耗,不动P50既定的随机消耗顺序(R4)。
    //====================================================================
    internal static class LayerTint
    {
        internal readonly record struct TintReport(int Walls, int Tiles)
        {
            public override string ToString() => $"墙{Walls}/砖{Tiles}";
        }

        /// <summary>
        /// 区内层染:只认已开凿格(空格+本层地牢墙),染其墙面,并顺带染四邻实心砖面
        /// 玩家实际看得见的就是内壁这一圈,实心大陆在第一个判断就退出,不做无用扫描。
        /// </summary>
        /// <param name="coverage">粗块覆盖率0~100,决定层染占多大面(不是逐格概率)</param>
        /// <param name="salt">层专属盐,让各层斑形不撞</param>
        /// <param name="walls">本层地牢墙族(只染这几种,避开彩窗/栅栏等特殊墙)</param>
        /// <param name="bricks">本层砖族(只染这几种,避开家具与机件)</param>
        internal static TintReport Wash(Rectangle area, byte paint, int coverage, int salt,
            ushort[] walls, ushort[] bricks) {
            int wallCount = 0, tileCount = 0;
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile || !Contains(walls, tile.WallType) || !Hit(x, y, coverage, salt)) {
                        continue;
                    }
                    if (tile.WallColor == PaintID.None) {
                        tile.WallColor = paint;
                        wallCount++;
                    }
                    tileCount += PaintFace(x - 1, y, paint, bricks)
                        + PaintFace(x + 1, y, paint, bricks)
                        + PaintFace(x, y - 1, paint, bricks)
                        + PaintFace(x, y + 1, paint, bricks);
                }
            }
            return new TintReport(wallCount, tileCount);
        }

        /// <summary>
        /// 定点物块上色:把区内某种tile刷成指定漆(机件/特殊块从底色里跳出来用)。
        /// 同样只填未上漆的格,不覆盖做旧痕迹。
        /// </summary>
        internal static int PaintTilesOfType(Rectangle area, ushort type, byte paint) {
            int count = 0;
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == type && tile.TileColor == PaintID.None) {
                        tile.TileColor = paint;
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 确定性块斑判定,给"要成片不要噪点"的墙变体混斑共用(F32圆斑手法的零随机版)。
        /// 逐格掷骰会撒出椒盐噪点,块散列才能出连片的变体区。
        /// </summary>
        internal static bool BlockPatch(int x, int y, int coverage, int salt) => Hit(x, y, coverage, salt);

        private static int PaintFace(int x, int y, byte paint, ushort[] bricks) {
            if (!WorldGen.InWorld(x, y, 5)) {
                return 0;
            }
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || tile.TileColor != PaintID.None || !Contains(bricks, tile.TileType)) {
                return 0;
            }
            tile.TileColor = paint;
            return 1;
        }

        private static bool Contains(ushort[] set, ushort value) {
            foreach (ushort v in set) {
                if (v == value) {
                    return true;
                }
            }
            return false;
        }

        //粗块(8x8)定大形、细块(2x2)啃边，两级叠出参差边界而不是棋盘格
        private static bool Hit(int x, int y, int coverage, int salt) {
            int coarse = Hash(x >> 3, y >> 3, salt) % 100;
            if (coarse < coverage) {
                return true;
            }
            return coarse < coverage + 20 && Hash(x >> 1, y >> 1, salt ^ 0x9E37) % 100 < 45;
        }

        private static int Hash(int x, int y, int salt) {
            unchecked {
                int h = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791);
                h ^= h >> 13;
                h *= 0x5BD1E995;
                h ^= h >> 15;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
