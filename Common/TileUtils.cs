using Terraria;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using static Terraria.WorldGen;
namespace CalamityOverhaul.Common
{
    //这里的代码来源于tModLoader源代码:
    //[https://github.com/tModLoader/tModLoader/blob/1.4.4/patches/tModLoader/Terraria/ModLoader/TileLoader.cs]
    //与[https://github.com/tModLoader/tModLoader/blob/1.4.4/patches/tModLoader/Terraria/WorldGen.TML.cs]
    //该部分的代码有一定的修改，用于解决物块挖掘使游戏崩溃的问题，代码的使用遵循TmodLoader所使用的[MIT license]规则:[https://github.com/tModLoader/tModLoader]

    // The following code is derived from the TModLoader source code:
    // [https://github.com/tModLoader/tModLoader/blob/1.4.4/patches/tModLoader/Terraria/ModLoader/TileLoader.cs]
    // and [https://github.com/tModLoader/tModLoader/blob/1.4.4/patches/tModLoader/Terraria/WorldGen.TML.cs]
    // This portion of the code has been modified to address an issue where block mining causes the game to crash.
    // The usage of this code follows the MIT license as specified by TModLoader:
    // [https://github.com/tModLoader/tModLoader]
    internal static class TileUtils
    {
        private static bool mergeUp;
        private static bool mergeDown;
        private static bool mergeLeft;
        private static bool mergeRight;
        private static bool IsActive(this Tile tile) => tile.HasTile;
        private static bool IsHalfBrick(this Tile tile) => tile.IsHalfBlock;
        private static void SetHalfBrick(this Tile tile, bool halfBrick) => tile.IsHalfBlock = halfBrick;
        private static void SetSlope(this Tile tile, byte slope) => tile.Slope = (SlopeType)slope;
        private static bool IsNactive(this Tile tile) => tile.HasUnactuatedTile;
        private static byte IsSlope(this Tile tile) => (byte)tile.Slope;
        private static bool IsBottomSlope(this Tile tile) {
            byte b = tile.IsSlope();
            if (b != 3) {
                return b == 4;
            }
            return true;
        }
        private static bool IsLeftSlope(this Tile tile) {
            byte b = tile.IsSlope();
            if (b != 2) {
                return b == 4;
            }
            return true;
        }
        private static bool IsTopSlope(this Tile tile) {
            byte b = tile.IsSlope();
            if (b != 1) {
                return b == 2;
            }
            return true;
        }
        private static bool IsRightSlope(this Tile tile) {
            byte b = tile.IsSlope();
            if (b != 1) {
                return b == 3;
            }
            return true;
        }
        private static int GetBlockType(this Tile tile) {
            if (tile.IsHalfBrick()) {
                return 1;
            }
            int num = tile.IsSlope();
            if (num > 0) {
                num++;
            }
            return num;
        }

        private static void CheckDoorClosed(int i, int j, Tile tileCache, int type) {
            if (destroyObject) {
                return;
            }
            int num = j;
            bool flag = false;
            int frameY = tileCache.TileFrameY;
            int num2 = frameY / 54;
            num2 += tileCache.TileFrameX / 54 * 36;
            num = j - frameY % 54 / 18;
            Tile tile = Main.tile[i, num - 1];
            Tile tile2 = Main.tile[i, num];
            Tile tile3 = Main.tile[i, num + 1];
            Tile tile4 = Main.tile[i, num + 2];
            Tile tile5 = Main.tile[i, num + 3];
            if (tile == null) {
                tile = default(Tile);
            }
            if (tile2 == null) {
                tile2 = default(Tile);
            }
            if (tile3 == null) {
                tile3 = default(Tile);
            }
            if (tile4 == null) {
                tile4 = default(Tile);
            }
            if (tile5 == null) {
                tile5 = default(Tile);
            }
            if (!SolidTile(tile)) {
                flag = true;
            }
            if (!SolidTile(tile5)) {
                flag = true;
            }
            if (!tile2.IsActive() || tile2.TileType != type) {
                flag = true;
            }
            if (!tile3.IsActive() || tile3.TileType != type) {
                flag = true;
            }
            if (!tile4.IsActive() || tile4.TileType != type) {
                flag = true;
            }
            if (flag) {
                destroyObject = true;
                bool num3 = TileLoader.Drop(i, j, type);
                KillTile(i, num);
                KillTile(i, num + 1);
                KillTile(i, num + 2);
                if (num3) {
                    DropDoorItem(i, j, num2);
                }
            }
            destroyObject = false;
        }

        private static void CheckDoorOpen(int i, int j, Tile tileCache) {
            if (destroyObject) {
                return;
            }
            int num = 0;
            int num2 = i;
            int num3 = j;
            short frameX = tileCache.TileFrameX;
            int frameY = tileCache.TileFrameY;
            int num4 = frameY / 54;
            num4 += tileCache.TileFrameX / 72 * 36;
            num3 = j - frameY % 54 / 18;
            bool flag = false;
            switch (frameX % 72) {
                case 0:
                    num2 = i;
                    num = 1;
                    break;
                case 18:
                    num2 = i - 1;
                    num = 1;
                    break;
                case 36:
                    num2 = i + 1;
                    num = -1;
                    break;
                case 54:
                    num2 = i;
                    num = -1;
                    break;
            }
            Tile tile = Main.tile[num2, num3 - 1];
            Tile tile2 = Main.tile[num2, num3 + 3];
            if (tile == null) {
                tile = default(Tile);
            }
            if (tile2 == null) {
                tile2 = default(Tile);
            }
            if (!SolidTile(tile) || !SolidTile(tile2)) {
                flag = true;
                destroyObject = true;
                if (TileLoader.Drop(i, j, tileCache.TileType)) {
                    DropDoorItem(i, j, num4);
                }
            }
            int num5 = num2;
            if (num == -1) {
                num5 = num2 - 1;
            }
            for (int k = num5; k < num5 + 2; k++) {
                for (int l = num3; l < num3 + 3; l++) {
                    if (!flag) {
                        Tile tile3 = Main.tile[k, l];
                        if (!tile3.IsActive() || tile3.TileType != 11) {
                            destroyObject = true;
                            if (TileLoader.Drop(i, j, tileCache.TileType)) {
                                DropDoorItem(i, j, num4);
                            }
                            flag = true;
                            k = num5;
                            l = num3;
                        }
                    }
                    if (flag) {
                        KillTile(k, l);
                    }
                }
            }
            destroyObject = false;
        }

        private static void SetFrameNumber(this Tile tile, byte frameNumber) => tile.TileFrameNumber = frameNumber;
        private static byte GetFrameNumber(this Tile tile) => (byte)tile.TileFrameNumber;
        private static bool GetInvisibleBlock(this Tile tile) => tile.IsTileInvisible;

        public static void DoErrorTile(Vector2 tilePos, Tile tile) {
            string errorT = "";
            string path = "steamapps\\common\\tModLoader\\tModLoader-Logs";
            string errorT_CN = $"格式化图格发生异常，位于地图坐标[{(int)tilePos.X}, {(int)tilePos.Y}]，如果需要寻求帮助，请附带上 {path} 文件夹下的client.log文件";
            string errorT_EN = $"An exception occurred in the formatted grid, located at map coordinates[{(int)tilePos.X}, {(int)tilePos.Y}] {path} subfolder 'client.log' File";
            errorT = CWRUtils.Translation(errorT_CN, errorT_EN);
            errorT.Domp(Color.Red);
            CWRMod.Instance.Logger.Info(errorT);
            $"At in InfinitePickProj.AI 69 line 'WorldGen.SquareTileFrame((int)tilePos.X, (int)tilePos.Y);', targetID: {tile.TileType}:{tile}".DompInConsole();
        }

        public static void CheckModTile(int i, int j, int type) {
            if (type <= TileID.Count || destroyObject) {
                return;
            }
            TileObjectData tileData = TileObjectData.GetTileData(type, 0);
            if (tileData == null) {
                return;
            }
            int frameX = Main.tile[i, j].TileFrameX;
            int frameY = Main.tile[i, j].TileFrameY;
            int subX = frameX / tileData.CoordinateFullWidth;
            int subY = frameY / tileData.CoordinateFullHeight;
            int wrap = tileData.StyleWrapLimit;
            if (wrap == 0) {
                wrap = 1;
            }

            int num = (tileData.StyleHorizontal ? (subY * wrap + subX) : (subX * wrap + subY));
            int style = num / tileData.StyleMultiplier;
            int alternate = num % tileData.StyleMultiplier;

            tileData = TileObjectData.GetTileData(type, style, alternate + 1);
            int partFrameX = frameX % tileData.CoordinateFullWidth;
            int partFrameY = frameY % tileData.CoordinateFullHeight;
            int partX = partFrameX / (tileData.CoordinateWidth + tileData.CoordinatePadding);
            int partY = 0;
            for (int remainingFrameY = partFrameY; partY + 1 < tileData.Height && remainingFrameY - tileData.CoordinateHeights[partY] - tileData.CoordinatePadding >= 0; partY++) {
                remainingFrameY -= tileData.CoordinateHeights[partY] + tileData.CoordinatePadding;
            }
            int originalI = i;
            int originalJ = j;
            i -= partX;
            j -= partY;
            int originX = i + tileData.Origin.X;
            int originY = j + tileData.Origin.Y;
            bool partiallyDestroyed = false;
            for (int x = i; x < i + tileData.Width; x++) {
                for (int y = j; y < j + tileData.Height; y++) {
                    if (!Main.tile[x, y].IsActive() || Main.tile[x, y].TileType != type) {
                        partiallyDestroyed = true;
                        break;
                    }
                }
                if (partiallyDestroyed) {
                    break;
                }
            }
            if (partiallyDestroyed || !TileObject.CanPlace(originX, originY, type, style, 0, out var _, onlyCheck: true, null, checkStay: true)) {
                destroyObject = true;

                for (int x = i; x < i + tileData.Width; x++) {
                    for (int y = j; y < j + tileData.Height; y++) {
                        if (Main.tile[x, y].TileType == type && Main.tile[x, y].IsActive()) {
                            KillTile(x, y);
                        }
                    }
                }
                TileLoader.KillMultiTile(i, j, frameX - partFrameX, frameY - partFrameY, type);
                destroyObject = false;
            }
            TileObject.objectPreview.Active = false;
        }

