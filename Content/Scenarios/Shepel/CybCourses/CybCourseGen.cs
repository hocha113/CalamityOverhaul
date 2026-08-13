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
    //悬浮训练平台：虚空中的一块火星导管镀层甲板+装饰浮岛
    //旧灰砖走廊与其密封的内部平台层已删（玩法一直发生在顶面，内部永不可见）
    internal class CybCourseGen : GenPass
    {
        //FloorY 仍是层高锚点：世界高度/worldSurface/地狱层余量都以它推算
        internal const int FloorY = 170;
        //甲板行走面（与旧版走廊顶板同一行，出生/标靶/MK2 的落点常量不变）
        internal const int SurfaceY = FloorY - 22;
        internal const int SpawnTileX = 120;
        internal const int SpawnTileY = SurfaceY;
        internal const int GenMK2OriginX = 140;
        internal const int GenMK2OriginY = SurfaceY - 1;
        internal const int GenMK2TileLeft = GenMK2OriginX - 2;
        internal const int GenMK2TileTop = GenMK2OriginY - 2;
        internal const int GenMK2TileW = 4;
        internal const int GenMK2TileH = 3;

        //主甲板横向范围
        internal const int PlatformLeft = 70;
        internal const int PlatformRight = 330;

        //装饰浮岛 (x0, x1, 顶行)；建造与甲板灯光共用，勿双份维护
        internal static readonly (int X0, int X1, int YTop)[] AccentIslets = [
            (30, 44, SurfaceY + 26),
            (352, 366, SurfaceY + 18),
            (44, 54, SurfaceY - 32),
            (346, 354, SurfaceY - 40),
        ];

        public CybCourseGen() : base("Cyb Course Generation", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "编译超梦空间...";

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            ClearWorld(width, height);
            BuildMainDeck();
            BuildAccentIslets();
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

        //主甲板：3 厚镀层面板 + 向下收窄的船体式龙骨剪影 + 两端护沿
        private static void BuildMainDeck() {
            const int center = (PlatformLeft + PlatformRight) / 2;

            //甲板面板
            for (int x = PlatformLeft; x <= PlatformRight; x++) {
                for (int y = SurfaceY; y <= SurfaceY + 2; y++) {
                    PlaceSolid(x, y, TileID.MartianConduitPlating);
                }
            }

            //船腹逐级内收（(内收量, 起始深度, 结束深度)）
            (int inset, int d0, int d1)[] hull = [
                (8,   3, 4),
                (22,  5, 6),
                (60,  7, 8),
            ];
            foreach (var (inset, d0, d1) in hull) {
                for (int x = PlatformLeft + inset; x <= PlatformRight - inset; x++) {
                    for (int d = d0; d <= d1; d++) {
                        PlaceSolid(x, SurfaceY + d, TileID.MartianConduitPlating);
                    }
                }
            }
            //中央龙骨收尖
            for (int d = 9; d <= 12; d++) {
                int half = 34 - (d - 9) * 9;
                for (int x = center - half; x <= center + half; x++) {
                    PlaceSolid(x, SurfaceY + d, TileID.MartianConduitPlating);
                }
            }

            //两端护沿（2 高，给标靶/玩家一个边界暗示；跃出由回收守卫兜底）
            for (int i = 0; i < 2; i++) {
                PlaceSolid(PlatformLeft + i, SurfaceY - 1, TileID.MartianConduitPlating);
                PlaceSolid(PlatformLeft, SurfaceY - 2, TileID.MartianConduitPlating);
                PlaceSolid(PlatformRight - i, SurfaceY - 1, TileID.MartianConduitPlating);
                PlaceSolid(PlatformRight, SurfaceY - 2, TileID.MartianConduitPlating);
            }
        }

        //远处几块不可达的小浮板，给虚空一点纵深剪影
        private static void BuildAccentIslets() {
            foreach (var (x0, x1, yTop) in AccentIslets) {
                PlacePlate(x0, x1, yTop);
            }
        }

        //小浮板：顶行全宽，下两行内收
        private static void PlacePlate(int x0, int x1, int yTop) {
            for (int x = x0; x <= x1; x++) {
                PlaceSolid(x, yTop, TileID.MartianConduitPlating);
            }
            for (int x = x0 + 2; x <= x1 - 2; x++) {
                PlaceSolid(x, yTop + 1, TileID.MartianConduitPlating);
            }
            for (int x = x0 + 5; x <= x1 - 5; x++) {
                PlaceSolid(x, yTop + 2, TileID.MartianConduitPlating);
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
