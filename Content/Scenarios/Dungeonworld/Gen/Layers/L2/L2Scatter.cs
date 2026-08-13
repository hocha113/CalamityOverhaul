using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    //L2层专属撒布装修数据(自包含常量表+应用方法),经ctx.Scatter声明、P55 ScatterPass统一执行
    //A路内置已覆盖:蛛网低/骨堆标/罐低(ScatterPass);本表只补L2认领母题:
    //  垂链(死铁全形态主导,附锈渍垂痕做旧签名)/链灯笼光族(标档全亮)/挂画(低档)
    //密度对位ROOMS-INDEX §7矩阵;放置一律先局部验证再落,拒绝即计失败(F30三段模式)
    internal static class L2Scatter
    {
        internal static ScatterEntry[] Entries() => [
            HangingDeadChains(),
            ChainLanterns(),
            Paintings(),
        ];

        //垂链:顶锚死铁链2~5节+链根锈渍(不发光,静止;冷粉发光=Boss房独占,INDEX §3裁决2)
        private static ScatterEntry HangingDeadChains() => new() {
            Name = "垂链", Density = ScatterDensity.Standard,
            StandardPer100k = 12, DedupeDist = 8, MaxPlaced = 60,
            TryPlace = static (x, y) => {
                if (!FindCeiling(x, y, out int anchorY)) {
                    return false;
                }
                int len = WorldGen.genRand.Next(2, 6);
                //链尾距地≥3,不糊玩家脸
                if (!AirColumn(x, anchorY, len + 3)) {
                    return false;
                }
                int placed = L2Palette.HangChain(x, anchorY, len);
                if (placed <= 0) {
                    return false;
                }
                L2Palette.RustStreak(x, anchorY + placed, WorldGen.genRand.Next(2, 4));
                return true;
            },
        };

        //链灯笼:L2光源族(与L1吊灯族分家,ROOMS-L2 §2.2);全亮,正下净空≥3(§3.2-7)
        private static ScatterEntry ChainLanterns() => new() {
            Name = "链灯笼", Density = ScatterDensity.Standard,
            StandardPer100k = 10, DedupeDist = 14, MaxPlaced = 50,
            TryPlace = static (x, y) => {
                if (!FindCeiling(x, y, out int topY)) {
                    return false;
                }
                if (!AirColumn(x, topY, 5)) {
                    return false;
                }
                return L2Palette.TryPlaceObject(x, topY, TileID.HangingLanterns,
                    L2Palette.LanternChainStyle);
            },
        };

        //挂画:低档(INDEX §7);原版随机画表,只挂在地牢墙面上
        private static ScatterEntry Paintings() => new() {
            Name = "挂画", Density = ScatterDensity.Low,
            StandardPer100k = 8, DedupeDist = 20, MaxPlaced = 12,
            TryPlace = static (x, y) => {
                Tile center = Main.tile[x, y];
                if (center.HasTile || !Main.wallDungeon[center.WallType]) {
                    return false;
                }
                //画幅最大6x4,以撒点为中心预检空腔与墙面
                for (int dx = -3; dx <= 3; dx++) {
                    for (int dy = -2; dy <= 2; dy++) {
                        Tile t = Main.tile[x + dx, y + dy];
                        if (t.HasTile || t.WallType == 0) {
                            return false;
                        }
                    }
                }
                PaintingEntry entry = WorldGen.RandPictureTile();
                WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == entry.tileType;
            },
        };

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

        //自anchorY起向下rows行全空(悬挂物净空预检)
        private static bool AirColumn(int x, int anchorY, int rows) {
            for (int i = 0; i < rows; i++) {
                int py = anchorY + i;
                if (!WorldGen.InWorld(x, py, 5) || Main.tile[x, py].HasTile) {
                    return false;
                }
            }
            return true;
        }
    }
}