        public static void TileFrame(int i, int j, bool resetFrame = false, bool noBreak = false) {
            bool addToList = false;

            if (i > 5 && j > 5 && i < Main.maxTilesX - 5 && j < Main.maxTilesY - 5 && Main.tile[i, j] != null) {
                if (SkipFramingBecauseOfGen && !Main.tileFrameImportant[Main.tile[i, j].TileType]) {
                    return;
                }
                addToList = UpdateMapTile(i, j);
                Tile tile = Main.tile[i, j];
                if (!tile.IsActive()) {
                    tile.SetHalfBrick(halfBrick: false);
                    tile.ClearBlockPaintAndCoating();
                    tile.SetSlope(0);
                }
                if (tile.LiquidAmount > 0 && Main.netMode != NetmodeID.MultiplayerClient && !noLiquidCheck) {
                    Liquid.AddWater(i, j);
                }
                if (tile.IsActive()) {
                    if (!TileLoader.TileFrame(i, j, tile.TileType, ref resetFrame, ref noBreak) || (noBreak && Main.tileFrameImportant[tile.TileType] && !TileID.Sets.Torch[tile.TileType])) {
                        return;
                    }
                    int num = tile.TileType;
                    if (Main.tileStone[num]) {
                        num = 1;
                    }
                    int frameX = tile.TileFrameX;
                    int frameY = tile.TileFrameY;
                    Rectangle rectangle = new Rectangle(-1, -1, 0, 0);
                    Tile tile2;
                    Tile tile3;
                    Tile tile4;
                    Tile tile5;
                    Tile tile6;
                    Tile tile7;
                    Tile tile8;
                    Tile tile9;
                    int num50;
                    if (Main.tileFrameImportant[tile.TileType]) {
                        num50 = num;
                        switch (num50) {
                            case 518:
                                CheckLilyPad(i, j);
                                return;
                            case 519:
                                CheckCatTail(i, j);
                                return;
                            case 549:
                                CheckUnderwaterPlant(549, i, j);
                                return;
                            case 571:
                                CheckBamboo(i, j);
                                return;
                        }
                        if (!TileID.Sets.Torch[num]) {
                            switch (num50) {
                                case 442:
                                    CheckProjectilePressurePad(i, j);
                                    break;
                                case 136: {
                                    tile2 = Main.tile[i, j - 1];
                                    tile3 = Main.tile[i, j + 1];
                                    tile4 = Main.tile[i - 1, j];
                                    tile5 = Main.tile[i + 1, j];
                                    tile6 = Main.tile[i - 1, j + 1];
                                    tile7 = Main.tile[i + 1, j + 1];
                                    tile8 = Main.tile[i - 1, j - 1];
                                    tile9 = Main.tile[i + 1, j - 1];
                                    int num20 = -1;
                                    int num21 = -1;
                                    int num22 = -1;
                                    int tree = -1;
                                    int tree2 = -1;
                                    int tree3 = -1;
                                    int tree4 = -1;
                                    if (tile2 != null && tile2.IsNactive()) {
                                        _ = ref tile2.TileType;
                                    }
                                    if (tile3 != null && tile3.IsNactive() && !tile3.IsHalfBrick() && !tile3.IsTopSlope()) {
                                        num20 = tile3.TileType;
                                    }
                                    if (tile4 != null && tile4.IsNactive()) {
                                        num21 = tile4.TileType;
                                    }
                                    if (tile5 != null && tile5.IsNactive()) {
                                        num22 = tile5.TileType;
                                    }
                                    if (tile6 != null && tile6.IsNactive()) {
                                        tree = tile6.TileType;
                                    }
                                    if (tile7 != null && tile7.IsNactive()) {
                                        tree2 = tile7.TileType;
                                    }
                                    if (tile8 != null && tile8.IsNactive()) {
                                        tree3 = tile8.TileType;
                                    }
                                    if (tile9 != null && tile9.IsNactive()) {
                                        tree4 = tile9.TileType;
                                    }
                                    if (num20 >= 0 && Main.tileSolid[num20] && !Main.tileNoAttach[num20] && tile3 != null && !tile3.IsHalfBrick() && (tile3.IsSlope() == 0 || tile3.IsBottomSlope())) {
                                        tile.TileFrameX = 0;
                                    }
                                    else if ((num21 >= 0 && Main.tileSolid[num21] && !Main.tileNoAttach[num21] && tile4 != null && (tile4.IsLeftSlope() || tile4.IsSlope() == 0) && !tile4.IsHalfBrick()) || (num21 >= 0 && TileID.Sets.IsBeam[num21]) || (IsTreeType(num21) && IsTreeType(tree3) && IsTreeType(tree))) {
                                        tile.TileFrameX = 18;
                                    }
                                    else if ((num22 >= 0 && Main.tileSolid[num22] && !Main.tileNoAttach[num22] && tile5 != null && (tile5.IsRightSlope() || tile5.IsSlope() == 0) && !tile5.IsHalfBrick()) || (num22 >= 0 && TileID.Sets.IsBeam[num22]) || (IsTreeType(num22) && IsTreeType(tree4) && IsTreeType(tree2))) {
                                        tile.TileFrameX = 36;
                                    }
                                    else if (tile.WallType > 0) {
                                        tile.TileFrameX = 54;
                                    }
                                    else {
                                        KillTile(i, j);
                                    }
                                    break;
                                }
                                case 129:
                                case 149: {
                                    tile2 = Main.tile[i, j - 1];
                                    tile3 = Main.tile[i, j + 1];
                                    tile4 = Main.tile[i - 1, j];
                                    tile5 = Main.tile[i + 1, j];
                                    int num23 = -1;
                                    int num24 = -1;
                                    int num25 = -1;
                                    int num26 = -1;
                                    if (tile2 != null && tile2.IsNactive() && !tile2.IsBottomSlope()) {
                                        num24 = tile2.TileType;
                                    }
                                    if (tile3 != null && tile3.IsNactive() && !tile3.IsHalfBrick() && !tile3.IsTopSlope()) {
                                        num23 = tile3.TileType;
                                    }
                                    if (tile4 != null && tile4.IsNactive() && !tile5.IsRightSlope()) {
                                        num25 = tile4.TileType;
                                    }
                                    if (tile5 != null && tile5.IsNactive() && !tile5.IsLeftSlope()) {
                                        num26 = tile5.TileType;
                                    }
                                    if (num23 >= 0 && Main.tileSolid[num23] && !Main.tileSolidTop[num23]) {
                                        tile.TileFrameY = 0;
                                    }
                                    else if (num25 >= 0 && Main.tileSolid[num25] && !Main.tileSolidTop[num25]) {
                                        tile.TileFrameY = 54;
                                    }
                                    else if (num26 >= 0 && Main.tileSolid[num26] && !Main.tileSolidTop[num26]) {
                                        tile.TileFrameY = 36;
                                    }
                                    else if (num24 >= 0 && Main.tileSolid[num24] && !Main.tileSolidTop[num24]) {
                                        tile.TileFrameY = 18;
                                    }
                                    else {
                                        KillTile(i, j);
                                    }
                                    break;
                                }
                                default:
                                    if (num != 461) {
                                        switch (num) {
                                            case 178: {
                                                tile2 = Main.tile[i, j - 1];
                                                tile3 = Main.tile[i, j + 1];
                                                tile4 = Main.tile[i - 1, j];
                                                tile5 = Main.tile[i + 1, j];
                                                int num15 = -1;
                                                int num16 = -1;
                                                int num17 = -1;
                                                int num18 = -1;
                                                if (tile2 != null && tile2.IsNactive() && !tile2.IsBottomSlope()) {
                                                    num16 = tile2.TileType;
                                                }
                                                if (tile3 != null && tile3.IsNactive() && !tile3.IsHalfBrick() && !tile3.IsTopSlope()) {
                                                    num15 = tile3.TileType;
                                                }
                                                if (tile4 != null && tile4.IsNactive() && !tile4.IsHalfBrick() && !tile4.IsRightSlope()) {
                                                    num17 = tile4.TileType;
                                                }
                                                if (tile5 != null && tile5.IsNactive() && !tile5.IsHalfBrick() && !tile5.IsLeftSlope()) {
                                                    num18 = tile5.TileType;
                                                }
                                                if (num17 == 10) {
                                                    num17 = -1;
                                                }
                                                if (num18 == 10) {
                                                    num18 = -1;
                                                }
                                                short num19 = (short)(genRand.Next(3) * 18);
                                                if (num15 >= 0 && Main.tileSolid[num15] && !Main.tileSolidTop[num15]) {
                                                    if (tile.TileFrameY < 0 || tile.TileFrameY > 36) {
                                                        tile.TileFrameY = num19;
                                                    }
                                                }
                                                else if (num17 >= 0 && Main.tileSolid[num17] && !Main.tileSolidTop[num17]) {
                                                    if (tile.TileFrameY < 108 || tile.TileFrameY > 54) {
                                                        tile.TileFrameY = (short)(108 + num19);
                                                    }
                                                }
                                                else if (num18 >= 0 && Main.tileSolid[num18] && !Main.tileSolidTop[num18]) {
                                                    if (tile.TileFrameY < 162 || tile.TileFrameY > 198) {
                                                        tile.TileFrameY = (short)(162 + num19);
                                                    }
                                                }
                                                else if (num16 >= 0 && Main.tileSolid[num16] && !Main.tileSolidTop[num16]) {
                                                    if (tile.TileFrameY < 54 || tile.TileFrameY > 90) {
                                                        tile.TileFrameY = (short)(54 + num19);
                                                    }
                                                }
                                                else {
                                                    KillTile(i, j);
                                                }
                                                break;
                                            }
                                            case 184: {
                                                tile2 = Main.tile[i, j - 1];
                                                tile3 = Main.tile[i, j + 1];
                                                tile4 = Main.tile[i - 1, j];
                                                tile5 = Main.tile[i + 1, j];
                                                int num10 = -1;
                                                int num11 = -1;
                                                int num12 = -1;
                                                int num13 = -1;
                                                if (tile2 != null && tile2.IsActive() && !tile2.IsBottomSlope()) {
                                                    num11 = tile2.TileType;
                                                }
                                                if (tile3 != null && tile3.IsActive() && !tile3.IsHalfBrick() && !tile3.IsTopSlope()) {
                                                    num10 = tile3.TileType;
                                                }
                                                if (tile4 != null && tile4.IsActive()) {
                                                    num12 = tile4.TileType;
                                                }
                                                if (tile5 != null && tile5.IsActive()) {
                                                    num13 = tile5.TileType;
                                                }
                                                short num14 = (short)(genRand.Next(3) * 18);
                                                if (num10 >= 0 && GetTileMossColor(num10) != -1) {
                                                    tile.TileFrameX = (short)(22 * GetTileMossColor(num10));
                                                    if (tile.TileFrameY < 0 || tile.TileFrameY > 36) {
                                                        tile.TileFrameY = num14;
                                                    }
                                                }
                                                else if (num11 >= 0 && GetTileMossColor(num11) != -1) {
                                                    tile.TileFrameX = (short)(22 * GetTileMossColor(num11));
                                                    if (tile.TileFrameY < 54 || tile.TileFrameY > 90) {
                                                        tile.TileFrameY = (short)(54 + num14);
                                                    }
                                                }
                                                else if (num12 >= 0 && GetTileMossColor(num12) != -1) {
                                                    tile.TileFrameX = (short)(22 * GetTileMossColor(num12));
                                                    if (tile.TileFrameY < 108 || tile.TileFrameY > 144) {
                                                        tile.TileFrameY = (short)(108 + num14);
                                                    }
                                                }
                                                else if (num13 >= 0 && GetTileMossColor(num13) != -1) {
                                                    tile.TileFrameX = (short)(22 * GetTileMossColor(num13));
                                                    if (tile.TileFrameY < 162 || tile.TileFrameY > 198) {
                                                        tile.TileFrameY = (short)(162 + num14);
                                                    }
                                                }
                                                else {
                                                    KillTile(i, j);
                                                }
                                                break;
                                            }
                                            case 529:
                                                if (!SolidTileAllowBottomSlope(i, j + 1)) {
                                                    KillTile(i, j);
                                                    break;
                                                }
                                                tile3 = Main.tile[i, j + 1];
                                                _ = Main.tile[i, j].TileFrameY / 34;
                                                if (tile3 == null || !tile3.IsActive() || (tile3.TileType >= 0 && !TileID.Sets.Conversion.Sand[tile3.TileType])) {
                                                    KillTile(i, j);
                                                }
                                                break;
                                            case 3:
                                            case 24:
                                            case 61:
                                            case 71:
                                            case 73:
                                            case 74:
                                            case 110:
                                            case 113:
                                            case 201:
                                            case 637:
                                                PlantCheck(i, j);
                                                break;
                                            case 227:
                                                CheckDye(i, j);
                                                break;
                                            case 579:
                                                CheckRockGolemHead(i, j);
                                                break;
                                            case 12:
                                            case 31:
                                            case 639:
                                                CheckOrb(i, j, num);
                                                break;
                                            case 165:
                                                CheckTight(i, j);
                                                break;
                                            case 324:
                                                if (!SolidTileAllowBottomSlope(i, j + 1)) {
                                                    KillTile(i, j);
                                                }
                                                break;
                                            case 235:
                                                Check3x1(i, j, num);
                                                break;
                                            case 185:
                                                CheckPile(i, j);
                                                break;
                                            default:
                                                if (num != 296 && num != 297 && num != 309 && num != 358 && num != 359 && num != 413 && num != 414 && num != 542 && num != 550 && num != 551 && num != 553 && num != 554 && num != 558 && num != 559 && num != 599 && num != 600 && num != 601 && num != 602 && num != 603 && num != 604 && num != 605 && num != 606 && num != 607 && num != 608 && num != 609 && num != 610 && num != 611 && num != 612 && num != 632 && num != 640 && num != 643 && num != 644 && num != 645) {
                                                    if (num == 10) {
                                                        CheckDoorClosed(i, j, tile, num);
                                                        break;
                                                    }
                                                    if (num == 11) {
                                                        CheckDoorOpen(i, j, tile);
                                                        break;
                                                    }
                                                    if (num == 314) {
                                                        Minecart.FrameTrack(i, j, pound: false);
                                                        tile2 = Main.tile[i, j - 1];
                                                        tile3 = Main.tile[i, j + 1];
                                                        if (tile2 != null && tile2.TileType >= 0 && Main.tileRope[tile2.TileType]) {
                                                            TileFrame(i, j - 1);
                                                        }
                                                        if (tile3 != null && tile3.TileType >= 0 && Main.tileRope[tile3.TileType]) {
                                                            TileFrame(i, j + 1);
                                                        }
                                                        break;
                                                    }
                                                    if (num == 380) {
                                                        tile4 = Main.tile[i - 1, j];
                                                        if (tile4 == null) {
                                                            break;
                                                        }
                                                        tile5 = Main.tile[i + 1, j];
                                                        if (!(tile5 == null) && !(Main.tile[i - 1, j + 1] == null) && !(Main.tile[i + 1, j + 1] == null) && !(Main.tile[i - 1, j - 1] == null) && Main.tile[i + 1, j - 1] != null) {
                                                            int num2 = -1;
                                                            int num3 = -1;
                                                            if (tile4 != null && tile4.IsActive()) {
                                                                num3 = (Main.tileStone[tile4.TileType] ? 1 : tile4.TileType);
                                                            }
                                                            if (tile5 != null && tile5.IsActive()) {
                                                                num2 = (Main.tileStone[tile5.TileType] ? 1 : tile5.TileType);
                                                            }
                                                            if (num2 >= 0 && !Main.tileSolid[num2]) {
                                                                num2 = -1;
                                                            }
                                                            if (num3 >= 0 && !Main.tileSolid[num3]) {
                                                                num3 = -1;
                                                            }
                                                            if (num3 == num && num2 == num) {
                                                                rectangle.X = 18;
                                                            }
                                                            else if (num3 == num && num2 != num) {
                                                                rectangle.X = 36;
                                                            }
                                                            else if (num3 != num && num2 == num) {
                                                                rectangle.X = 0;
                                                            }
                                                            else {
                                                                rectangle.X = 54;
                                                            }
                                                            tile.TileFrameX = (short)rectangle.X;
                                                        }
                                                        break;
                                                    }
                                                    if (num >= 0 && TileID.Sets.Platforms[num]) {
                                                        tile4 = Main.tile[i - 1, j];
                                                        if (tile4 == null) {
                                                            break;
                                                        }
                                                        tile5 = Main.tile[i + 1, j];
                                                        if (tile5 == null) {
                                                            break;
                                                        }
                                                        tile6 = Main.tile[i - 1, j + 1];
                                                        if (tile6 == null) {
                                                            break;
                                                        }
                                                        tile7 = Main.tile[i + 1, j + 1];
                                                        if (tile7 == null) {
                                                            break;
                                                        }
                                                        tile8 = Main.tile[i - 1, j - 1];
                                                        if (tile8 == null) {
                                                            break;
                                                        }
                                                        tile9 = Main.tile[i + 1, j - 1];
                                                        if (tile9 == null) {
                                                            break;
                                                        }
                                                        int num4 = -1;
                                                        int num5 = -1;
                                                        if (tile4 != null && tile4.IsActive()) {
                                                            num5 = (Main.tileStone[tile4.TileType] ? 1 : ((!TileID.Sets.Platforms[tile4.TileType]) ? tile4.TileType : num));
                                                        }
                                                        if (tile5 != null && tile5.IsActive()) {
                                                            num4 = (Main.tileStone[tile5.TileType] ? 1 : ((!TileID.Sets.Platforms[tile5.TileType]) ? tile5.TileType : num));
                                                        }
                                                        if (num4 >= 0 && !Main.tileSolid[num4]) {
                                                            num4 = -1;
                                                        }
                                                        if (num5 >= 0 && !Main.tileSolid[num5]) {
                                                            num5 = -1;
                                                        }
                                                        if (num5 == num && tile4.IsHalfBrick() != tile.IsHalfBrick()) {
                                                            num5 = -1;
                                                        }
                                                        if (num4 == num && tile5.IsHalfBrick() != tile.IsHalfBrick()) {
                                                            num4 = -1;
                                                        }
                                                        if (num5 != -1 && num5 != num && tile.IsHalfBrick()) {
                                                            num5 = -1;
                                                        }
                                                        if (num4 != -1 && num4 != num && tile.IsHalfBrick()) {
                                                            num4 = -1;
                                                        }
                                                        if (num5 == -1 && tile8.IsActive() && tile8.TileType == num && tile8.IsSlope() == 1) {
                                                            num5 = num;
                                                        }
                                                        if (num4 == -1 && tile9.IsActive() && tile9.TileType == num && tile9.IsSlope() == 2) {
                                                            num4 = num;
                                                        }
                                                        if (num5 == num && tile4.IsSlope() == 2 && num4 != num) {
                                                            num4 = -1;
                                                        }
                                                        if (num4 == num && tile5.IsSlope() == 1 && num5 != num) {
                                                            num5 = -1;
                                                        }
                                                        if (tile.IsSlope() == 1) {
                                                            if (TileID.Sets.Platforms[tile5.TileType] && tile5.IsSlope() == 0 && !tile5.IsHalfBrick()) {
                                                                rectangle.X = 468;
                                                            }
                                                            else if (!tile7.IsActive() && (!TileID.Sets.Platforms[tile7.TileType] || tile7.IsSlope() == 2)) {
                                                                if (!tile4.IsActive() && (!TileID.Sets.Platforms[tile8.TileType] || tile8.IsSlope() != 1)) {
                                                                    rectangle.X = 432;
                                                                }
                                                                else {
                                                                    rectangle.X = 360;
                                                                }
                                                            }
                                                            else if (!tile4.IsActive() && (!TileID.Sets.Platforms[tile8.TileType] || tile8.IsSlope() != 1)) {
                                                                rectangle.X = 396;
                                                            }
                                                            else {
                                                                rectangle.X = 180;
                                                            }
                                                        }
                                                        else if (tile.IsSlope() == 2) {
                                                            if (TileID.Sets.Platforms[tile4.TileType] && tile4.IsSlope() == 0 && !tile4.IsHalfBrick()) {
                                                                rectangle.X = 450;
                                                            }
                                                            else if (!tile6.IsActive() && (!TileID.Sets.Platforms[tile6.TileType] || tile6.IsSlope() == 1)) {
                                                                if (!tile5.IsActive() && (!TileID.Sets.Platforms[tile9.TileType] || tile9.IsSlope() != 2)) {
                                                                    rectangle.X = 414;
                                                                }
                                                                else {
                                                                    rectangle.X = 342;
                                                                }
                                                            }
                                                            else if (!tile5.IsActive() && (!TileID.Sets.Platforms[tile9.TileType] || tile9.IsSlope() != 2)) {
                                                                rectangle.X = 378;
                                                            }
                                                            else {
                                                                rectangle.X = 144;
                                                            }
                                                        }
                                                        else if (num5 == num && num4 == num) {
                                                            if (tile4.IsSlope() == 2 && tile5.IsSlope() == 1) {
                                                                rectangle.X = 252;
                                                            }
                                                            else if (tile4.IsSlope() == 2) {
                                                                rectangle.X = 216;
                                                            }
                                                            else if (tile5.IsSlope() == 1) {
                                                                rectangle.X = 234;
                                                            }
                                                            else {
                                                                rectangle.X = 0;
                                                            }
                                                        }
                                                        else if (num5 == num && num4 == -1) {
                                                            if (tile4.IsSlope() == 2) {
                                                                rectangle.X = 270;
                                                            }
                                                            else {
                                                                rectangle.X = 18;
                                                            }
                                                        }
                                                        else if (num5 == -1 && num4 == num) {
                                                            if (tile5.IsSlope() == 1) {
                                                                rectangle.X = 288;
                                                            }
                                                            else {
                                                                rectangle.X = 36;
                                                            }
                                                        }
                                                        else if (num5 != num && num4 == num) {
                                                            rectangle.X = 54;
                                                        }
                                                        else if (num5 == num && num4 != num) {
                                                            rectangle.X = 72;
                                                        }
                                                        else if (num5 != num && num5 != -1 && num4 == -1) {
                                                            rectangle.X = 108;
                                                        }
                                                        else if (num5 == -1 && num4 != num && num4 != -1) {
                                                            rectangle.X = 126;
                                                        }
                                                        else {
                                                            rectangle.X = 90;
                                                        }
                                                        tile.TileFrameX = (short)rectangle.X;
                                                        if (Main.tile[i, j - 1] != null && Main.tileRope[Main.tile[i, j - 1].TileType]) {
                                                            TileFrame(i, j - 1);
                                                        }
                                                        if (Main.tile[i, j + 1] != null && Main.tileRope[Main.tile[i, j + 1].TileType]) {
                                                            TileFrame(i, j + 1);
                                                        }
                                                        break;
                                                    }
                                                    switch (num) {
                                                        case 233:
                                                        case 236:
                                                        case 238:
                                                            CheckJunglePlant(i, j, num);
                                                            return;
                                                        case 530:
                                                            CheckOasisPlant(i, j);
                                                            return;
                                                        case 240:
                                                        case 440:
                                                            Check3x3Wall(i, j);
                                                            return;
                                                        case 245:
                                                            Check2x3Wall(i, j);
                                                            return;
                                                        case 246:
                                                            Check3x2Wall(i, j);
                                                            return;
                                                        case 241:
                                                            Check4x3Wall(i, j);
                                                            return;
                                                        case 242:
                                                            Check6x4Wall(i, j);
                                                            return;
                                                        case 464:
                                                        case 466:
                                                            Check5x4(i, j, num);
                                                            return;
                                                        case 334:
                                                            CheckWeaponsRack(i, j);
                                                            return;
                                                        case 471:
                                                            TEWeaponsRack.Framing_CheckTile(i, j);
                                                            return;
                                                        case 34:
                                                        case 454:
                                                            CheckChand(i, j, num);
                                                            return;
                                                        case 547:
                                                        case 623:
                                                            Check2x5(i, j, num);
                                                            return;
                                                        case 548:
                                                        case 614:
                                                            Check3x6(i, j, num);
                                                            return;
                                                        case 613:
                                                            Check3x5(i, j, num);
                                                            return;
                                                        default: {
                                                            if (num == 354 || num == 406 || num == 412 || num == 355 || num == 452 || num == 455 || num == 491 || num == 499 || num == 642) {
                                                                break;
                                                            }
                                                            int num51 = num;
                                                            if (num51 != 15 && !TileID.Sets.TreeSapling[num]) {
                                                                switch (num51) {
                                                                    case 216:
                                                                    case 338:
                                                                    case 390:
                                                                    case 493:
                                                                    case 497:
                                                                    case 590:
                                                                    case 595:
                                                                    case 615:
                                                                        break;
                                                                    default:
                                                                        if (num < 391 || num > 394) {
                                                                            switch (num) {
                                                                                case 36:
                                                                                case 135:
                                                                                case 141:
                                                                                case 144:
                                                                                case 210:
                                                                                case 239:
                                                                                case 428:
                                                                                case 593:
                                                                                case 624:
                                                                                case 650:
                                                                                case 656:
                                                                                    Check1x1(i, j, num);
                                                                                    return;
                                                                                case 476:
                                                                                    CheckGolf1x1(i, j, num);
                                                                                    return;
                                                                                case 494:
                                                                                    CheckGolf1x1(i, j, num);
                                                                                    return;
                                                                                case 419:
                                                                                case 420:
                                                                                case 423:
                                                                                case 424:
                                                                                case 429:
                                                                                case 445:
                                                                                    CheckLogicTiles(i, j, num);
                                                                                    return;
                                                                                case 16:
                                                                                case 18:
                                                                                case 29:
                                                                                case 103:
                                                                                case 134:
                                                                                case 462:
                                                                                case 649:
                                                                                    Check2x1(i, j, (ushort)num);
                                                                                    return;
                                                                                case 13:
                                                                                case 33:
                                                                                case 49:
                                                                                case 50:
                                                                                case 78:
                                                                                case 174:
                                                                                case 372:
                                                                                case 646:
                                                                                    CheckOnTable1x1(i, j, num);
                                                                                    return;
                                                                                default:
                                                                                    if (TileID.Sets.BasicChest[num] && num < TileID.Count) {
                                                                                        CheckChest(i, j, num);
                                                                                        return;
                                                                                    }
                                                                                    switch (num) {
                                                                                        case 128:
                                                                                            CheckMan(i, j);
                                                                                            return;
                                                                                        case 269:
                                                                                            CheckWoman(i, j);
                                                                                            return;
                                                                                        case 470:
                                                                                            TEDisplayDoll.Framing_CheckTile(i, j);
                                                                                            return;
                                                                                        case 475:
                                                                                            TEHatRack.Framing_CheckTile(i, j);
                                                                                            return;
                                                                                        case 597:
                                                                                            TETeleportationPylon.Framing_CheckTile(i, j);
                                                                                            return;
                                                                                        case 27:
                                                                                            CheckSunflower(i, j);
                                                                                            return;
                                                                                        case 28:
                                                                                        case 653:
                                                                                            CheckPot(i, j, num);
                                                                                            return;
                                                                                        case 171:
                                                                                            CheckXmasTree(i, j);
                                                                                            return;
                                                                                        default:
                                                                                            if ((TileID.Sets.BasicChestFake[num] || num == 457) && num < TileID.Count) {
                                                                                                break;
                                                                                            }
                                                                                            switch (num) {
                                                                                                case 335:
                                                                                                case 411:
                                                                                                case 490:
                                                                                                case 564:
                                                                                                case 565:
                                                                                                case 594:
                                                                                                    Check2x2(i, j, num);
                                                                                                    return;
                                                                                                default:
                                                                                                    if (num >= 316 && num <= 318) {
                                                                                                        break;
                                                                                                    }
                                                                                                    switch (num) {
                                                                                                        case 376:
                                                                                                        case 443:
                                                                                                        case 444:
                                                                                                        case 485:
                                                                                                            CheckSuper(i, j, num);
                                                                                                            return;
                                                                                                        case 91:
                                                                                                            CheckBanner(i, j, (byte)num);
                                                                                                            return;
                                                                                                        case 35:
                                                                                                        case 139:
                                                                                                            CheckMB(i, j, (byte)num);
                                                                                                            return;
                                                                                                        case 386:
                                                                                                        case 387:
                                                                                                            CheckTrapDoor(i, j, num);
                                                                                                            return;
                                                                                                        case 388:
                                                                                                        case 389:
                                                                                                            CheckTallGate(i, j, num);
                                                                                                            return;
                                                                                                        case 92:
                                                                                                        case 93:
                                                                                                        case 453:
                                                                                                            Check1xX(i, j, (short)num);
                                                                                                            return;
                                                                                                        case 104:
                                                                                                        case 105:
                                                                                                        case 207:
                                                                                                        case 320:
                                                                                                        case 337:
                                                                                                        case 349:
                                                                                                        case 356:
                                                                                                        case 378:
                                                                                                        case 410:
                                                                                                        case 456:
                                                                                                        case 465:
                                                                                                        case 480:
                                                                                                        case 489:
                                                                                                        case 506:
                                                                                                        case 509:
                                                                                                        case 531:
                                                                                                        case 545:
                                                                                                        case 560:
                                                                                                        case 591:
                                                                                                        case 592:
                                                                                                        case 657:
                                                                                                        case 658:
                                                                                                        case 663:
                                                                                                            Check2xX(i, j, (ushort)num);
                                                                                                            return;
                                                                                                        case 101:
                                                                                                        case 102:
                                                                                                        case 463:
                                                                                                        case 617:
                                                                                                            Check3x4(i, j, num);
                                                                                                            return;
                                                                                                        case 42:
                                                                                                        case 270:
                                                                                                        case 271:
                                                                                                        case 572:
                                                                                                        case 581:
                                                                                                        case 660:
                                                                                                            Check1x2Top(i, j, (ushort)num);
                                                                                                            return;
                                                                                                        case 55:
                                                                                                        case 85:
                                                                                                        case 395:
                                                                                                        case 425:
                                                                                                        case 510:
                                                                                                        case 511:
                                                                                                        case 573:
                                                                                                            CheckSign(i, j, (ushort)num);
                                                                                                            return;
                                                                                                        case 520:
                                                                                                            CheckFoodPlatter(i, j, (ushort)num);
                                                                                                            return;
                                                                                                        case 209:
                                                                                                            CheckCannon(i, j, num);
                                                                                                            return;
                                                                                                        case 79:
                                                                                                        case 90:
                                                                                                        case 487:
                                                                                                            Check4x2(i, j, num);
                                                                                                            return;
                                                                                                        case 94:
                                                                                                        case 95:
                                                                                                        case 97:
                                                                                                        case 98:
                                                                                                        case 99:
                                                                                                        case 100:
                                                                                                        case 125:
                                                                                                        case 126:
                                                                                                        case 173:
                                                                                                        case 282:
                                                                                                        case 287:
                                                                                                        case 319:
                                                                                                        case 621:
                                                                                                        case 622:
                                                                                                            Check2x2(i, j, num);
                                                                                                            return;
                                                                                                        case 96:
                                                                                                            Check2x2Style(i, j, num);
                                                                                                            return;
                                                                                                        case 81: {
                                                                                                            tile2 = Main.tile[i, j - 1];
                                                                                                            tile3 = Main.tile[i, j + 1];
                                                                                                            _ = Main.tile[i - 1, j];
                                                                                                            _ = Main.tile[i + 1, j];
                                                                                                            int num8 = -1;
                                                                                                            int num9 = -1;
                                                                                                            if (tile2 != null && tile2.IsActive()) {
                                                                                                                num9 = tile2.TileType;
                                                                                                            }
                                                                                                            if (tile3 != null && tile3.IsActive()) {
                                                                                                                num8 = tile3.TileType;
                                                                                                            }
                                                                                                            if (num9 != -1) {
                                                                                                                KillTile(i, j);
                                                                                                            }
                                                                                                            else if (num8 < 0 || !Main.tileSolid[num8] || (tile3 != null && (tile3.IsHalfBrick() || tile3.IsTopSlope()))) {
                                                                                                                KillTile(i, j);
                                                                                                            }
                                                                                                            return;
                                                                                                        }
                                                                                                        default:
                                                                                                            if (Main.tileAlch[num]) {
                                                                                                                CheckAlch(i, j);
                                                                                                                return;
                                                                                                            }
                                                                                                            switch (num) {
                                                                                                                case 72: {
                                                                                                                    tile2 = Main.tile[i, j - 1];
                                                                                                                    tile3 = Main.tile[i, j + 1];
                                                                                                                    int num6 = -1;
                                                                                                                    int num7 = -1;
                                                                                                                    if (tile2 != null && tile2.IsActive()) {
                                                                                                                        num7 = tile2.TileType;
                                                                                                                    }
                                                                                                                    if (tile3 != null && tile3.IsActive()) {
                                                                                                                        num6 = tile3.TileType;
                                                                                                                    }
                                                                                                                    if (num6 != num && num6 != 70) {
                                                                                                                        KillTile(i, j);
                                                                                                                    }
                                                                                                                    else if (num7 != num && tile.TileFrameX == 0) {
                                                                                                                        tile.SetFrameNumber((byte)genRand.Next(3));
                                                                                                                        if (tile.GetFrameNumber() == 0) {
                                                                                                                            tile.TileFrameX = 18;
                                                                                                                            tile.TileFrameY = 0;
                                                                                                                        }
                                                                                                                        if (tile.GetFrameNumber() == 1) {
                                                                                                                            tile.TileFrameX = 18;
                                                                                                                            tile.TileFrameY = 18;
                                                                                                                        }
                                                                                                                        if (tile.GetFrameNumber() == 2) {
                                                                                                                            tile.TileFrameX = 18;
                                                                                                                            tile.TileFrameY = 36;
                                                                                                                        }
                                                                                                                    }
                                                                                                                    break;
                                                                                                                }
                                                                                                                case 5:
                                                                                                                    CheckTree(i, j);
                                                                                                                    break;
                                                                                                                case 583:
                                                                                                                case 584:
                                                                                                                case 585:
                                                                                                                case 586:
                                                                                                                case 587:
                                                                                                                case 588:
                                                                                                                case 589:
                                                                                                                    CheckTreeWithSettings(i, j, new CheckTreeSettings {
                                                                                                                        IsGroundValid = GemTreeGroundTest
                                                                                                                    });
                                                                                                                    break;
                                                                                                                case 596:
                                                                                                                    CheckTreeWithSettings(i, j, new CheckTreeSettings {
                                                                                                                        IsGroundValid = VanityTreeGroundTest
                                                                                                                    });
                                                                                                                    break;
                                                                                                                case 616:
                                                                                                                    CheckTreeWithSettings(i, j, new CheckTreeSettings {
                                                                                                                        IsGroundValid = VanityTreeGroundTest
                                                                                                                    });
                                                                                                                    break;
                                                                                                                case 634:
                                                                                                                    CheckTreeWithSettings(i, j, new CheckTreeSettings {
                                                                                                                        IsGroundValid = AshTreeGroundTest
                                                                                                                    });
                                                                                                                    break;
                                                                                                                case 323:
                                                                                                                    CheckPalmTree(i, j);
                                                                                                                    break;
                                                                                                                case 567:
                                                                                                                    CheckGnome(i, j);
                                                                                                                    break;
                                                                                                                case 630:
                                                                                                                case 631:
                                                                                                                    CheckStinkbugBlocker(i, j);
                                                                                                                    break;
                                                                                                            }
                                                                                                            CheckModTile(i, j, num);
                                                                                                            return;
                                                                                                        case 172:
                                                                                                        case 360:
                                                                                                        case 505:
                                                                                                        case 521:
                                                                                                        case 522:
                                                                                                        case 523:
                                                                                                        case 524:
                                                                                                        case 525:
                                                                                                        case 526:
                                                                                                        case 527:
                                                                                                        case 543:
                                                                                                        case 568:
                                                                                                        case 569:
                                                                                                        case 570:
                                                                                                        case 580:
                                                                                                        case 598:
                                                                                                        case 620:
                                                                                                        case 652:
                                                                                                        case 654:
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                                case 132:
                                                                                                case 138:
                                                                                                case 142:
                                                                                                case 143:
                                                                                                case 288:
                                                                                                case 289:
                                                                                                case 290:
                                                                                                case 291:
                                                                                                case 292:
                                                                                                case 293:
                                                                                                case 294:
                                                                                                case 295:
                                                                                                case 484:
                                                                                                case 664:
                                                                                                case 665:
                                                                                                    break;
                                                                                            }
                                                                                            Check2x2(i, j, num);
                                                                                            return;
                                                                                        case 254:
                                                                                            break;
                                                                                    }
                                                                                    Check2x2Style(i, j, num);
                                                                                    return;
                                                                                case 405:
                                                                                case 486:
                                                                                case 488:
                                                                                case 532:
                                                                                case 533:
                                                                                case 544:
                                                                                case 552:
                                                                                case 555:
                                                                                case 556:
                                                                                case 582:
                                                                                case 619:
                                                                                case 629:
                                                                                case 647:
                                                                                case 648:
                                                                                case 651:
                                                                                    break;
                                                                            }
                                                                        }
                                                                        goto case 14;
                                                                    case 14:
                                                                    case 17:
                                                                    case 26:
                                                                    case 77:
                                                                    case 86:
                                                                    case 87:
                                                                    case 88:
                                                                    case 89:
                                                                    case 114:
                                                                    case 133:
                                                                    case 186:
                                                                    case 187:
                                                                    case 215:
                                                                    case 217:
                                                                    case 218:
                                                                    case 237:
                                                                    case 244:
                                                                    case 285:
                                                                    case 286:
                                                                    case 298:
                                                                    case 299:
                                                                    case 310:
                                                                    case 339:
                                                                    case 361:
                                                                    case 362:
                                                                    case 363:
                                                                    case 364:
                                                                    case 377:
                                                                    case 469:
                                                                    case 538:
                                                                        Check3x2(i, j, (ushort)num);
                                                                        return;
                                                                }
                                                            }
                                                            Check1x2(i, j, (ushort)num);
                                                            return;
                                                        }
                                                        case 106:
                                                        case 212:
                                                        case 219:
                                                        case 220:
                                                        case 228:
                                                        case 231:
                                                        case 243:
                                                        case 247:
                                                        case 283:
                                                        case 300:
                                                        case 301:
                                                        case 302:
                                                        case 303:
                                                        case 304:
                                                        case 305:
                                                        case 306:
                                                        case 307:
                                                        case 308:
                                                            break;
                                                    }
                                                    Check3x3(i, j, (ushort)num);
                                                    break;
                                                }
                                                goto case 275;
                                            case 275:
                                            case 276:
                                            case 277:
                                            case 278:
                                            case 279:
                                            case 280:
                                            case 281:
                                                Check6x3(i, j, num);
                                                break;
                                        }
                                        break;
                                    }
                                    goto case 373;
                                case 373:
                                case 374:
                                case 375:
                                    tile2 = Main.tile[i, j - 1];
                                    if (tile2 == null || !tile2.IsActive() || tile2.IsBottomSlope() || !Main.tileSolid[tile2.TileType] || Main.tileSolidTop[tile2.TileType]) {
                                        KillTile(i, j);
                                    }
                                    break;
                            }
                        }
                        else {
                            CheckTorch(i, j);
                        }
                        return;
                    }
                    if ((num >= 255 && num <= 268) || num == 385 || (uint)(num - 446) <= 2u) {
                        Framing.SelfFrame8Way(i, j, tile, resetFrame);
                        return;
                    }
                    tile2 = Main.tile[i, j - 1];
                    tile3 = Main.tile[i, j + 1];
                    tile4 = Main.tile[i - 1, j];
                    tile5 = Main.tile[i + 1, j];
                    tile6 = Main.tile[i - 1, j + 1];
                    tile7 = Main.tile[i + 1, j + 1];
                    tile8 = Main.tile[i - 1, j - 1];
                    tile9 = Main.tile[i + 1, j - 1];
                    int upLeft = -1;
                    int up = -1;
                    int upRight = -1;
                    int left = -1;
                    int right = -1;
                    int downLeft = -1;
                    int down = -1;
                    int downRight = -1;
                    if (tile4 != null && tile4.IsActive()) {
                        left = (Main.tileStone[tile4.TileType] ? 1 : tile4.TileType);
                        if (tile4.IsSlope() == 1 || tile4.IsSlope() == 3) {
                            left = -1;
                        }
                    }
                    if (tile5 != null && tile5.IsActive()) {
                        right = (Main.tileStone[tile5.TileType] ? 1 : tile5.TileType);
                        if (tile5.IsSlope() == 2 || tile5.IsSlope() == 4) {
                            right = -1;
                        }
                    }
                    if (tile2 != null && tile2.IsActive()) {
                        up = (Main.tileStone[tile2.TileType] ? 1 : tile2.TileType);
                        if (tile2.IsSlope() == 3 || tile2.IsSlope() == 4) {
                            up = -1;
                        }
                    }
                    if (tile3 != null && tile3.IsActive()) {
                        down = (Main.tileStone[tile3.TileType] ? 1 : tile3.TileType);
                        if (tile3.IsSlope() == 1 || tile3.IsSlope() == 2) {
                            down = -1;
                        }
                    }
                    if (tile8 != null && tile8.IsActive()) {
                        upLeft = (Main.tileStone[tile8.TileType] ? 1 : tile8.TileType);
                    }
                    if (tile9 != null && tile9.IsActive()) {
                        upRight = (Main.tileStone[tile9.TileType] ? 1 : tile9.TileType);
                    }
                    if (tile6 != null && tile6.IsActive()) {
                        downLeft = (Main.tileStone[tile6.TileType] ? 1 : tile6.TileType);
                    }
                    if (tile7 != null && tile7.IsActive()) {
                        downRight = (Main.tileStone[tile7.TileType] ? 1 : tile7.TileType);
                    }
                    if (tile.IsSlope() == 2) {
                        up = -1;
                        left = -1;
                    }
                    if (tile.IsSlope() == 1) {
                        up = -1;
                        right = -1;
                    }
                    if (tile.IsSlope() == 4) {
                        down = -1;
                        left = -1;
                    }
                    if (tile.IsSlope() == 3) {
                        down = -1;
                        right = -1;
                    }
                    if (num == 668) {
                        num = 0;
                    }
                    TileMergeAttempt(0, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    num50 = num;
                    if (TileID.Sets.Snow[num]) {
                        TileMergeAttempt(num, Main.tileBrick, TileID.Sets.Ices, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    else if (!TileID.Sets.Ices[num]) {
                        if (num50 == 162) {
                            TileMergeAttempt(num, Main.tileBrick, TileID.Sets.IcesSnow, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        else if (Main.tileBrick[num]) {
                            int num51 = num;
                            if (!TileID.Sets.GrassSpecial[num]) {
                                if (num51 == 633) {
                                    TileMergeAttempt(num, Main.tileBrick, TileID.Sets.Ash, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                }
                                else {
                                    TileMergeAttempt(num, Main.tileBrick, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                }
                            }
                            else {
                                TileMergeAttempt(num, Main.tileBrick, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                            }
                        }
                        else if (Main.tilePile[num]) {
                            TileMergeAttempt(num, Main.tilePile, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                    }
                    else {
                        TileMergeAttempt(num, Main.tileBrick, TileID.Sets.Snow, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    if ((TileID.Sets.Stone[num] || Main.tileMoss[num]) && down == 165) {
                        if (tile3 != null && tile3.TileFrameY == 72) {
                            down = num;
                        }
                        else if (tile3 != null && tile3.TileFrameY == 0) {
                            down = num;
                        }
                    }
                    if ((TileID.Sets.Stone[num] || Main.tileMoss[num]) && up == 165) {
                        if (tile2 != null && tile2.TileFrameY == 90) {
                            up = num;
                        }
                        else if (tile2 != null && tile2.TileFrameY == 54) {
                            up = num;
                        }
                    }
                    if (num == 225) {
                        if (down == 165) {
                            down = num;
                        }
                        if (up == 165) {
                            up = num;
                        }
                    }
                    if ((TileID.Sets.Ices[num] || num == 147) && down == 165) {
                        down = num;
                    }
                    if ((tile.IsSlope() == 1 || tile.IsSlope() == 2) && down > -1 && !TileID.Sets.Platforms[down]) {
                        down = num;
                    }
                    if (up > -1 && tile2 != null && (tile2.IsSlope() == 1 || tile2.IsSlope() == 2) && !TileID.Sets.Platforms[up]) {
                        up = num;
                    }
                    if ((tile.IsSlope() == 3 || tile.IsSlope() == 4) && up > -1 && !TileID.Sets.Platforms[up]) {
                        up = num;
                    }
                    if (down > -1 && tile3 != null && (tile3.IsSlope() == 3 || tile3.IsSlope() == 4) && !TileID.Sets.Platforms[down]) {
                        down = num;
                    }
                    if (num == 124) {
                        if (up > -1 && Main.tileSolid[up] && !TileID.Sets.Platforms[up]) {
                            up = num;
                        }
                        if (down > -1 && Main.tileSolid[down] && !TileID.Sets.Platforms[down]) {
                            down = num;
                        }
                    }
                    if (up > -1 && tile2 != null && tile2.IsHalfBrick() && !TileID.Sets.Platforms[up]) {
                        up = num;
                    }
                    if (left > -1 && tile4 != null && tile4.IsHalfBrick()) {
                        if (tile.IsHalfBrick()) {
                            left = num;
                        }
                        else if (tile4.TileType != num) {
                            left = -1;
                        }
                    }
                    if (right > -1 && tile5 != null && tile5.IsHalfBrick()) {
                        if (tile.IsHalfBrick()) {
                            right = num;
                        }
                        else if (tile5.TileType != num) {
                            right = -1;
                        }
                    }
                    if (tile.IsHalfBrick()) {
                        if (left != num) {
                            left = -1;
                        }
                        if (right != num) {
                            right = -1;
                        }
                        up = -1;
                    }
                    if (tile3 != null && tile3.IsHalfBrick()) {
                        down = -1;
                    }
                    if (!Main.tileSolid[num]) {
                        switch (num) {
                            case 49:
                                CheckOnTable1x1(i, j, (byte)num);
                                return;
                            case 80:
                                CactusFrame(i, j);
                                return;
                        }
                    }
                    mergeUp = false;
                    mergeDown = false;
                    mergeLeft = false;
                    mergeRight = false;
                    int num27 = 0;
                    if (resetFrame) {
                        num27 = genRand.Next(0, 3);
                        tile.SetFrameNumber((byte)num27);
                    }
                    else {
                        num27 = tile.GetFrameNumber();
                    }
                    if (Main.tileLargeFrames[num] == 1) {
                        int num28 = j % 4;
                        int num29 = i % 3;
                        num27 = (new int[4, 3]
                        {
                            { 2, 4, 2 },
                            { 1, 3, 1 },
                            { 2, 2, 4 },
                            { 1, 1, 3 }
                        })[num28, num29] - 1;
                    }
                    if (Main.tileLargeFrames[num] == 2) {
                        int num52 = i % 2;
                        int num31 = j % 2;
                        num27 = num52 + num31 * 2;
                    }
                    if (!Main.tileRope[num] && TileID.Sets.BlockMergesWithMergeAllBlock[num]) {
                        TileMergeAttempt(num, Main.tileBlendAll, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    if (Main.tileBlendAll[num]) {
                        TileMergeAttempt(num, TileID.Sets.BlockMergesWithMergeAllBlock, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    if (TileID.Sets.ForcedDirtMerging[num]) {
                        TileMergeAttempt(num, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    if (TileID.Sets.Dirt[num]) {
                        if (up > -1 && Main.tileMergeDirt[up]) {
                            TileFrame(i, j - 1);
                            if (mergeDown) {
                                up = num;
                            }
                        }
                        else if (up >= 0 && TileID.Sets.Snow[up]) {
                            TileFrame(i, j - 1);
                            if (mergeDown) {
                                up = num;
                            }
                        }
                        if (down > -1 && Main.tileMergeDirt[down]) {
                            TileFrame(i, j + 1);
                            if (mergeUp) {
                                down = num;
                            }
                        }
                        else if (down >= 0 && TileID.Sets.Snow[down]) {
                            TileFrame(i, j + 1);
                            if (mergeUp) {
                                down = num;
                            }
                        }
                        if (left > -1 && Main.tileMergeDirt[left]) {
                            TileFrame(i - 1, j);
                            if (mergeRight) {
                                left = num;
                            }
                        }
                        else if (left >= 0 && TileID.Sets.Snow[left]) {
                            TileFrame(i - 1, j);
                            if (mergeRight) {
                                left = num;
                            }
                        }
                        if (right > -1 && Main.tileMergeDirt[right]) {
                            TileFrame(i + 1, j);
                            if (mergeLeft) {
                                right = num;
                            }
                        }
                        else if (right == 147) {
                            TileFrame(i + 1, j);
                            if (mergeLeft) {
                                right = num;
                            }
                        }
                        bool[] mergesWithDirtInASpecialWay = TileID.Sets.Conversion.MergesWithDirtInASpecialWay;
                        if (up > -1 && mergesWithDirtInASpecialWay[up]) {
                            up = num;
                        }
                        if (down > -1 && mergesWithDirtInASpecialWay[down]) {
                            down = num;
                        }
                        if (left > -1 && mergesWithDirtInASpecialWay[left]) {
                            left = num;
                        }
                        if (right > -1 && mergesWithDirtInASpecialWay[right]) {
                            right = num;
                        }
                        if (upLeft > -1 && Main.tileMergeDirt[upLeft]) {
                            upLeft = num;
                        }
                        else if (upLeft > -1 && mergesWithDirtInASpecialWay[upLeft]) {
                            upLeft = num;
                        }
                        if (upRight > -1 && Main.tileMergeDirt[upRight]) {
                            upRight = num;
                        }
                        else if (upRight > -1 && mergesWithDirtInASpecialWay[upRight]) {
                            upRight = num;
                        }
                        if (downLeft > -1 && Main.tileMergeDirt[downLeft]) {
                            downLeft = num;
                        }
                        else if (downLeft > -1 && mergesWithDirtInASpecialWay[downLeft]) {
                            downLeft = num;
                        }
                        if (downRight > -1 && Main.tileMergeDirt[downRight]) {
                            downRight = num;
                        }
                        else if (downRight > -1 && mergesWithDirtInASpecialWay[downRight]) {
                            downRight = num;
                        }
                        TileMergeAttempt(0, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        TileMergeAttempt(-2, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        TileMergeAttempt(0, 191, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        if (up > -1 && TileID.Sets.ForcedDirtMerging[up]) {
                            up = num;
                        }
                        if (down > -1 && TileID.Sets.ForcedDirtMerging[down]) {
                            down = num;
                        }
                        if (left > -1 && TileID.Sets.ForcedDirtMerging[left]) {
                            left = num;
                        }
                        if (right > -1 && TileID.Sets.ForcedDirtMerging[right]) {
                            right = num;
                        }
                        if (upLeft > -1 && TileID.Sets.ForcedDirtMerging[upLeft]) {
                            upLeft = num;
                        }
                        if (upRight > -1 && TileID.Sets.ForcedDirtMerging[upRight]) {
                            upRight = num;
                        }
                        if (downLeft > -1 && TileID.Sets.ForcedDirtMerging[downLeft]) {
                            downLeft = num;
                        }
                        if (downRight > -1 && TileID.Sets.ForcedDirtMerging[downRight]) {
                            downRight = num;
                        }
                    }
                    else if (Main.tileRope[num]) {
                        if (num != 504 && up != num && IsRope(i, j - 1)) {
                            up = num;
                        }
                        if (down != num && IsRope(i, j + 1)) {
                            down = num;
                        }
                        if (num != 504 && up > -1 && Main.tileSolid[up] && !Main.tileSolidTop[up]) {
                            up = num;
                        }
                        if (down > -1 && Main.tileSolid[down]) {
                            down = num;
                        }
                        if (num != 504 && up != num) {
                            if (left > -1 && Main.tileSolid[left]) {
                                left = num;
                            }
                            if (right > -1 && Main.tileSolid[right]) {
                                right = num;
                            }
                        }
                    }
                    else {
                        switch (num) {
                            case 53:
                                TileMergeAttemptFrametest(i, j, num, 397, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttemptFrametest(i, j, num, 396, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                break;
                            case 234:
                                TileMergeAttemptFrametest(i, j, num, 399, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttemptFrametest(i, j, num, 401, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                break;
                            case 112:
                                TileMergeAttemptFrametest(i, j, num, 398, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttemptFrametest(i, j, num, 400, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                break;
                            case 116:
                                TileMergeAttemptFrametest(i, j, num, 402, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttemptFrametest(i, j, num, 403, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                break;
                        }
                    }
                    if (Main.tileMergeDirt[num]) {
                        TileMergeAttempt(-2, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        if (num == 1) {
                            if ((double)j > Main.rockLayer) {
                                TileMergeAttemptFrametest(i, j, num, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                            }
                            TileMergeAttemptFrametest(i, j, num, TileID.Sets.Ash, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                    }
                    else {
                        num50 = num;
                        if (!TileID.Sets.HellSpecial[num]) {
                            if (num50 == 57) {
                                TileMergeAttempt(-2, 1, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttempt(num, 633, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttemptFrametest(i, j, num, TileID.Sets.HellSpecial, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                            }
                            else if (!TileID.Sets.Mud[num]) {
                                switch (num50) {
                                    case 211:
                                        TileMergeAttempt(59, 60, ref up, ref down, ref left, ref right);
                                        TileMergeAttempt(-2, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        break;
                                    case 225:
                                    case 226:
                                        TileMergeAttempt(-2, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        break;
                                    case 60:
                                        TileMergeAttempt(59, 211, ref up, ref down, ref left, ref right);
                                        break;
                                    case 189:
                                        TileMergeAttemptFrametest(i, j, num, TileID.Sets.MergesWithClouds, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        break;
                                    case 196:
                                        TileMergeAttempt(-2, 189, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        TileMergeAttempt(num, 460, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        break;
                                    case 460:
                                        TileMergeAttempt(-2, 189, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        TileMergeAttempt(num, 196, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        break;
                                    default:
                                        if (TileID.Sets.Snow[num]) {
                                            TileMergeAttemptFrametest(i, j, num, TileID.Sets.IcesSlush, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        }
                                        else if (!TileID.Sets.IcesSlush[num]) {
                                            switch (num50) {
                                                case 162:
                                                    TileMergeAttempt(-2, TileID.Sets.IcesSnow, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 32:
                                                    if (down == 23) {
                                                        down = num;
                                                    }
                                                    break;
                                                case 352:
                                                    if (down == 199) {
                                                        down = num;
                                                    }
                                                    break;
                                                case 69:
                                                    if (down == 60) {
                                                        down = num;
                                                    }
                                                    break;
                                                case 655:
                                                    if (down == 60) {
                                                        down = num;
                                                    }
                                                    break;
                                                case 51:
                                                    TileMergeAttempt(num, TileID.Sets.AllTiles, Main.tileNoAttach, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 192:
                                                    TileMergeAttemptFrametest(i, j, num, 191, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 191:
                                                    TileMergeAttempt(-2, 192, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttempt(num, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 384:
                                                    TileMergeAttemptFrametest(i, j, num, 383, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 383:
                                                    TileMergeAttempt(-2, 384, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttempt(num, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 407:
                                                    TileMergeAttempt(-2, 404, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 404:
                                                    TileMergeAttempt(-2, 396, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttemptFrametest(i, j, num, 407, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 397:
                                                    TileMergeAttempt(-2, 53, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttemptFrametest(i, j, num, 396, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 396:
                                                    TileMergeAttempt(-2, 397, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttempt(-2, 53, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttemptFrametest(i, j, num, 404, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 398:
                                                    TileMergeAttempt(-2, 112, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttemptFrametest(i, j, num, 400, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 400:
                                                    TileMergeAttempt(-2, 398, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttempt(-2, 112, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 399:
                                                    TileMergeAttempt(-2, 234, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttemptFrametest(i, j, num, 401, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 401:
                                                    TileMergeAttempt(-2, 399, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttempt(-2, 234, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 402:
                                                    TileMergeAttempt(-2, 116, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttemptFrametest(i, j, num, 403, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                                case 403:
                                                    TileMergeAttempt(-2, 402, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    TileMergeAttempt(-2, 116, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                                    break;
                                            }
                                        }
                                        else {
                                            TileMergeAttempt(-2, 147, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                        }
                                        break;
                                }
                            }
                            else {
                                if ((double)j > Main.rockLayer) {
                                    TileMergeAttempt(-2, 1, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                }
                                TileMergeAttempt(num, TileID.Sets.GrassSpecial, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                TileMergeAttemptFrametest(i, j, num, TileID.Sets.JungleSpecial, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                if ((double)j < Main.rockLayer) {
                                    TileMergeAttemptFrametest(i, j, num, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                }
                                else {
                                    TileMergeAttempt(num, TileID.Sets.Dirt, ref up, ref down, ref left, ref right);
                                }
                            }
                        }
                        else {
                            TileMergeAttempt(-2, TileID.Sets.Ash, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                    }
                    if (num == 0) {
                        TileMergeAttempt(num, Main.tileMoss, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        TileMergeAttempt(num, TileID.Sets.tileMossBrick, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    else if (Main.tileMoss[num] || TileID.Sets.tileMossBrick[num]) {
                        TileMergeAttempt(num, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    else if (Main.tileStone[num] || num == 1) {
                        TileMergeAttempt(num, Main.tileMoss, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    else if (num == 38) {
                        TileMergeAttempt(num, TileID.Sets.tileMossBrick, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    if (TileID.Sets.Conversion.Grass[num]) {
                        TileMergeAttempt(num, TileID.Sets.Ore, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    else if (TileID.Sets.Ore[num]) {
                        TileMergeAttempt(num, TileID.Sets.Conversion.Grass, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    if (num >= 0 && TileID.Sets.Mud[num]) {
                        TileMergeAttempt(num, TileID.Sets.OreMergesWithMud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    else if (TileID.Sets.OreMergesWithMud[num]) {
                        TileMergeAttempt(num, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    bool flag = false;
                    TileMergeCullCache tileMergeCullCache = default(TileMergeCullCache);
                    if (!Main.ShouldShowInvisibleWalls()) {
                        bool flag2 = tile.GetInvisibleBlock();
                        tileMergeCullCache.CullTop |= tile2 != null && tile2.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullBottom |= tile3 != null && tile3.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullLeft |= tile4 != null && tile4.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullRight |= tile5 != null && tile5.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullTopLeft |= tile8 != null && tile8.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullTopRight |= tile9 != null && tile9.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullBottomLeft |= tile6 != null && tile6.GetInvisibleBlock() != flag2;
                        tileMergeCullCache.CullBottomRight |= tile7 != null && tile7.GetInvisibleBlock() != flag2;
                    }
                    if (TileID.Sets.Grass[num] || TileID.Sets.GrassSpecial[num] || Main.tileMoss[num] || TileID.Sets.NeedsGrassFraming[num] || TileID.Sets.tileMossBrick[num]) {
                        flag = true;
                        TileMergeAttemptWeird(num, -1, Main.tileSolid, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        int num32 = TileID.Sets.NeedsGrassFramingDirt[num];
                        if (TileID.Sets.GrassSpecial[num]) {
                            num32 = 59;
                        }
                        else if (Main.tileMoss[num]) {
                            num32 = 1;
                        }
                        else if (TileID.Sets.tileMossBrick[num]) {
                            num32 = 38;
                        }
                        else {
                            switch (num) {
                                case 2:
                                case 477:
                                    TileMergeAttempt(num32, 23, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                    break;
                                case 23:
                                    TileMergeAttempt(num32, 2, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                                    break;
                            }
                        }
                        tileMergeCullCache.Cull(ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        if (up != num && up != num32 && (down == num || down == num32)) {
                            if (left == num32 && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 198;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 198;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 198;
                                        break;
                                }
                            }
                            else if (left == num && right == num32) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 198;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 198;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 198;
                                        break;
                                }
                            }
                        }
                        else if (down != num && down != num32 && (up == num || up == num32)) {
                            if (left == num32 && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 216;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 216;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 216;
                                        break;
                                }
                            }
                            else if (left == num && right == num32) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 216;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 216;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 216;
                                        break;
                                }
                            }
                        }
                        else if (left != num && left != num32 && (right == num || right == num32)) {
                            if (up == num32 && down == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 72;
                                        rectangle.Y = 144;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 162;
                                        break;
                                    default:
                                        rectangle.X = 72;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (down == num && up == num32) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 72;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 108;
                                        break;
                                    default:
                                        rectangle.X = 72;
                                        rectangle.Y = 126;
                                        break;
                                }
                            }
                        }
                        else if (right != num && right != num32 && (left == num || left == num32)) {
                            if (up == num32 && down == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 90;
                                        rectangle.Y = 144;
                                        break;
                                    case 1:
                                        rectangle.X = 90;
                                        rectangle.Y = 162;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (down == num && right == up) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 90;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 90;
                                        rectangle.Y = 108;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 126;
                                        break;
                                }
                            }
                        }
                        else if (up == num && down == num && left == num && right == num) {
                            if (upLeft != num && upRight != num && downLeft != num && downRight != num) {
                                if (downRight == num32) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 108;
                                            rectangle.Y = 324;
                                            break;
                                        case 1:
                                            rectangle.X = 126;
                                            rectangle.Y = 324;
                                            break;
                                        default:
                                            rectangle.X = 144;
                                            rectangle.Y = 324;
                                            break;
                                    }
                                }
                                else if (upRight == num32) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 108;
                                            rectangle.Y = 342;
                                            break;
                                        case 1:
                                            rectangle.X = 126;
                                            rectangle.Y = 342;
                                            break;
                                        default:
                                            rectangle.X = 144;
                                            rectangle.Y = 342;
                                            break;
                                    }
                                }
                                else if (downLeft == num32) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 108;
                                            rectangle.Y = 360;
                                            break;
                                        case 1:
                                            rectangle.X = 126;
                                            rectangle.Y = 360;
                                            break;
                                        default:
                                            rectangle.X = 144;
                                            rectangle.Y = 360;
                                            break;
                                    }
                                }
                                else if (upLeft == num32) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 108;
                                            rectangle.Y = 378;
                                            break;
                                        case 1:
                                            rectangle.X = 126;
                                            rectangle.Y = 378;
                                            break;
                                        default:
                                            rectangle.X = 144;
                                            rectangle.Y = 378;
                                            break;
                                    }
                                }
                                else {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 144;
                                            rectangle.Y = 234;
                                            break;
                                        case 1:
                                            rectangle.X = 198;
                                            rectangle.Y = 234;
                                            break;
                                        default:
                                            rectangle.X = 252;
                                            rectangle.Y = 234;
                                            break;
                                    }
                                }
                            }
                            else if (upLeft != num && downRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 306;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 306;
                                        break;
                                    default:
                                        rectangle.X = 72;
                                        rectangle.Y = 306;
                                        break;
                                }
                            }
                            else if (upRight != num && downLeft != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 90;
                                        rectangle.Y = 306;
                                        break;
                                    case 1:
                                        rectangle.X = 108;
                                        rectangle.Y = 306;
                                        break;
                                    default:
                                        rectangle.X = 126;
                                        rectangle.Y = 306;
                                        break;
                                }
                            }
                            else if (upLeft != num && upRight == num && downLeft == num && downRight == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (upLeft == num && upRight != num && downLeft == num && downRight == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (upLeft == num && upRight == num && downLeft != num && downRight == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                            else if (upLeft == num && upRight == num && downLeft == num && downRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                        }
                        else if (up == num && down == num32 && left == num && right == num && upLeft == -1 && upRight == -1) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 108;
                                    rectangle.Y = 18;
                                    break;
                                case 1:
                                    rectangle.X = 126;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 144;
                                    rectangle.Y = 18;
                                    break;
                            }
                        }
                        else if (up == num32 && down == num && left == num && right == num && downLeft == -1 && downRight == -1) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 108;
                                    rectangle.Y = 36;
                                    break;
                                case 1:
                                    rectangle.X = 126;
                                    rectangle.Y = 36;
                                    break;
                                default:
                                    rectangle.X = 144;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up == num && down == num && left == num32 && right == num && upRight == -1 && downRight == -1) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 198;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 198;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 198;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up == num && down == num && left == num && right == num32 && upLeft == -1 && downLeft == -1) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 180;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 180;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 180;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up == num && down == num32 && left == num && right == num) {
                            if (upRight != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (upLeft != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                        }
                        else if (up == num32 && down == num && left == num && right == num) {
                            if (downRight != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                            else if (downLeft != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                        }
                        else if (up == num && down == num && left == num && right == num32) {
                            if (upLeft != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                            else if (downLeft != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                        }
                        else if (up == num && down == num && left == num32 && right == num) {
                            if (upRight != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                            else if (downRight != -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                        }
                        else if ((up == num32 && down == num && left == num && right == num) || (up == num && down == num32 && left == num && right == num) || (up == num && down == num && left == num32 && right == num) || (up == num && down == num && left == num && right == num32)) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 18;
                                    rectangle.Y = 18;
                                    break;
                                case 1:
                                    rectangle.X = 36;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 54;
                                    rectangle.Y = 18;
                                    break;
                            }
                        }
                        if ((up == num || up == num32) && (down == num || down == num32) && (left == num || left == num32) && (right == num || right == num32)) {
                            if (upLeft != num && upLeft != num32 && (upRight == num || upRight == num32) && (downLeft == num || downLeft == num32) && (downRight == num || downRight == num32)) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (upRight != num && upRight != num32 && (upLeft == num || upLeft == num32) && (downLeft == num || downLeft == num32) && (downRight == num || downRight == num32)) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 180;
                                        break;
                                }
                            }
                            else if (downLeft != num && downLeft != num32 && (upLeft == num || upLeft == num32) && (upRight == num || upRight == num32) && (downRight == num || downRight == num32)) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                            else if (downRight != num && downRight != num32 && (upLeft == num || upLeft == num32) && (downLeft == num || downLeft == num32) && (upRight == num || upRight == num32)) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 162;
                                        break;
                                }
                            }
                        }
                        if (up != num32 && up != num && down == num && left != num32 && left != num && right == num && downRight != num32 && downRight != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 90;
                                    rectangle.Y = 270;
                                    break;
                                case 1:
                                    rectangle.X = 108;
                                    rectangle.Y = 270;
                                    break;
                                default:
                                    rectangle.X = 126;
                                    rectangle.Y = 270;
                                    break;
                            }
                        }
                        else if (up != num32 && up != num && down == num && left == num && right != num32 && right != num && downLeft != num32 && downLeft != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 144;
                                    rectangle.Y = 270;
                                    break;
                                case 1:
                                    rectangle.X = 162;
                                    rectangle.Y = 270;
                                    break;
                                default:
                                    rectangle.X = 180;
                                    rectangle.Y = 270;
                                    break;
                            }
                        }
                        else if (down != num32 && down != num && up == num && left != num32 && left != num && right == num && upRight != num32 && upRight != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 90;
                                    rectangle.Y = 288;
                                    break;
                                case 1:
                                    rectangle.X = 108;
                                    rectangle.Y = 288;
                                    break;
                                default:
                                    rectangle.X = 126;
                                    rectangle.Y = 288;
                                    break;
                            }
                        }
                        else if (down != num32 && down != num && up == num && left == num && right != num32 && right != num && upLeft != num32 && upLeft != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 144;
                                    rectangle.Y = 288;
                                    break;
                                case 1:
                                    rectangle.X = 162;
                                    rectangle.Y = 288;
                                    break;
                                default:
                                    rectangle.X = 180;
                                    rectangle.Y = 288;
                                    break;
                            }
                        }
                        else if (up != num && up != num32 && down == num && left == num && right == num && downLeft != num && downLeft != num32 && downRight != num && downRight != num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 144;
                                    rectangle.Y = 216;
                                    break;
                                case 1:
                                    rectangle.X = 198;
                                    rectangle.Y = 216;
                                    break;
                                default:
                                    rectangle.X = 252;
                                    rectangle.Y = 216;
                                    break;
                            }
                        }
                        else if (down != num && down != num32 && up == num && left == num && right == num && upLeft != num && upLeft != num32 && upRight != num && upRight != num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 144;
                                    rectangle.Y = 252;
                                    break;
                                case 1:
                                    rectangle.X = 198;
                                    rectangle.Y = 252;
                                    break;
                                default:
                                    rectangle.X = 252;
                                    rectangle.Y = 252;
                                    break;
                            }
                        }
                        else if (left != num && left != num32 && down == num && up == num && right == num && upRight != num && upRight != num32 && downRight != num && downRight != num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 126;
                                    rectangle.Y = 234;
                                    break;
                                case 1:
                                    rectangle.X = 180;
                                    rectangle.Y = 234;
                                    break;
                                default:
                                    rectangle.X = 234;
                                    rectangle.Y = 234;
                                    break;
                            }
                        }
                        else if (right != num && right != num32 && down == num && up == num && left == num && upLeft != num && upLeft != num32 && downLeft != num && downLeft != num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 162;
                                    rectangle.Y = 234;
                                    break;
                                case 1:
                                    rectangle.X = 216;
                                    rectangle.Y = 234;
                                    break;
                                default:
                                    rectangle.X = 270;
                                    rectangle.Y = 234;
                                    break;
                            }
                        }
                        else if (up != num32 && up != num && (down == num32 || down == num) && left == num32 && right == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 36;
                                    rectangle.Y = 270;
                                    break;
                                case 1:
                                    rectangle.X = 54;
                                    rectangle.Y = 270;
                                    break;
                                default:
                                    rectangle.X = 72;
                                    rectangle.Y = 270;
                                    break;
                            }
                        }
                        else if (down != num32 && down != num && (up == num32 || up == num) && left == num32 && right == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 36;
                                    rectangle.Y = 288;
                                    break;
                                case 1:
                                    rectangle.X = 54;
                                    rectangle.Y = 288;
                                    break;
                                default:
                                    rectangle.X = 72;
                                    rectangle.Y = 288;
                                    break;
                            }
                        }
                        else if (left != num32 && left != num && (right == num32 || right == num) && up == num32 && down == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 0;
                                    rectangle.Y = 270;
                                    break;
                                case 1:
                                    rectangle.X = 0;
                                    rectangle.Y = 288;
                                    break;
                                default:
                                    rectangle.X = 0;
                                    rectangle.Y = 306;
                                    break;
                            }
                        }
                        else if (right != num32 && right != num && (left == num32 || left == num) && up == num32 && down == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 18;
                                    rectangle.Y = 270;
                                    break;
                                case 1:
                                    rectangle.X = 18;
                                    rectangle.Y = 288;
                                    break;
                                default:
                                    rectangle.X = 18;
                                    rectangle.Y = 306;
                                    break;
                            }
                        }
                        else if (up == num && down == num32 && left == num32 && right == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 198;
                                    rectangle.Y = 288;
                                    break;
                                case 1:
                                    rectangle.X = 216;
                                    rectangle.Y = 288;
                                    break;
                                default:
                                    rectangle.X = 234;
                                    rectangle.Y = 288;
                                    break;
                            }
                        }
                        else if (up == num32 && down == num && left == num32 && right == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 198;
                                    rectangle.Y = 270;
                                    break;
                                case 1:
                                    rectangle.X = 216;
                                    rectangle.Y = 270;
                                    break;
                                default:
                                    rectangle.X = 234;
                                    rectangle.Y = 270;
                                    break;
                            }
                        }
                        else if (up == num32 && down == num32 && left == num && right == num32) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 198;
                                    rectangle.Y = 306;
                                    break;
                                case 1:
                                    rectangle.X = 216;
                                    rectangle.Y = 306;
                                    break;
                                default:
                                    rectangle.X = 234;
                                    rectangle.Y = 306;
                                    break;
                            }
                        }
                        else if (up == num32 && down == num32 && left == num32 && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 144;
                                    rectangle.Y = 306;
                                    break;
                                case 1:
                                    rectangle.X = 162;
                                    rectangle.Y = 306;
                                    break;
                                default:
                                    rectangle.X = 180;
                                    rectangle.Y = 306;
                                    break;
                            }
                        }
                        if (up != num && up != num32 && down == num && left == num && right == num) {
                            if ((downLeft == num32 || downLeft == num) && downRight != num32 && downRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 324;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 324;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 324;
                                        break;
                                }
                            }
                            else if ((downRight == num32 || downRight == num) && downLeft != num32 && downLeft != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 324;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 324;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 324;
                                        break;
                                }
                            }
                        }
                        else if (down != num && down != num32 && up == num && left == num && right == num) {
                            if ((upLeft == num32 || upLeft == num) && upRight != num32 && upRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 342;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 342;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 342;
                                        break;
                                }
                            }
                            else if ((upRight == num32 || upRight == num) && upLeft != num32 && upLeft != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 342;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 342;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 342;
                                        break;
                                }
                            }
                        }
                        else if (left != num && left != num32 && up == num && down == num && right == num) {
                            if ((upRight == num32 || upRight == num) && downRight != num32 && downRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 360;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 360;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 360;
                                        break;
                                }
                            }
                            else if ((downRight == num32 || downRight == num) && upRight != num32 && upRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 360;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 360;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 360;
                                        break;
                                }
                            }
                        }
                        else if (right != num && right != num32 && up == num && down == num && left == num) {
                            if ((upLeft == num32 || upLeft == num) && downLeft != num32 && downLeft != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 378;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 378;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 378;
                                        break;
                                }
                            }
                            else if ((downLeft == num32 || downLeft == num) && upLeft != num32 && upLeft != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 378;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 378;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 378;
                                        break;
                                }
                            }
                        }
                        if ((up == num || up == num32) && (down == num || down == num32) && (left == num || left == num32) && (right == num || right == num32) && upLeft != -1 && upRight != -1 && downLeft != -1 && downRight != -1) {
                            if ((i + j) % 2 == 1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 108;
                                        rectangle.Y = 198;
                                        break;
                                    case 1:
                                        rectangle.X = 126;
                                        rectangle.Y = 198;
                                        break;
                                    default:
                                        rectangle.X = 144;
                                        rectangle.Y = 198;
                                        break;
                                }
                            }
                            else {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 18;
                                        rectangle.Y = 18;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 18;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 18;
                                        break;
                                }
                            }
                        }
                        if (num32 >= 0 && TileID.Sets.Dirt[num32]) {
                            TileMergeAttempt(-2, TileID.Sets.Dirt, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        else if (num32 >= 0 && TileID.Sets.Mud[num32]) {
                            TileMergeAttempt(-2, TileID.Sets.Mud, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        else {
                            TileMergeAttempt(-2, num32, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        tileMergeCullCache.Cull(ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    }
                    TileMergeAttempt(num, Main.tileMerge[num], ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                    if (rectangle.X == -1 && rectangle.Y == -1 && (Main.tileMergeDirt[num] || (num > -1 && TileID.Sets.ChecksForMerge[num]))) {
                        if (!flag) {
                            flag = true;
                            TileMergeAttemptWeird(num, -1, Main.tileSolid, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        if (up > -1 && up != num) {
                            up = -1;
                        }
                        if (down > -1 && down != num) {
                            down = -1;
                        }
                        if (left > -1 && left != num) {
                            left = -1;
                        }
                        if (right > -1 && right != num) {
                            right = -1;
                        }
                        tileMergeCullCache.Cull(ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        if (up != -1 && down != -1 && left != -1 && right != -1) {
                            if (up == -2 && down == num && left == num && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 144;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 162;
                                        rectangle.Y = 108;
                                        break;
                                    default:
                                        rectangle.X = 180;
                                        rectangle.Y = 108;
                                        break;
                                }
                                mergeUp = true;
                            }
                            else if (up == num && down == -2 && left == num && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 144;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 162;
                                        rectangle.Y = 90;
                                        break;
                                    default:
                                        rectangle.X = 180;
                                        rectangle.Y = 90;
                                        break;
                                }
                                mergeDown = true;
                            }
                            else if (up == num && down == num && left == -2 && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 162;
                                        rectangle.Y = 126;
                                        break;
                                    case 1:
                                        rectangle.X = 162;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 162;
                                        rectangle.Y = 162;
                                        break;
                                }
                                mergeLeft = true;
                            }
                            else if (up == num && down == num && left == num && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 144;
                                        rectangle.Y = 126;
                                        break;
                                    case 1:
                                        rectangle.X = 144;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 144;
                                        rectangle.Y = 162;
                                        break;
                                }
                                mergeRight = true;
                            }
                            else if (up == -2 && down == num && left == -2 && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 162;
                                        break;
                                }
                                mergeUp = true;
                                mergeLeft = true;
                            }
                            else if (up == -2 && down == num && left == num && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 126;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 162;
                                        break;
                                }
                                mergeUp = true;
                                mergeRight = true;
                            }
                            else if (up == num && down == -2 && left == -2 && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 36;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 180;
                                        break;
                                }
                                mergeDown = true;
                                mergeLeft = true;
                            }
                            else if (up == num && down == -2 && left == num && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 108;
                                        break;
                                    case 1:
                                        rectangle.X = 54;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 180;
                                        break;
                                }
                                mergeDown = true;
                                mergeRight = true;
                            }
                            else if (up == num && down == num && left == -2 && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 180;
                                        rectangle.Y = 126;
                                        break;
                                    case 1:
                                        rectangle.X = 180;
                                        rectangle.Y = 144;
                                        break;
                                    default:
                                        rectangle.X = 180;
                                        rectangle.Y = 162;
                                        break;
                                }
                                mergeLeft = true;
                                mergeRight = true;
                            }
                            else if (up == -2 && down == -2 && left == num && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 144;
                                        rectangle.Y = 180;
                                        break;
                                    case 1:
                                        rectangle.X = 162;
                                        rectangle.Y = 180;
                                        break;
                                    default:
                                        rectangle.X = 180;
                                        rectangle.Y = 180;
                                        break;
                                }
                                mergeUp = true;
                                mergeDown = true;
                            }
                            else if (up == -2 && down == num && left == -2 && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 198;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 198;
                                        rectangle.Y = 108;
                                        break;
                                    default:
                                        rectangle.X = 198;
                                        rectangle.Y = 126;
                                        break;
                                }
                                mergeUp = true;
                                mergeLeft = true;
                                mergeRight = true;
                            }
                            else if (up == num && down == -2 && left == -2 && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 198;
                                        rectangle.Y = 144;
                                        break;
                                    case 1:
                                        rectangle.X = 198;
                                        rectangle.Y = 162;
                                        break;
                                    default:
                                        rectangle.X = 198;
                                        rectangle.Y = 180;
                                        break;
                                }
                                mergeDown = true;
                                mergeLeft = true;
                                mergeRight = true;
                            }
                            else if (up == -2 && down == -2 && left == num && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 216;
                                        rectangle.Y = 144;
                                        break;
                                    case 1:
                                        rectangle.X = 216;
                                        rectangle.Y = 162;
                                        break;
                                    default:
                                        rectangle.X = 216;
                                        rectangle.Y = 180;
                                        break;
                                }
                                mergeUp = true;
                                mergeDown = true;
                                mergeRight = true;
                            }
                            else if (up == -2 && down == -2 && left == -2 && right == num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 216;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 216;
                                        rectangle.Y = 108;
                                        break;
                                    default:
                                        rectangle.X = 216;
                                        rectangle.Y = 126;
                                        break;
                                }
                                mergeUp = true;
                                mergeDown = true;
                                mergeLeft = true;
                            }
                            else if (up == -2 && down == -2 && left == -2 && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 108;
                                        rectangle.Y = 198;
                                        break;
                                    case 1:
                                        rectangle.X = 126;
                                        rectangle.Y = 198;
                                        break;
                                    default:
                                        rectangle.X = 144;
                                        rectangle.Y = 198;
                                        break;
                                }
                                mergeUp = true;
                                mergeDown = true;
                                mergeLeft = true;
                                mergeRight = true;
                            }
                            else if (up == num && down == num && left == num && right == num) {
                                if (upLeft == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 18;
                                            rectangle.Y = 108;
                                            break;
                                        case 1:
                                            rectangle.X = 18;
                                            rectangle.Y = 144;
                                            break;
                                        default:
                                            rectangle.X = 18;
                                            rectangle.Y = 180;
                                            break;
                                    }
                                }
                                if (upRight == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 0;
                                            rectangle.Y = 108;
                                            break;
                                        case 1:
                                            rectangle.X = 0;
                                            rectangle.Y = 144;
                                            break;
                                        default:
                                            rectangle.X = 0;
                                            rectangle.Y = 180;
                                            break;
                                    }
                                }
                                if (downLeft == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 18;
                                            rectangle.Y = 90;
                                            break;
                                        case 1:
                                            rectangle.X = 18;
                                            rectangle.Y = 126;
                                            break;
                                        default:
                                            rectangle.X = 18;
                                            rectangle.Y = 162;
                                            break;
                                    }
                                }
                                if (downRight == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 0;
                                            rectangle.Y = 90;
                                            break;
                                        case 1:
                                            rectangle.X = 0;
                                            rectangle.Y = 126;
                                            break;
                                        default:
                                            rectangle.X = 0;
                                            rectangle.Y = 162;
                                            break;
                                    }
                                }
                            }
                        }
                        else {
                            if (!TileID.Sets.Grass[num] && !TileID.Sets.GrassSpecial[num]) {
                                if (up == -1 && down == -2 && left == num && right == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 234;
                                            rectangle.Y = 0;
                                            break;
                                        case 1:
                                            rectangle.X = 252;
                                            rectangle.Y = 0;
                                            break;
                                        default:
                                            rectangle.X = 270;
                                            rectangle.Y = 0;
                                            break;
                                    }
                                    mergeDown = true;
                                }
                                else if (up == -2 && down == -1 && left == num && right == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 234;
                                            rectangle.Y = 18;
                                            break;
                                        case 1:
                                            rectangle.X = 252;
                                            rectangle.Y = 18;
                                            break;
                                        default:
                                            rectangle.X = 270;
                                            rectangle.Y = 18;
                                            break;
                                    }
                                    mergeUp = true;
                                }
                                else if (up == num && down == num && left == -1 && right == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 234;
                                            rectangle.Y = 36;
                                            break;
                                        case 1:
                                            rectangle.X = 252;
                                            rectangle.Y = 36;
                                            break;
                                        default:
                                            rectangle.X = 270;
                                            rectangle.Y = 36;
                                            break;
                                    }
                                    mergeRight = true;
                                }
                                else if (up == num && down == num && left == -2 && right == -1) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 234;
                                            rectangle.Y = 54;
                                            break;
                                        case 1:
                                            rectangle.X = 252;
                                            rectangle.Y = 54;
                                            break;
                                        default:
                                            rectangle.X = 270;
                                            rectangle.Y = 54;
                                            break;
                                    }
                                    mergeLeft = true;
                                }
                            }
                            if (up != -1 && down != -1 && left == -1 && right == num) {
                                if (up == -2 && down == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 72;
                                            rectangle.Y = 144;
                                            break;
                                        case 1:
                                            rectangle.X = 72;
                                            rectangle.Y = 162;
                                            break;
                                        default:
                                            rectangle.X = 72;
                                            rectangle.Y = 180;
                                            break;
                                    }
                                    mergeUp = true;
                                }
                                else if (down == -2 && up == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 72;
                                            rectangle.Y = 90;
                                            break;
                                        case 1:
                                            rectangle.X = 72;
                                            rectangle.Y = 108;
                                            break;
                                        default:
                                            rectangle.X = 72;
                                            rectangle.Y = 126;
                                            break;
                                    }
                                    mergeDown = true;
                                }
                            }
                            else if (up != -1 && down != -1 && left == num && right == -1) {
                                if (up == -2 && down == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 90;
                                            rectangle.Y = 144;
                                            break;
                                        case 1:
                                            rectangle.X = 90;
                                            rectangle.Y = 162;
                                            break;
                                        default:
                                            rectangle.X = 90;
                                            rectangle.Y = 180;
                                            break;
                                    }
                                    mergeUp = true;
                                }
                                else if (down == -2 && up == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 90;
                                            rectangle.Y = 90;
                                            break;
                                        case 1:
                                            rectangle.X = 90;
                                            rectangle.Y = 108;
                                            break;
                                        default:
                                            rectangle.X = 90;
                                            rectangle.Y = 126;
                                            break;
                                    }
                                    mergeDown = true;
                                }
                            }
                            else if (up == -1 && down == num && left != -1 && right != -1) {
                                if (left == -2 && right == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 0;
                                            rectangle.Y = 198;
                                            break;
                                        case 1:
                                            rectangle.X = 18;
                                            rectangle.Y = 198;
                                            break;
                                        default:
                                            rectangle.X = 36;
                                            rectangle.Y = 198;
                                            break;
                                    }
                                    mergeLeft = true;
                                }
                                else if (right == -2 && left == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 54;
                                            rectangle.Y = 198;
                                            break;
                                        case 1:
                                            rectangle.X = 72;
                                            rectangle.Y = 198;
                                            break;
                                        default:
                                            rectangle.X = 90;
                                            rectangle.Y = 198;
                                            break;
                                    }
                                    mergeRight = true;
                                }
                            }
                            else if (up == num && down == -1 && left != -1 && right != -1) {
                                if (left == -2 && right == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 0;
                                            rectangle.Y = 216;
                                            break;
                                        case 1:
                                            rectangle.X = 18;
                                            rectangle.Y = 216;
                                            break;
                                        default:
                                            rectangle.X = 36;
                                            rectangle.Y = 216;
                                            break;
                                    }
                                    mergeLeft = true;
                                }
                                else if (right == -2 && left == num) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 54;
                                            rectangle.Y = 216;
                                            break;
                                        case 1:
                                            rectangle.X = 72;
                                            rectangle.Y = 216;
                                            break;
                                        default:
                                            rectangle.X = 90;
                                            rectangle.Y = 216;
                                            break;
                                    }
                                    mergeRight = true;
                                }
                            }
                            else if (up != -1 && down != -1 && left == -1 && right == -1) {
                                if (up == -2 && down == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 108;
                                            rectangle.Y = 216;
                                            break;
                                        case 1:
                                            rectangle.X = 108;
                                            rectangle.Y = 234;
                                            break;
                                        default:
                                            rectangle.X = 108;
                                            rectangle.Y = 252;
                                            break;
                                    }
                                    mergeUp = true;
                                    mergeDown = true;
                                }
                                else if (up == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 126;
                                            rectangle.Y = 144;
                                            break;
                                        case 1:
                                            rectangle.X = 126;
                                            rectangle.Y = 162;
                                            break;
                                        default:
                                            rectangle.X = 126;
                                            rectangle.Y = 180;
                                            break;
                                    }
                                    mergeUp = true;
                                }
                                else if (down == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 126;
                                            rectangle.Y = 90;
                                            break;
                                        case 1:
                                            rectangle.X = 126;
                                            rectangle.Y = 108;
                                            break;
                                        default:
                                            rectangle.X = 126;
                                            rectangle.Y = 126;
                                            break;
                                    }
                                    mergeDown = true;
                                }
                            }
                            else if (up == -1 && down == -1 && left != -1 && right != -1) {
                                if (left == -2 && right == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 162;
                                            rectangle.Y = 198;
                                            break;
                                        case 1:
                                            rectangle.X = 180;
                                            rectangle.Y = 198;
                                            break;
                                        default:
                                            rectangle.X = 198;
                                            rectangle.Y = 198;
                                            break;
                                    }
                                    mergeLeft = true;
                                    mergeRight = true;
                                }
                                else if (left == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 0;
                                            rectangle.Y = 252;
                                            break;
                                        case 1:
                                            rectangle.X = 18;
                                            rectangle.Y = 252;
                                            break;
                                        default:
                                            rectangle.X = 36;
                                            rectangle.Y = 252;
                                            break;
                                    }
                                    mergeLeft = true;
                                }
                                else if (right == -2) {
                                    switch (num27) {
                                        case 0:
                                            rectangle.X = 54;
                                            rectangle.Y = 252;
                                            break;
                                        case 1:
                                            rectangle.X = 72;
                                            rectangle.Y = 252;
                                            break;
                                        default:
                                            rectangle.X = 90;
                                            rectangle.Y = 252;
                                            break;
                                    }
                                    mergeRight = true;
                                }
                            }
                            else if (up == -2 && down == -1 && left == -1 && right == -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 108;
                                        rectangle.Y = 144;
                                        break;
                                    case 1:
                                        rectangle.X = 108;
                                        rectangle.Y = 162;
                                        break;
                                    default:
                                        rectangle.X = 108;
                                        rectangle.Y = 180;
                                        break;
                                }
                                mergeUp = true;
                            }
                            else if (up == -1 && down == -2 && left == -1 && right == -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 108;
                                        rectangle.Y = 90;
                                        break;
                                    case 1:
                                        rectangle.X = 108;
                                        rectangle.Y = 108;
                                        break;
                                    default:
                                        rectangle.X = 108;
                                        rectangle.Y = 126;
                                        break;
                                }
                                mergeDown = true;
                            }
                            else if (up == -1 && down == -1 && left == -2 && right == -1) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 0;
                                        rectangle.Y = 234;
                                        break;
                                    case 1:
                                        rectangle.X = 18;
                                        rectangle.Y = 234;
                                        break;
                                    default:
                                        rectangle.X = 36;
                                        rectangle.Y = 234;
                                        break;
                                }
                                mergeLeft = true;
                            }
                            else if (up == -1 && down == -1 && left == -1 && right == -2) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 54;
                                        rectangle.Y = 234;
                                        break;
                                    case 1:
                                        rectangle.X = 72;
                                        rectangle.Y = 234;
                                        break;
                                    default:
                                        rectangle.X = 90;
                                        rectangle.Y = 234;
                                        break;
                                }
                                mergeRight = true;
                            }
                        }
                    }
                    int num33 = tile.GetBlockType();
                    if (TileID.Sets.HasSlopeFrames[num]) {
                        if (num33 == 0) {
                            bool flag3 = num == up && tile2 != null && tile2.IsTopSlope();
                            bool flag4 = num == left && tile4 != null && tile4.IsLeftSlope();
                            bool flag5 = num == right && tile5 != null && tile5.IsRightSlope();
                            bool flag6 = num == down && tile3 != null && tile3.IsBottomSlope();
                            int num34 = 0;
                            int num35 = 0;
                            if (flag3.ToInt() + flag4.ToInt() + flag5.ToInt() + flag6.ToInt() > 2) {
                                int num36 = (tile2 != null && tile2.IsSlope() == 1).ToInt() + (tile5 != null && tile5.IsSlope() == 1).ToInt() + (tile3 != null && tile3.IsSlope() == 4).ToInt() + (tile4 != null && tile4.IsSlope() == 4).ToInt();
                                int num37 = (tile2 != null && tile2.IsSlope() == 2).ToInt() + (tile5 != null && tile5.IsSlope() == 3).ToInt() + (tile3 != null && tile3.IsSlope() == 3).ToInt() + (tile4 != null && tile4.IsSlope() == 2).ToInt();
                                if (num36 == num37) {
                                    num34 = 2;
                                    num35 = 4;
                                }
                                else if (num36 > num37) {
                                    bool num53 = num == upLeft && tile8 != null && tile8.IsSlope() == 0;
                                    bool flag7 = num == downRight && tile7 != null && tile7.IsSlope() == 0;
                                    if (num53 && flag7) {
                                        num35 = 4;
                                    }
                                    else if (flag7) {
                                        num34 = 6;
                                    }
                                    else {
                                        num34 = 7;
                                        num35 = 1;
                                    }
                                }
                                else {
                                    bool num54 = num == upRight && tile9 != null && tile9.IsSlope() == 0;
                                    bool flag8 = num == downLeft && tile6 != null && tile6.IsSlope() == 0;
                                    if (num54 && flag8) {
                                        num35 = 4;
                                        num34 = 1;
                                    }
                                    else if (flag8) {
                                        num34 = 7;
                                    }
                                    else {
                                        num34 = 6;
                                        num35 = 1;
                                    }
                                }
                                rectangle.X = (18 + num34) * 18;
                                rectangle.Y = num35 * 18;
                            }
                            else {
                                if (flag3 && flag4 && num == down && num == right) {
                                    num35 = 2;
                                }
                                else if (flag3 && flag5 && num == down && num == left) {
                                    num34 = 1;
                                    num35 = 2;
                                }
                                else if (flag5 && flag6 && num == up && num == left) {
                                    num34 = 1;
                                    num35 = 3;
                                }
                                else if (flag6 && flag4 && num == up && num == right) {
                                    num35 = 3;
                                }
                                if (num34 != 0 || num35 != 0) {
                                    rectangle.X = (18 + num34) * 18;
                                    rectangle.Y = num35 * 18;
                                }
                            }
                        }
                        if (num33 >= 2 && (rectangle.X < 0 || rectangle.Y < 0)) {
                            int num40 = -1;
                            int num41 = -1;
                            int num42 = -1;
                            int num43 = 0;
                            int num44 = 0;
                            switch (num33) {
                                case 2:
                                    num40 = left;
                                    num41 = down;
                                    num42 = downLeft;
                                    num43++;
                                    break;
                                case 3:
                                    num40 = right;
                                    num41 = down;
                                    num42 = downRight;
                                    break;
                                case 4:
                                    num40 = left;
                                    num41 = up;
                                    num42 = upLeft;
                                    num43++;
                                    num44++;
                                    break;
                                case 5:
                                    num40 = right;
                                    num41 = up;
                                    num42 = upRight;
                                    num44++;
                                    break;
                            }
                            if (num != num40 || num != num41 || num != num42) {
                                if (num == num40 && num == num41) {
                                    num43 += 2;
                                }
                                else if (num == num40) {
                                    num43 += 4;
                                }
                                else if (num == num41) {
                                    num43 += 4;
                                    num44 += 2;
                                }
                                else {
                                    num43 += 2;
                                    num44 += 2;
                                }
                            }
                            rectangle.X = (18 + num43) * 18;
                            rectangle.Y = num44 * 18;
                        }
                    }
                    if (rectangle.X < 0 || rectangle.Y < 0) {
                        if (!flag) {
                            flag = true;
                            TileMergeAttemptWeird(num, -1, Main.tileSolid, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                            tileMergeCullCache.Cull(ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        if (TileID.Sets.Grass[num] || TileID.Sets.GrassSpecial[num] || Main.tileMoss[num] || TileID.Sets.tileMossBrick[num]) {
                            TileMergeAttempt(num, -2, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                            tileMergeCullCache.Cull(ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
                        }
                        if (up == num && down == num && left == num && right == num) {
                            if (upLeft != num && upRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 108;
                                        rectangle.Y = 18;
                                        break;
                                    case 1:
                                        rectangle.X = 126;
                                        rectangle.Y = 18;
                                        break;
                                    default:
                                        rectangle.X = 144;
                                        rectangle.Y = 18;
                                        break;
                                }
                            }
                            else if (downLeft != num && downRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 108;
                                        rectangle.Y = 36;
                                        break;
                                    case 1:
                                        rectangle.X = 126;
                                        rectangle.Y = 36;
                                        break;
                                    default:
                                        rectangle.X = 144;
                                        rectangle.Y = 36;
                                        break;
                                }
                            }
                            else if (upLeft != num && downLeft != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 180;
                                        rectangle.Y = 0;
                                        break;
                                    case 1:
                                        rectangle.X = 180;
                                        rectangle.Y = 18;
                                        break;
                                    default:
                                        rectangle.X = 180;
                                        rectangle.Y = 36;
                                        break;
                                }
                            }
                            else if (upRight != num && downRight != num) {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 198;
                                        rectangle.Y = 0;
                                        break;
                                    case 1:
                                        rectangle.X = 198;
                                        rectangle.Y = 18;
                                        break;
                                    default:
                                        rectangle.X = 198;
                                        rectangle.Y = 36;
                                        break;
                                }
                            }
                            else {
                                switch (num27) {
                                    case 0:
                                        rectangle.X = 18;
                                        rectangle.Y = 18;
                                        break;
                                    case 1:
                                        rectangle.X = 36;
                                        rectangle.Y = 18;
                                        break;
                                    default:
                                        rectangle.X = 54;
                                        rectangle.Y = 18;
                                        break;
                                }
                            }
                        }
                        else if (up != num && down == num && left == num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 18;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 36;
                                    rectangle.Y = 0;
                                    break;
                                default:
                                    rectangle.X = 54;
                                    rectangle.Y = 0;
                                    break;
                            }
                        }
                        else if (up == num && down != num && left == num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 18;
                                    rectangle.Y = 36;
                                    break;
                                case 1:
                                    rectangle.X = 36;
                                    rectangle.Y = 36;
                                    break;
                                default:
                                    rectangle.X = 54;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up == num && down == num && left != num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 0;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 0;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 0;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up == num && down == num && left == num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 72;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 72;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 72;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up != num && down == num && left != num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 0;
                                    rectangle.Y = 54;
                                    break;
                                case 1:
                                    rectangle.X = 36;
                                    rectangle.Y = 54;
                                    break;
                                default:
                                    rectangle.X = 72;
                                    rectangle.Y = 54;
                                    break;
                            }
                        }
                        else if (up != num && down == num && left == num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 18;
                                    rectangle.Y = 54;
                                    break;
                                case 1:
                                    rectangle.X = 54;
                                    rectangle.Y = 54;
                                    break;
                                default:
                                    rectangle.X = 90;
                                    rectangle.Y = 54;
                                    break;
                            }
                        }
                        else if (up == num && down != num && left != num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 0;
                                    rectangle.Y = 72;
                                    break;
                                case 1:
                                    rectangle.X = 36;
                                    rectangle.Y = 72;
                                    break;
                                default:
                                    rectangle.X = 72;
                                    rectangle.Y = 72;
                                    break;
                            }
                        }
                        else if (up == num && down != num && left == num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 18;
                                    rectangle.Y = 72;
                                    break;
                                case 1:
                                    rectangle.X = 54;
                                    rectangle.Y = 72;
                                    break;
                                default:
                                    rectangle.X = 90;
                                    rectangle.Y = 72;
                                    break;
                            }
                        }
                        else if (up == num && down == num && left != num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 90;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 90;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 90;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up != num && down != num && left == num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 108;
                                    rectangle.Y = 72;
                                    break;
                                case 1:
                                    rectangle.X = 126;
                                    rectangle.Y = 72;
                                    break;
                                default:
                                    rectangle.X = 144;
                                    rectangle.Y = 72;
                                    break;
                            }
                        }
                        else if (up != num && down == num && left != num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 108;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 126;
                                    rectangle.Y = 0;
                                    break;
                                default:
                                    rectangle.X = 144;
                                    rectangle.Y = 0;
                                    break;
                            }
                        }
                        else if (up == num && down != num && left != num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 108;
                                    rectangle.Y = 54;
                                    break;
                                case 1:
                                    rectangle.X = 126;
                                    rectangle.Y = 54;
                                    break;
                                default:
                                    rectangle.X = 144;
                                    rectangle.Y = 54;
                                    break;
                            }
                        }
                        else if (up != num && down != num && left != num && right == num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 162;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 162;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 162;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up != num && down != num && left == num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 216;
                                    rectangle.Y = 0;
                                    break;
                                case 1:
                                    rectangle.X = 216;
                                    rectangle.Y = 18;
                                    break;
                                default:
                                    rectangle.X = 216;
                                    rectangle.Y = 36;
                                    break;
                            }
                        }
                        else if (up != num && down != num && left != num && right != num) {
                            switch (num27) {
                                case 0:
                                    rectangle.X = 162;
                                    rectangle.Y = 54;
                                    break;
                                case 1:
                                    rectangle.X = 180;
                                    rectangle.Y = 54;
                                    break;
                                default:
                                    rectangle.X = 198;
                                    rectangle.Y = 54;
                                    break;
                            }
                        }
                    }
                    if (rectangle.X <= -1 || rectangle.Y <= -1) {
                        if (num27 <= 0) {
                            rectangle.X = 18;
                            rectangle.Y = 18;
                        }
                        else if (num27 == 1) {
                            rectangle.X = 36;
                            rectangle.Y = 18;
                        }
                        if (num27 >= 2) {
                            rectangle.X = 54;
                            rectangle.Y = 18;
                        }
                    }
                    if (Main.tileLargeFrames[num] == 1 && num27 == 3) {
                        rectangle.Y += 90;
                    }
                    if (Main.tileLargeFrames[num] == 2 && num27 == 3) {
                        rectangle.Y += 90;
                    }
                    tile.TileFrameX = (short)rectangle.X;
                    tile.TileFrameY = (short)rectangle.Y;
                    if (TileID.Sets.IsVine[num]) {
                        up = ((tile2 == null) ? num : ((!tile2.IsNactive()) ? (-1) : ((!tile2.IsBottomSlope()) ? tile2.TileType : (-1))));
                        if (num != up) {
                            bool num55 = up == 60 || up == 62;
                            bool num56 = up == 109 || up == 115;
                            bool flag9 = up == 23 || up == 636 || up == 661;
                            bool flag10 = up == 199 || up == 205 || up == 662;
                            bool flag11 = up == 2 || up == 52;
                            bool flag12 = up == 382;
                            bool num57 = up == 70 || up == 528;
                            bool num58 = up == 633 || up == 638;
                            ushort num49 = 0;
                            if (num58) {
                                num49 = 638;
                            }
                            if (num57) {
                                num49 = 528;
                            }
                            if (num56) {
                                num49 = 115;
                            }
                            if (num55) {
                                num49 = 62;
                            }
                            if (flag9) {
                                num49 = 636;
                            }
                            if (flag10) {
                                num49 = 205;
                            }
                            if (flag11 && num != 382) {
                                num49 = 52;
                            }
                            if (flag12) {
                                num49 = 382;
                            }
                            if (num49 != 0 && num49 != num) {
                                tile.TileType = num49;
                                SquareTileFrame(i, j);
                                return;
                            }
                        }
                        if (up != num) {
                            bool flag13 = false;
                            if (up == -1) {
                                flag13 = true;
                            }
                            if (num == 52 && up != 2 && up != 192) {
                                flag13 = true;
                            }
                            if (num == 382 && up != 2 && up != 192) {
                                flag13 = true;
                            }
                            if (num == 62 && up != 60) {
                                flag13 = true;
                            }
                            if (num == 115 && up != 109) {
                                flag13 = true;
                            }
                            if (num == 528 && up != 70) {
                                flag13 = true;
                            }
                            if (num == 636 && up != 23 && up != 661) {
                                flag13 = true;
                            }
                            if (num == 205 && up != 199 && up != 662) {
                                flag13 = true;
                            }
                            if (num == 638 && up != 633) {
                                flag13 = true;
                            }
                            if (flag13) {
                                KillTile(i, j);
                            }
                        }
                    }
                    bool flag14 = false;
                    if (!noTileActions && tile.IsActive() && TileID.Sets.Falling[num]) {
                        SpawnFallingBlockProjectile(i, j, tile, tile2, tile3, num);
                    }
                    if ((rectangle.X != frameX && rectangle.Y != frameY && frameX >= 0 && frameY >= 0) || flag14) {
                        tileReframeCount++;
                        if (tileReframeCount < 25) {
                            bool num59 = mergeUp;
                            bool flag15 = mergeDown;
                            bool flag16 = mergeLeft;
                            bool flag17 = mergeRight;
                            TileFrame(i - 1, j);
                            TileFrame(i + 1, j);
                            TileFrame(i, j - 1);
                            TileFrame(i, j + 1);
                            mergeUp = num59;
                            mergeDown = flag15;
                            mergeLeft = flag16;
                            mergeRight = flag17;
                        }
                        tileReframeCount--;
                    }
                }
            }

            if (i > 0 && j > 0) {
                UpdateMapTile(i, j, addToList);
            }
        }

    }
}
