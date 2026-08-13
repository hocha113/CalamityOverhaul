using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //L6层专属撒布(经ctx.Scatter声明、P55 ScatterPass统一执行,F30三段模式)
    //密度对位ROOMS-INDEX §7矩阵L6列:灯=低(炉光替代)/挂画=低/旗帜=低/
    //地面杂物=标(渣/工具)/蛛网=零/书台=零;
    //尖刺峰值由B型走廊刺坑承担,不随机撒刺(公平性:杀招必须可读预告)
    internal static class L6Scatter
    {
        internal static ScatterEntry[] Entries() => [
            BrassLanterns(),
            Paintings(),
            Banners(),
            SlagClutter(),
            OilStreaks(),
            CrackedCorners(),
        ];

        //黄铜灯笼:低档补位(炉膛是主光);正下净空≥5(§3.2-7吊灯纪律的灯笼档)
        private static ScatterEntry BrassLanterns() => new() {
            Name = "黄铜灯笼", Density = ScatterDensity.Low,
            StandardPer100k = 6, DedupeDist = 16, MaxPlaced = 40,
            TryPlace = static (x, y) => {
                if (!FindCeiling(x, y, out int topY) || !AirColumn(x, topY, 5)) {
                    return false;
                }
                return L6Palette.TryPlaceObject(x, topY, TileID.HangingLanterns,
                    L6Palette.LanternBrassStyle);
            },
        };

        //挂画:低档;6x4包络预检(镜像L2)
        private static ScatterEntry Paintings() => new() {
            Name = "挂画", Density = ScatterDensity.Low,
            StandardPer100k = 4, DedupeDist = 22, MaxPlaced = 12,
            TryPlace = static (x, y) => {
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
                PaintingEntry entry = WorldGen.RandPictureTile();
                WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == entry.tileType;
            },
        };

        //旗帜:低档,天花悬挂,按墙变体取Tiled/Slab样式组
        private static ScatterEntry Banners() => new() {
            Name = "旗帜", Density = ScatterDensity.Low,
            StandardPer100k = 5, DedupeDist = 18, MaxPlaced = 16,
            TryPlace = static (x, y) => {
                if (!FindCeiling(x, y, out int topY) || !AirColumn(x, topY, 4)) {
                    return false;
                }
                return L6Palette.TryPlaceTile(x, topY, TileID.Banners,
                    L6Palette.BannerStyleFor(x, topY + 1));
            },
        };

        //地面渣/工具:标档;罐+铁/铅锭(禁骨堆,INDEX §3骨归L5)
        private static ScatterEntry SlagClutter() => new() {
            Name = "炉渣铁料", Density = ScatterDensity.Standard,
            StandardPer100k = 8, DedupeDist = 10, MaxPlaced = 50,
            TryPlace = static (x, y) => {
                if (!L6Palette.OnDungeonFloor(x, y)) {
                    return false;
                }
                if (WorldGen.genRand.NextBool(3)) {
                    int style = WorldGen.genRand.NextBool()
                        ? L6Palette.BarIronStyle : L6Palette.BarLeadStyle;
                    return L6Palette.TryPlaceTile(x, y, TileID.MetalBars, style);
                }
                return WorldGen.PlacePot(x, y, TileID.Pots,
                    WorldGen.genRand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1));
            },
        };

        //油渍引导线:做旧签名的地面形态,只染实心地板(机关段已有定向油渍,此处补氛围)
        private static ScatterEntry OilStreaks() => new() {
            Name = "油渍", Density = ScatterDensity.Standard,
            StandardPer100k = 10, DedupeDist = 8, MaxPlaced = 60,
            TryPlace = static (x, y) => {
                Tile t = Main.tile[x, y];
                if (!t.HasTile || t.TileType != L6Palette.Brick) {
                    return false;
                }
                if (Main.tile[x, y - 1].HasTile) {
                    return false;
                }
                L6Palette.OilStreakFloor(x, y, WorldGen.genRand.Next(3, 6));
                return true;
            },
        };

        //裂砖墙角贴片:成组3格(§3.2-5破损纪律,禁逐格随机),只改已暴露的蓝砖
        private static ScatterEntry CrackedCorners() => new() {
            Name = "裂砖墙角", Density = ScatterDensity.Low,
            StandardPer100k = 3, DedupeDist = 16, MaxPlaced = 20,
            TryPlace = static (x, y) => {
                int placed = 0;
                for (int i = 0; i < 3; i++) {
                    int px = x + i;
                    if (!WorldGen.InWorld(px, y, 5)) {
                        break;
                    }
                    Tile t = Main.tile[px, y];
                    if (!t.HasTile || t.TileType != L6Palette.Brick || !HasAirNeighbor(px, y)) {
                        break;
                    }
                    TileBrush.SetSolid(px, y, L6Palette.CrackedBrick);
                    placed++;
                }
                return placed >= 3;
            },
        };

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

        private static bool AirColumn(int x, int anchorY, int rows) {
            for (int i = 0; i < rows; i++) {
                int py = anchorY + i;
                if (!WorldGen.InWorld(x, py, 5) || Main.tile[x, py].HasTile) {
                    return false;
                }
            }
            return true;
        }

        private static bool HasAirNeighbor(int x, int y) {
            return IsAir(x - 1, y) || IsAir(x + 1, y) || IsAir(x, y - 1) || IsAir(x, y + 1);

            static bool IsAir(int px, int py) {
                return WorldGen.InWorld(px, py, 5) && !Main.tile[px, py].HasTile;
            }
        }
    }
}
