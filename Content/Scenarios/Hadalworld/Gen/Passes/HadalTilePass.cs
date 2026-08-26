using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Passes
{
    //P20:模型→物块/墙/静水直写(薄壳:读格落块,零随机零厚逻辑)
    //墙规则:逐列自上而下,首遇实心后才开始涂墙(开阔水柱无墙,洞内有墙)
    //出生点协议:写Main.spawnTileX/Y与HadalworldMetrics.SpawnTile(brief §2)
    internal class HadalTilePass : GenPass
    {
        public HadalTilePass() : base("Hadalworld Tiles", 4f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "浇筑深渊与灌注静水...";
            HadalTerrainModel model = HadalGenContext.Model;
            int width = HadalworldMetrics.Width;
            int height = HadalworldMetrics.Height;

            for (int x = 0; x < width; x++) {
                progress.Set(x / (double)(width - 1));
                bool wallOn = false;
                for (int y = 0; y < height; y++) {
                    Tile tile = Main.tile[x, y];
                    HadalMat mat = model.At(x, y);
                    if (mat != HadalMat.None) {
                        if (y > 60) {
                            wallOn = true; //顶部封板不算"入岩",海面水柱保持无墙
                        }
                        tile.HasTile = true;
                        tile.TileType = MapTile(mat);
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;
                        tile.LiquidAmount = 0;
                        HadalGenContext.SolidWrites++;
                    }
                    else {
                        tile.HasTile = false;
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;
                        if (model.IsWater(x, y)) {
                            tile.LiquidType = LiquidID.Water;
                            tile.LiquidAmount = byte.MaxValue;
                            HadalGenContext.WaterWrites++;
                        }
                        else {
                            tile.LiquidAmount = 0;
                            HadalGenContext.AirWrites++;
                        }
                    }
                    ushort wall = wallOn ? MapWall(model, mat, x, y) : WallID.None;
                    tile.WallType = wall;
                    if (wall != WallID.None) {
                        HadalGenContext.WallWrites++;
                    }
                }
            }

            //出生点=气穴房地板实心行(spawnTileY语义:脚落该行顶,镜像Dungeonworld)
            Main.spawnTileX = model.SpawnX;
            Main.spawnTileY = model.SpawnY;
            HadalworldMetrics.SpawnTile = new(model.SpawnX, model.SpawnY);

            CWRMod.Instance.Logger.Info(
                $"[Hadalworld] P20 Tiles solid={HadalGenContext.SolidWrites}"
                + $" water={HadalGenContext.WaterWrites} air={HadalGenContext.AirWrites}"
                + $" walls={HadalGenContext.WallWrites} spawn=({Main.spawnTileX},{Main.spawnTileY})");
        }

        private static ushort MapTile(HadalMat mat) => mat switch {
            HadalMat.Sand => TileID.Sand,
            HadalMat.HardSand => TileID.HardenedSand,
            HadalMat.Sandstone => TileID.Sandstone,
            HadalMat.Silt => TileID.Silt,
            HadalMat.Clay => TileID.ClayBlock,
            HadalMat.Mud => TileID.Mud,
            HadalMat.MushroomMud => TileID.MushroomGrass,
            HadalMat.Granite => TileID.Granite,
            HadalMat.Obsidian => TileID.Obsidian,
            HadalMat.Ash => TileID.Ash,
            HadalMat.RoomShell => TileID.Sandstone,
            _ => TileID.Stone,
        };

        //材质有主见的跟材质,其余按分带斑块哈希取墙(patch=9x7格,免像素噪)
        private static ushort MapWall(HadalTerrainModel model, HadalMat mat, int x, int y) {
            switch (mat) {
                case HadalMat.Sand:
                case HadalMat.HardSand:
                    return WallID.HardenedSand;
                case HadalMat.Sandstone:
                    return WallID.Sandstone;
                case HadalMat.Mud:
                case HadalMat.MushroomMud:
                    return WallID.MudUnsafe;
                case HadalMat.RoomShell:
                    return WallID.SmoothSandstone;
            }
            //出生房内膛:人工凿室感
            if (mat == HadalMat.None && model.IsAirPocket(x, y)) {
                return WallID.SmoothSandstone;
            }
            int patch = ((x / 9) * 73856093 ^ (y / 7) * 19349663) & int.MaxValue;
            return HadalworldMetrics.GetZone(y) switch {
                HadalZone.Sky or HadalZone.Sunlit => _sunlitWalls[patch % _sunlitWalls.Length],
                HadalZone.Twilight => _twilightWalls[patch % _twilightWalls.Length],
                HadalZone.Midnight => _midnightWalls[patch % _midnightWalls.Length],
                HadalZone.Abyssal => _abyssalWalls[patch % _abyssalWalls.Length],
                _ => _hadalWalls[patch % _hadalWalls.Length],
            };
        }

        private static readonly ushort[] _sunlitWalls = [WallID.HardenedSand, WallID.HardenedSand, WallID.Sandstone];
        private static readonly ushort[] _twilightWalls = [
            WallID.DirtUnsafe1, WallID.DirtUnsafe2, WallID.DirtUnsafe3, WallID.DirtUnsafe4,
            WallID.CaveWall, WallID.CaveWall2,
        ];
        private static readonly ushort[] _midnightWalls = [
            WallID.RocksUnsafe1, WallID.RocksUnsafe2, WallID.RocksUnsafe3, WallID.RocksUnsafe4,
            WallID.CaveUnsafe, WallID.Cave6Unsafe,
        ];
        private static readonly ushort[] _abyssalWalls = [
            WallID.GraniteUnsafe, WallID.GraniteUnsafe, WallID.RocksUnsafe1, WallID.RocksUnsafe3,
        ];
        private static readonly ushort[] _hadalWalls = [
            WallID.ObsidianBackUnsafe, WallID.ObsidianBackUnsafe, WallID.GraniteUnsafe,
        ];
    }
}
