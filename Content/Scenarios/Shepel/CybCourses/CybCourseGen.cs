using CalamityOverhaul.Content.Industrials.Generator.Thermal;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //横向主通道+高低平台
    internal class CybCourseGen : GenPass
    {
        internal const int FloorY = 170;
        private const int RoomHeight = 20;
        private const int FloorThick = 8;
        private const int WallThick = 6;
        //走廊顶板上方=FloorY-RoomHeight-2
        internal const int SurfaceY = FloorY - RoomHeight - 2;
        internal const int SpawnTileX = 120;
        internal const int SpawnTileY = SurfaceY;
        internal const int GenMK2OriginX = 140;
        internal const int GenMK2OriginY = SurfaceY - 1;
        internal const int GenMK2TileLeft = GenMK2OriginX - 2;
        internal const int GenMK2TileTop = GenMK2OriginY - 2;
        internal const int GenMK2TileW = 4;
        internal const int GenMK2TileH = 3;

        public CybCourseGen() : base("Cyb Course Generation", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "构建超梦空间...";

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            ClearWorld(width, height);
            FillBorders(width, height);
            BuildMainCorridor(width);
            PlacePlatforms(width);
            PlaceGeneratorMK2();
            Main.spawnTileX = SpawnTileX;
            Main.spawnTileY = SpawnTileY;
            CaptureSnapshot();
        }

        private static void ClearWorld(int width, int height) {
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = false;
                    tile.WallType = WallID.None;
                    tile.LiquidAmount = 0;
                }
            }
        }

        private static void FillBorders(int width, int height) {
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < WallThick; y++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
                for (int y = height - WallThick; y < height; y++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
            }
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < WallThick; x++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
                for (int x = width - WallThick; x < width; x++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
            }
        }

        private static void BuildMainCorridor(int width) {
            int ceilY = FloorY - RoomHeight;

            for (int x = WallThick; x < width - WallThick; x++) {
                for (int y = FloorY; y < FloorY + FloorThick; y++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
                for (int y = ceilY - 2; y < ceilY; y++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
                for (int y = ceilY; y < FloorY; y++) {
                    Main.tile[x, y].WallType = WallID.IronBrick;
                }
            }
        }

        private static void PlacePlatforms(int width) {
            (int offsetX, int riseY, int w)[] platformDefs = [
                (60,  6,  24),
                (110, 10, 20),
                (155, 6,  20),
                (200, 12, 22),
                (250, 6,  20),
                (300, 10, 18),
            ];

            foreach (var (offsetX, riseY, w) in platformDefs) {
                int platY = FloorY - riseY;
                for (int x = offsetX; x < offsetX + w && x < width - WallThick; x++) {
                    PlaceSolid(x, platY, TileID.IronBrick);
                    PlaceSolid(x, platY + 1, TileID.IronBrick);
                }
            }
        }

        //MK2直写帧，绕过PlaceObject
        private static void PlaceGeneratorMK2() {
            int tileType = ModContent.TileType<ThermalGeneratorMK2Tile>();
            for (int dx = 0; dx < GenMK2TileW; dx++) {
                for (int dy = 0; dy < GenMK2TileH; dy++) {
                    int tx = GenMK2TileLeft + dx;
                    int ty = GenMK2TileTop + dy;
                    if (!WorldGen.InWorld(tx, ty)) continue;
                    Tile t = Main.tile[tx, ty];
                    t.HasTile = true;
                    t.TileType = (ushort)tileType;
                    t.TileFrameX = (short)(dx * 18);
                    t.TileFrameY = (short)(dy * 18);
                }
            }
        }

        private static void PlaceSolid(int x, int y, ushort tileType) {
            if (!WorldGen.InWorld(x, y)) {
                return;
            }
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = tileType;
        }

        private struct TileSnapshot
        {
            public ushort TileType;
            public ushort WallType;
            public short FrameX;
            public short FrameY;
            public byte LiquidAmount;
            public byte LiquidType;
            public byte Slope;
            public byte TileColor;
            public byte WallColor;
            public bool HasTile;
            public bool IsHalfBlock;
            public bool IsActuated;
        }

        private static TileSnapshot[,] _snapshot;
        private static int _snapshotWidth;
        private static int _snapshotHeight;

        private static void CaptureSnapshot() {
            int w = Main.maxTilesX;
            int h = Main.maxTilesY;
            _snapshot = new TileSnapshot[w, h];
            _snapshotWidth = w;
            _snapshotHeight = h;
            for (int x = 0; x < w; x++) {
                for (int y = 0; y < h; y++) {
                    Tile t = Main.tile[x, y];
                    _snapshot[x, y] = new TileSnapshot {
                        HasTile = t.HasTile,
                        TileType = t.TileType,
                        WallType = t.WallType,
                        FrameX = t.TileFrameX,
                        FrameY = t.TileFrameY,
                        LiquidAmount = t.LiquidAmount,
                        LiquidType = (byte)t.LiquidType,
                        Slope = (byte)t.Slope,
                        IsHalfBlock = t.IsHalfBlock,
                        IsActuated = t.IsActuated,
                        TileColor = t.TileColor,
                        WallColor = t.WallColor,
                    };
                }
            }
        }

        //RETRY回写物块+重建MK2 TP(TP非Tile字段)
        internal static void RestoreSnapshot() {
            if (_snapshot == null) {
                return;
            }
            int w = System.Math.Min(_snapshotWidth, Main.maxTilesX);
            int h = System.Math.Min(_snapshotHeight, Main.maxTilesY);

            //先Kill旧TP再回滚
            int tpID = TileProcessorLoader.GetModuleID<ThermalGeneratorMK2TP>();
            if (TPUtils.TryGetTopLeft(GenMK2OriginX, GenMK2OriginY, out Point16 topLeft)) {
                var existing = TileProcessorLoader.FindModulePreciseSearch(tpID, topLeft);
                existing?.Kill();
            }


            for (int x = 0; x < w; x++) {
                for (int y = 0; y < h; y++) {
                    var s = _snapshot[x, y];
                    Tile t = Main.tile[x, y];
                    t.HasTile = s.HasTile;
                    t.TileType = s.TileType;
                    t.WallType = s.WallType;
                    t.TileFrameX = s.FrameX;
                    t.TileFrameY = s.FrameY;
                    t.LiquidAmount = s.LiquidAmount;
                    t.LiquidType = s.LiquidType;
                    t.Slope = (SlopeType)s.Slope;
                    t.IsHalfBlock = s.IsHalfBlock;
                    t.IsActuated = s.IsActuated;
                    t.TileColor = s.TileColor;
                    t.WallColor = s.WallColor;
                }
            }

            int mk2TileType = ModContent.TileType<ThermalGeneratorMK2Tile>();
            TileProcessorLoader.AddInWorld(mk2TileType, new Point16(GenMK2TileLeft, GenMK2TileTop), null);

            Main.refreshMap = true;
            WorldGen.RangeFrame(0, 0, w - 1, h - 1);
        }

        //离开子世界释放大数组
        internal static void ClearSnapshot() {
            _snapshot = null;
            _snapshotWidth = 0;
            _snapshotHeight = 0;
        }
    }
}
