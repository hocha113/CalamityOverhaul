using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //L5层专属撒布装修(经ctx.Scatter声明、P55 ScatterPass统一执行,F30三段模式)
    //密度对位ROOMS-INDEX §7矩阵L5列:
    //  灯=低(集市高由房型定向装修,本表不重复)/挂画=标(变体墙骨画)/旗帜=标(集市幡)
    //  蛛网=峰/地面杂物=峰(骨堆)/墓碑=上带去重≥20
    //无光深巷(y≥L5State.DarkZoneTop)拒灯/幡/画——二现靠携带光源(INDEX §3)
    //裂砖/尖刺表面拒杂物:保"裂=危险"可读性(F31)
    internal static class L5Scatter
    {
        internal static ScatterEntry[] Entries() => [
            BoneLanterns(),
            BonePilesPeak(),
            CobwebsPeak(),
            Tombstones(),
            BonePaintings(),
            MarketBanners(),
        ];

        //灯:低档骨灯笼,集市WallBase已有定向高光,本条跳过基础墙避免叠灯
        private static ScatterEntry BoneLanterns() => new() {
            Name = "骨灯笼(低)", Density = ScatterDensity.Low,
            StandardPer100k = 8, DedupeDist = 16, MaxPlaced = 48,
            TryPlace = static (x, y) => {
                if (y >= L5State.DarkZoneTop || !FindCeiling(x, y, out int topY)) {
                    return false;
                }
                if (Main.tile[x, y].WallType == L5Palette.WallBase) {
                    return false;
                }
                if (!AirColumn(x, topY, 5)) {
                    return false;
                }
                return L5Palette.TryPlaceObject(x, topY, TileID.HangingLanterns, L5Palette.LanternBone);
            },
        };

        //骨堆峰档:小堆为主、1/4换大堆(INDEX §3骨全权/§7杂物峰)
        private static ScatterEntry BonePilesPeak() => new() {
            Name = "骨堆(峰)", Density = ScatterDensity.Peak,
            StandardPer100k = 16, DedupeDist = 7, MaxPlaced = 180,
            TryPlace = static (x, y) => {
                if (!OnSafeFloor(x, y)) {
                    return false;
                }
                return WorldGen.genRand.Next(4) == 0
                    ? L5Palette.PlaceLargeBones(x, y, WorldGen.genRand)
                    : L5Palette.PlaceSmallBones(x, y, WorldGen.genRand);
            },
        };

        //蛛网峰档:空格+粉地牢墙+四邻实心(墙角感);集市基础墙1/3才落(市集不该结满网)
        private static ScatterEntry CobwebsPeak() => new() {
            Name = "蛛网(峰)", Density = ScatterDensity.Peak,
            StandardPer100k = 22, DedupeDist = 5, MaxPlaced = 240,
            TryPlace = static (x, y) => {
                Tile t = Main.tile[x, y];
                if (t.HasTile || !L5Palette.IsPinkDungeonWall(t.WallType) || !AnySolidNeighbor(x, y)) {
                    return false;
                }
                if (t.WallType == L5Palette.WallBase && WorldGen.genRand.Next(3) != 0) {
                    return false;
                }
                WorldGen.PlaceTile(x, y, TileID.Cobweb, mute: true);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Cobweb;
            },
        };

        //墓碑:只散上带(L5State.TombstoneMaxY),去重20(ROOMS-L5 §2.1)
        private static ScatterEntry Tombstones() => new() {
            Name = "墓碑", Density = ScatterDensity.Low,
            StandardPer100k = 4, DedupeDist = 20, MaxPlaced = 18,
            TryPlace = static (x, y) => {
                if (y > L5State.TombstoneMaxY || !OnSafeFloor(x, y)) {
                    return false;
                }
                //2x2底锚,邻列也须空
                if (Main.tile[x + 1, y].HasTile || Main.tile[x, y - 1].HasTile
                    || Main.tile[x + 1, y - 1].HasTile) {
                    return false;
                }
                return L5Palette.PlaceTombstone(x, y, WorldGen.genRand);
            },
        };

        //挂画:标档;变体墙(Slab/Tiled)走骨画族(F30/F35白送),基础墙走普通画
        private static ScatterEntry BonePaintings() => new() {
            Name = "挂画(标)", Density = ScatterDensity.Standard,
            StandardPer100k = 7, DedupeDist = 18, MaxPlaced = 36,
            TryPlace = static (x, y) => {
                if (y >= L5State.DarkZoneTop) {
                    return false;
                }
                Tile center = Main.tile[x, y];
                if (center.HasTile || !L5Palette.IsPinkDungeonWall(center.WallType)) {
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
                bool variant = center.WallType == L5Palette.WallSlab
                    || center.WallType == L5Palette.WallTiled;
                PaintingEntry entry = variant ? WorldGen.RandBonePicture() : WorldGen.RandPictureTile();
                WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == entry.tileType;
            },
        };

        //集市幡:标档,样式12/13变体墙组;无光带拒入
        private static ScatterEntry MarketBanners() => new() {
            Name = "集市幡", Density = ScatterDensity.Standard,
            StandardPer100k = 6, DedupeDist = 14, MaxPlaced = 28,
            TryPlace = static (x, y) => {
                if (y >= L5State.DarkZoneTop || !FindCeiling(x, y, out int topY)) {
                    return false;
                }
                if (!AirColumn(x, topY, 3)) {
                    return false;
                }
                int style = WorldGen.genRand.NextBool() ? L5Palette.BannerMarketA : L5Palette.BannerMarketB;
                return L5Palette.TryPlaceObject(x, topY, TileID.Banners, style);
            },
        };

        //==================== 局部验证共用件(镜像L2Scatter先例) ====================

        private static bool OnSafeFloor(int x, int y) {
            if (Main.tile[x, y].HasTile || !L5Palette.IsPinkDungeonWall(Main.tile[x, y].WallType)) {
                return false;
            }
            Tile below = Main.tile[x, y + 1];
            if (!below.HasTile || !Main.tileSolid[below.TileType] || below.TileType == TileID.Platforms) {
                return false;
            }
            //裂砖/尖刺=坑语言,杂物不许糊预告
            return below.TileType != L5Palette.CrackedBrick && below.TileType != TileID.Spikes;
        }

        private static bool FindCeiling(int x, int y, out int belowCeiling) {
            belowCeiling = 0;
            Tile self = Main.tile[x, y];
            if (self.HasTile || !L5Palette.IsPinkDungeonWall(self.WallType)) {
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

        private static bool AnySolidNeighbor(int x, int y) {
            return IsSolid(x - 1, y) || IsSolid(x + 1, y) || IsSolid(x, y - 1) || IsSolid(x, y + 1);

            static bool IsSolid(int px, int py) {
                Tile t = Main.tile[px, py];
                return t.HasTile && Main.tileSolid[t.TileType];
            }
        }
    }
}
