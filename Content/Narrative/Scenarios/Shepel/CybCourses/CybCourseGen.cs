using CalamityOverhaul.Content.Industrials.Generator.Thermal;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    //超梦教程关卡的世界地形生成
    //布局：一条主通道从左延伸到右，中间有若干高低平台和隔断墙，整体走向横向
    internal class CybCourseGen : GenPass
    {
        //主通道地板Y坐标（从顶部算）
        internal const int FloorY = 170;
        //通道净高（空气格数）
        private const int RoomHeight = 20;
        //地面厚度
        private const int FloorThick = 8;
        //墙面厚度（左右边界）
        private const int WallThick = 6;
        //玩家所在地表tile Y（走廊顶板上方，= FloorY - RoomHeight - 2）
        internal const int SurfaceY = FloorY - RoomHeight - 2;
        //玩家初始 spawn tile 坐标（与 ApplyPass 末尾设置保持一致，便于 RETRY 重置位置时复用）
        internal const int SpawnTileX = 120;
        internal const int SpawnTileY = SurfaceY;
        //热能发电机MK2放置参数（PlaceObject origin坐标和tile范围）
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

            //第一步：清空全图所有默认方块和背景墙，让天空着色器透出来
            ClearWorld(width, height);

            //铺设封闭边界（上下左右厚板）
            FillBorders(width, height);

            //生成主通道地板和顶板，通道内壁使用铁砖墙
            BuildMainCorridor(width);

            //在主通道内放置若干高低平台
            PlacePlatforms(width);
            //放置热能发电机MK2作为物块骇入教学目标
            PlaceGeneratorMK2();
            //设置出生点到走廊顶板上方，玩家站在通道顶部地面上
            Main.spawnTileX = SpawnTileX;
            Main.spawnTileY = SpawnTileY;

            //生成完成后立即对全图物块拍快照，供软重启（RETRY）回滚使用
            CaptureSnapshot();
        }

        //清空全图：移除所有默认方块和背景墙，背景墙清为0（无墙）让天空可见
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

        //填充外边界（上下左右），使用灰砖构成硬边框
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

        //生成主通道：地板+顶板+背景墙换成铁砖墙以区分内外
        private static void BuildMainCorridor(int width) {
            int ceilY = FloorY - RoomHeight;

            for (int x = WallThick; x < width - WallThick; x++) {
                //地板
                for (int y = FloorY; y < FloorY + FloorThick; y++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
                //顶板（薄一些，2格）
                for (int y = ceilY - 2; y < ceilY; y++) {
                    PlaceSolid(x, y, TileID.GrayBrick);
                }
                //通道内部换为铁砖背景墙，视觉上更有金属感
                for (int y = ceilY; y < FloorY; y++) {
                    Main.tile[x, y].WallType = WallID.IronBrick;
                }
            }
        }

        //在主通道中铺设若干平台，制造高低起伏的教程路线
        //平台使用铁砖方块，高度交错，宽度固定为20格，间距20格
        private static void PlacePlatforms(int width) {
            //平台参数：[起始X偏移, 高于地板的格数, 宽度]
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
                    //平台主体（2格厚）
                    PlaceSolid(x, platY, TileID.IronBrick);
                    PlaceSolid(x, platY + 1, TileID.IronBrick);
                }
            }
        }

        //在走廊右侧放置热能发电机MK2，作为物块扫描教学目标
        //直写帧数据绕过 PlaceObject 检查
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

        //物块状态快照：仅在 RETRY 时使用，记录全图每格的关键字段以便原样回滚
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

        //快照尺寸与数组：以拍摄时的世界尺寸为准，避免未来子世界尺寸变更后越界
        private static TileSnapshot[,] _snapshot;
        private static int _snapshotWidth;
        private static int _snapshotHeight;

        //在 ApplyPass 末尾对全图物块逐格拍照，包含 TileType/帧坐标/液体/坡度/着色等
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

        //RETRY 时调用：把快照中的物块字段原样写回，再补回热能发电机MK2 的 TP 实体
        //（TP 不属于 Tile 字段，必须手动重建）
        internal static void RestoreSnapshot() {
            if (_snapshot == null) {
                return;
            }
            int w = System.Math.Min(_snapshotWidth, Main.maxTilesX);
            int h = System.Math.Min(_snapshotHeight, Main.maxTilesY);

            //先清掉MK2 当前的 TP，再回滚物块，最后重新挂载新 TP，避免出现孤儿引用
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

            //快照里 MK2 的帧数据已经回写完成，这里只需重新生成 TP 实体
            int mk2TileType = ModContent.TileType<ThermalGeneratorMK2Tile>();
            TileProcessorLoader.AddInWorld(mk2TileType, new Point16(GenMK2TileLeft, GenMK2TileTop), null);

            //刷新光照与小地图，让肉眼立刻看到回滚后的世界
            Main.refreshMap = true;
            WorldGen.RangeFrame(0, 0, w - 1, h - 1);
        }
    }
}
