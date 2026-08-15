using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //L4层专属撒布装修(经ctx.Scatter声明、P55 ScatterPass统一执行,F30三段模式)
    //密度对位ROOMS-INDEX §7矩阵L4列:灯=干道标/水下零,挂画=低,旗帜=零,蛛网=零,
    //地面杂物=标(沉链/罐);验证一律以"绿地牢墙面"为界,天然不越界到M0蓝底区
    internal static class L4Scatter
    {
        internal static ScatterEntry[] Entries() => [
            SunkenChains(),
            MossDaubs(),
            SconceLights(),
            DungeonPotsWetDry(),
            Paintings(),
        ];

        //水下沉链:INDEX §3裁决的L4锁链唯一许可形态(横躺地面,与L2垂链两轴差异)
        //只落在水中贴地处,液体保持写入(L4Palette.LaySunkenChain不清LiquidAmount)
        private static ScatterEntry SunkenChains() => new() {
            Name = "沉链", Density = ScatterDensity.Standard,
            StandardPer100k = 14, DedupeDist = 9, MaxPlaced = 70,
            TryPlace = static (x, y) => {
                Tile t = Main.tile[x, y];
                if (t.HasTile || t.LiquidAmount == 0) {
                    return false;
                }
                Tile below = Main.tile[x, y + 1];
                if (!below.HasTile || !Main.tileSolid[below.TileType]
                    || below.TileType == TileID.Platforms) {
                    return false;
                }
                return L4Palette.LaySunkenChain(x, y, WorldGen.genRand.Next(3, 7)) >= 2;
            },
        };

        //苔藓斑:paint深绿点染绿砖露面(做旧签名的一半;密下稀上由湿邻加权实现)
        private static ScatterEntry MossDaubs() => new() {
            Name = "苔藓斑", Density = ScatterDensity.High,
            StandardPer100k = 24, DedupeDist = 5, MaxPlaced = 220,
            TryPlace = static (x, y) => {
                Tile t = Main.tile[x, y];
                if (!t.HasTile || (t.TileType != L4Palette.Brick && t.TileType != L4Palette.CrackedBrick)) {
                    return false;
                }
                bool exposed = false, wet = false;
                Probe(x + 1, y, ref exposed, ref wet);
                Probe(x - 1, y, ref exposed, ref wet);
                Probe(x, y + 1, ref exposed, ref wet);
                Probe(x, y - 1, ref exposed, ref wet);
                //水线下必染,水线上1/3染("水线下密,水线上稀",ROOMS-L4 §2.3)
                if (!exposed || (!wet && !WorldGen.genRand.NextBool(3))) {
                    return false;
                }
                return L4Palette.MossDaub(x, y) > 0;

                static void Probe(int px, int py, ref bool exposed, ref bool wet) {
                    Tile n = Main.tile[px, py];
                    if (!n.HasTile) {
                        exposed = true;
                        if (n.LiquidAmount > 0) {
                            wet = true;
                        }
                    }
                }
            },
        };

        //油布壁灯:干道"标"档(同类去重15,F33),挂点必须全干(灯体两行无液体)
        private static ScatterEntry SconceLights() => new() {
            Name = "油布壁灯", Density = ScatterDensity.Standard,
            StandardPer100k = 8, DedupeDist = 15, MaxPlaced = 40,
            TryPlace = static (x, y) => {
                if (!FindGreenCeiling(x, y, out int topY)) {
                    return false;
                }
                for (int i = 0; i < 4; i++) {
                    Tile t = Main.tile[x, topY + i];
                    if (t.HasTile || t.LiquidAmount > 0) {
                        return false;
                    }
                }
                return L4Palette.TryPlaceObject(x, topY, TileID.HangingLanterns,
                    L4Palette.LanternSconceStyle);
            },
        };

        //地牢罐:标档;干湿都放(水下罐=冲进下水道的失物,ROOMS-L4 §3彩蛋语义)
        private static ScatterEntry DungeonPotsWetDry() => new() {
            Name = "罐", Density = ScatterDensity.Standard,
            StandardPer100k = 9, DedupeDist = 10, MaxPlaced = 55,
            TryPlace = static (x, y) => {
                Tile t = Main.tile[x, y];
                if (t.HasTile) {
                    return false;
                }
                Tile below = Main.tile[x, y + 1];
                if (!below.HasTile || !Main.tileSolid[below.TileType]
                    || below.TileType == TileID.Platforms) {
                    return false;
                }
                return WorldGen.PlacePot(x, y, TileID.Pots,
                    WorldGen.genRand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax));
            },
        };

        //挂画:低档(INDEX §7),只挂干燥绿墙(潮层挂画少的叙事一致性)
        private static ScatterEntry Paintings() => new() {
            Name = "挂画", Density = ScatterDensity.Low,
            StandardPer100k = 6, DedupeDist = 22, MaxPlaced = 10,
            TryPlace = static (x, y) => {
                Tile center = Main.tile[x, y];
                if (center.HasTile || !IsGreenWall(center.WallType)) {
                    return false;
                }
                for (int dx = -3; dx <= 3; dx++) {
                    for (int dy = -2; dy <= 2; dy++) {
                        Tile t = Main.tile[x + dx, y + dy];
                        if (t.HasTile || t.WallType == WallID.None || t.LiquidAmount > 0) {
                            return false;
                        }
                    }
                }
                PaintingEntry entry = WorldGen.RandPictureTile();
                WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
                return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == entry.tileType;
            },
        };

        private static bool IsGreenWall(ushort wall) => L4Palette.IsLayerWall(wall);

        //自撒点向上找绿房实心天花(≤8行),返回悬挂起始行(镜像L2Scatter.FindCeiling)
        private static bool FindGreenCeiling(int x, int y, out int belowCeiling) {
            belowCeiling = 0;
            Tile self = Main.tile[x, y];
            if (self.HasTile || !IsGreenWall(self.WallType)) {
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
    }
}
