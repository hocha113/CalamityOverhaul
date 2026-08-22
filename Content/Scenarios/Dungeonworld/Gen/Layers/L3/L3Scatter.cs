using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //L3层内三区(ROOMS-L3 §0):上区阅览(亮)/中区迷宫(标)/下区禁书区带(暗),
    //撒布条目按区门控，密度矩阵(INDEX §7)L3列:灯=亮区高/禁书区零,
    //挂画=峰,贴墙书台=峰,杂物=标(纸堆书堆),蛛网=禁书区低,旗帜=定向(目录厅,不入撒布)
    internal readonly struct L3Zones(int readingBottom, int forbiddenTop)
    {
        //y<ReadingBottom=阅览区;y>=ForbiddenTop=禁书区带;其间=迷宫区
        internal readonly int ReadingBottom = readingBottom;
        internal readonly int ForbiddenTop = forbiddenTop;
    }

    //L3层专属撒布装修数据(自包含),经ctx.Scatter声明、P55 ScatterPass统一执行
    //放置一律先局部验证再落,拒绝即计失败(F30三段模式);MaxPlaced=耗时保险(R5)
    internal static class L3Scatter
    {
        internal static ScatterEntry[] Entries(L3Zones zones) => [
            ReadingLamps(zones),
            MazeLamps(zones),
            WallBookNooks(zones),
            Paintings(zones),
            FloorClutter(),
            VaultCobwebs(zones),
        ];

        //阅览区灯:高档全亮,吊灯1/7+灯笼混用(ROOMS-L3 §2.2),不接开关
        private static ScatterEntry ReadingLamps(L3Zones zones) => new() {
            Name = "灯·阅览区(全亮)", Density = ScatterDensity.High,
            StandardPer100k = 1.0, DedupeDist = 15, MaxPlaced = 40,
            TryPlace = (x, y) => {
                if (y >= zones.ReadingBottom || !FindCeiling(x, y, out int anchorY)) {
                    return false;
                }
                bool chandelier = WorldGen.genRand.Next(7) == 0;
                if (!AirColumn(x, anchorY, chandelier ? 8 : 5)) {
                    return false;
                }
                bool ok = chandelier
                    ? L3Lights.PlaceChandelier(x, anchorY)
                    : L3Lights.PlaceLantern(x, anchorY, caged: false);
                if (ok) {
                    L3Lights.LampsLit++;
                }
                return ok;
            },
        };

        //迷宫区灯:标档,原版"三盏灭两盏"+开关电线全语法(F33,本层独占INDEX §3)
        private static ScatterEntry MazeLamps(L3Zones zones) => new() {
            Name = "灯·迷宫区(三盏灭两盏)", Density = ScatterDensity.Standard,
            StandardPer100k = 3.0, DedupeDist = 15, MaxPlaced = 80,
            TryPlace = (x, y) => {
                if (y < zones.ReadingBottom || y >= zones.ForbiddenTop
                    || !FindCeiling(x, y, out int anchorY)) {
                    return false;
                }
                bool chandelier = WorldGen.genRand.Next(7) == 0;
                if (!AirColumn(x, anchorY, chandelier ? 8 : 5)) {
                    return false;
                }
                return L3Lights.PlaceWiredLamp(x, anchorY, chandelier,
                    caged: WorldGen.genRand.NextBool(), 2, 3, WorldGen.genRand);
            },
        };

        //贴墙书台:峰档(本层独占母题全形态,F30语法)：沿墙伸1~3格平台,
        //台上书(样式0~4,水矢书5禁用)或墨瓶/水蜡烛(:28376-28436原版分支)
        private static ScatterEntry WallBookNooks(L3Zones zones) => new() {
            Name = "贴墙书台", Density = ScatterDensity.Peak,
            StandardPer100k = 1.2, DedupeDist = 8, MaxPlaced = 130,
            TryPlace = (x, y) => {
                if (y >= zones.ForbiddenTop || !L3Palette.InBlueInterior(x, y)) {
                    return false;
                }
                //贴墙判定:一侧实心地牢砖,向另一侧伸展
                int dir;
                if (SolidBrick(x - 1, y)) {
                    dir = 1;
                }
                else if (SolidBrick(x + 1, y)) {
                    dir = -1;
                }
                else {
                    return false;
                }
                int len = WorldGen.genRand.Next(1, 4);
                //伸展带与上方2行必须全空(书/瓶的落位空间)
                for (int i = 0; i < len; i++) {
                    int px = x + dir * i;
                    if (!L3Palette.InBlueInterior(px, y) || Main.tile[px, y - 1].HasTile
                        || Main.tile[px, y - 2].HasTile) {
                        return false;
                    }
                }
                bool books = WorldGen.genRand.NextBool();
                for (int i = 0; i < len; i++) {
                    int px = x + dir * i;
                    TileBrush.SetPlatform(px, y, L3Palette.PlatformFrameY);
                    if (books) {
                        L3Palette.PlaceBook(px, y - 1, WorldGen.genRand);
                    }
                }
                if (!books && WorldGen.genRand.NextBool()) {
                    //非书龛:墨瓶为主,1/4换水蜡烛(原版:28411-28423比例)
                    if (WorldGen.genRand.Next(4) == 0) {
                        L3Palette.PlaceOnSurface(x, y - 1, TileID.WaterCandle);
                    }
                    else {
                        L3Palette.PlaceInkBottle(x, y - 1, WorldGen.genRand);
                    }
                }
                return true;
            },
        };

        //挂画:峰档(全世界峰值,INDEX §7);6x4包络预检(镜像L2先例)
        private static ScatterEntry Paintings(L3Zones zones) => new() {
            Name = "挂画", Density = ScatterDensity.Peak,
            StandardPer100k = 0.7, DedupeDist = 20, MaxPlaced = 80,
            TryPlace = (x, y) => {
                if (y >= zones.ForbiddenTop) {
                    return false;
                }
                Tile center = Main.tile[x, y];
                if (center.HasTile || !Main.wallDungeon[center.WallType]) {
                    return false;
                }
                for (int dx = -3; dx <= 3; dx++) {
                    for (int dy = -2; dy <= 2; dy++) {
                        Tile t = Main.tile[x + dx, y + dy];
                        if (t.HasTile || t.WallType == WallID.None) {
                            return false;
                        }
                    }
                }
                return L3Palette.PlacePainting(x, y);
            },
        };

        //地面杂物=散书/墨瓶/罐(标档;纸堆tile185样式对源不可靠,沿L1保守解
        //用书tile50直铺替代,ROOMS-L3 §2.3明载该fallback)
        private static ScatterEntry FloorClutter() => new() {
            Name = "散书墨瓶", Density = ScatterDensity.Standard,
            StandardPer100k = 5.0, DedupeDist = 6, MaxPlaced = 120,
            TryPlace = static (x, y) => {
                if (!L3Palette.InBlueInterior(x, y) || !L3Palette.OnFloor(x, y)) {
                    return false;
                }
                int roll = WorldGen.genRand.Next(100);
                if (roll < 55) {
                    return L3Palette.PlaceBook(x, y, WorldGen.genRand);
                }
                if (roll < 80) {
                    return L3Palette.PlaceInkBottle(x, y, WorldGen.genRand);
                }
                return WorldGen.PlacePot(x, y, TileID.Pots,
                    WorldGen.genRand.Next(L3Palette.PotStyleMin, L3Palette.PotStyleMax + 1));
            },
        };

        //蛛网:仅禁书区带低档(尘封感,INDEX §3裁决:L3禁书区少量)
        private static ScatterEntry VaultCobwebs(L3Zones zones) => new() {
            Name = "蛛网·禁书带", Density = ScatterDensity.Low,
            StandardPer100k = 3.0, DedupeDist = 8, MaxPlaced = 36,
            TryPlace = (x, y) => {
                if (y < zones.ForbiddenTop) {
                    return false;
                }
                Tile t = Main.tile[x, y];
                if (t.HasTile || t.WallType == WallID.None || !AnySolidNeighbor(x, y)) {
                    return false;
                }
                WorldGen.PlaceTile(x, y, TileID.Cobweb, mute: true);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Cobweb;
            },
        };

        //==================== 局部验证共用件(镜像L2Scatter先例) ====================

        private static bool SolidBrick(int x, int y) {
            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType] && t.TileType != TileID.Platforms;
        }

        //自撒点向上找实心天花(≤8行),返回锚点正下方的悬挂起始行
        private static bool FindCeiling(int x, int y, out int belowCeiling) {
            belowCeiling = 0;
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            for (int i = 1; i <= 8; i++) {
                if (!WorldGen.InWorld(x, y - i, 5)) {
                    return false;
                }
                Tile t = Main.tile[x, y - i];
                if (!t.HasTile) {
                    continue;
                }
                if (!Main.tileSolid[t.TileType] || t.TileType == TileID.Platforms) {
                    return false;
                }
                belowCeiling = y - i + 1;
                return true;
            }
            return false;
        }

        //自anchorY起向下rows行全空(吊挂净空,§3.2-7)
        private static bool AirColumn(int x, int anchorY, int rows) {
            for (int i = 0; i < rows; i++) {
                int py = anchorY + i;
                if (!WorldGen.InWorld(x, py, 5) || Main.tile[x, py].HasTile) {
                    return false;
                }
            }
            return true;
        }

        private static bool AnySolidNeighbor(int x, int y) {
            return IsSolid(x - 1, y) || IsSolid(x + 1, y) || IsSolid(x, y - 1) || IsSolid(x, y + 1);

            static bool IsSolid(int px, int py) {
                Tile t = Main.tile[px, py];
                return t.HasTile && Main.tileSolid[t.TileType];
            }
        }
    }
}
