using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 泄洪堂：不溺者的专属 Boss 房（L4 水牢演出型唯一建筑，镜像 GaolBossRoom 全套纪律：
    /// 字符画 prefab + 语义槽、行长断言 fail loud、家具走 PlaceObject 拒绝记日志、
    /// 末尾 RangeFrame、Place() 落成即向看守注册）。
    /// 几何即相位表：两道刻度线（rel 29 / rel 16）是战斗水位的实体化预告，
    /// 双壁架（rel 28）恰高出刻度 1 水面一格，双砖柱（顶 rel 12）高出刻度 2 水面四格。
    /// 生成期房间是干的：一切液体由看守运行期一次性写入（绕开 L4 生成期水体审计口径，
    /// 不注册 L4WaterWorks 全局舱段表，全局阀切换永远碰不到本房水体）。
    /// </summary>
    internal static class FloodGalleryRoom
    {
        //==================== 尺寸与语义槽（tile 坐标，相对 prefab 左上角）====================

        internal const int Width = 74;
        internal const int Height = 48;

        /// <summary>左右门插槽：Archway 3 深 × 4 高，底沿与室内地板齐平</summary>
        internal static readonly Point LeftDoorOffset = new(0, 38);
        internal static readonly Point RightDoorOffset = new(71, 38);
        internal const int DoorHeight = 4;

        /// <summary>内膛开区列界（含）与地板顶行</summary>
        internal const int InteriorLeft = 3;
        internal const int InteriorRight = 70;
        internal const int InteriorTop = 3;
        internal const int FloorRel = 42;

        /// <summary>三档水位的水面行（rel；水占 [Surface, FloorRel) 内非实心格）</summary>
        internal const int AnkleSurfaceRel = 41;
        internal const int Scale1SurfaceRel = 29;
        internal const int Scale2SurfaceRel = 16;
        /// <summary>踝水浅量（255=整格；踝水只到小腿）</summary>
        internal const byte AnkleAmount = 140;

        /// <summary>王座壁龛锚（蛰伏体坐位）与阀台触发区（3×3，站立 30t 触发）</summary>
        internal static readonly Point ThroneOffset = new(67, 39);
        internal static readonly Point ValveOffset = new(12, 39);
        /// <summary>排水格栅横带：row=FloorRel, cols [GrateLeft, GrateRight]（战利品落点）</summary>
        internal const int GrateLeft = 33;
        internal const int GrateRight = 40;

        /// <summary>立管双柱（涨水喷雾的墙面水口，纯墙面语义）</summary>
        internal const int PipeLeftCol = 13;
        internal const int PipeRightCol = 59;
        internal const int PipeTopRel = 3;
        internal const int PipeBottomRel = 28;

        /// <summary>双砖柱（P3 立足点）：左缘列与宽度、柱顶行</summary>
        internal const int PillarLeftCol = 17;
        internal const int PillarRightCol = 51;
        internal const int PillarWidth = 6;
        internal const int PillarTopRel = 12;

        internal static Rectangle Bounds(Point origin) => new(origin.X, origin.Y, Width, Height);

        /// <summary>王座槽世界像素（蛰伏体/换体锚点）</summary>
        internal static Vector2 ThroneWorldPos(Point origin)
            => new((origin.X + ThroneOffset.X) * 16f + 8f, (origin.Y + ThroneOffset.Y) * 16f + 8f);

        /// <summary>阀台触发区世界矩形（像素，3×3 格）</summary>
        internal static Rectangle ValveZoneWorld(Point origin)
            => new((origin.X + ValveOffset.X - 1) * 16, (origin.Y + ValveOffset.Y - 1) * 16, 48, 48);

        /// <summary>格栅中心世界像素（死亡演出泄洪口 + 战利品落点）</summary>
        internal static Vector2 GrateWorldPos(Point origin)
            => new((origin.X + (GrateLeft + GrateRight + 1) * 0.5f) * 16f, (origin.Y + FloorRel) * 16f);

        /// <summary>指定水位档的水面世界 Y（像素，水面=该行顶）</summary>
        internal static float SurfaceWorldY(Point origin, int surfaceRel)
            => (origin.Y + surfaceRel) * 16f;

        //==================== 字符画（图例见 Place 的 switch；行长运行期断言，fail loud）====================
        //# 实心绿砖  . 空+绿墙  , 空+板岩绿墙(王座龛背景)  : 空+瓷面绿墙(顶拱带)
        //| 空+瓷面墙+灰漆(立管)  = 空+瓷面墙+深蓝漆(水位刻度线)  G 绿砖平台+灰漆(排水格栅)
        //D 门插槽(空,语义由偏移常量承载)

        private static readonly string[] Rows = BuildRows();

        /// <summary>
        /// 行模板拼装（等价于手绘字符画，改用计数拼接根除手写 74 列的宽度事故；
        /// ValidatePrefab 仍对每行做行长断言，双保险）。
        /// 布局（rel 行）：0~2 壳顶 / 3~8 顶拱带 / 3~28 立管纵带 / 12 起双砖柱 /
        /// 16 与 29 水位刻度线 / 28 双壁架 / 34~41 王座壁龛 / 38~41 门插槽 /
        /// 41 阀台 / 42 地板顶（33~40 排水格栅）/ 43~47 地板体与壳底。
        /// </summary>
        private static string[] BuildRows() {
            string solid = new('#', Width);
            //顶拱带（含立管）：3..12 拱(10) | 13..14 管 | 15..58 拱(44) | 59..60 管 | 61..70 拱(10)
            string arch = "###" + new string(':', 10) + "||" + new string(':', 44) + "||" + new string(':', 10) + "###";
            //开区（含立管，柱前）
            string openPipe = "###" + new string('.', 10) + "||" + new string('.', 44) + "||" + new string('.', 10) + "###";
            //开区（含立管+双柱）：17..22 与 51..56 砖柱
            string pillarPipe = "###" + new string('.', 10) + "||" + ".." + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + ".." + "||" + new string('.', 10) + "###";
            //刻度线行（柱体照旧实心）
            string scale = "###" + new string('=', 14) + new string('#', 6)
                + new string('=', 28) + new string('#', 6) + new string('=', 14) + "###";
            //壁架行：3..10 与 63..70 各 8 宽实砖
            string ledge = "###" + new string('#', 8) + ".." + "||" + ".." + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + ".." + "||" + ".." + new string('#', 8) + "###";
            //开区（双柱，立管已止）
            string pillar = "###" + new string('.', 14) + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + new string('.', 14) + "###";
            //王座龛楣行：64..70 砖楣
            string lintel = "###" + new string('.', 14) + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + new string('.', 7) + new string('#', 7) + "###";
            //王座龛上半：64 门柱 + 65..70 板岩背景
            string nicheJamb = "###" + new string('.', 14) + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + new string('.', 7) + "#" + new string(',', 6) + "###";
            //门插槽行（龛下半开口）
            string door = "DDD" + new string('.', 14) + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + new string('.', 8) + new string(',', 6) + "DDD";
            //门插槽底行 + 阀台（11..13 一格高台阶）
            string dais = "DDD" + new string('.', 8) + "###" + "..." + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + new string('.', 8) + new string(',', 6) + "DDD";
            //地板顶行：33..40 排水格栅
            string grate = "###" + new string('#', 30) + new string('G', 8) + new string('#', 30) + "###";

            var rows = new string[Height];
            for (int i = 0; i < Height; i++) {
                rows[i] = i switch {
                    <= 2 => solid,
                    <= 8 => arch,
                    <= 11 => openPipe,
                    <= 15 => pillarPipe,
                    16 => scale,
                    <= 27 => pillarPipe,
                    28 => ledge,
                    29 => scale,
                    <= 33 => pillar,
                    34 => lintel,
                    <= 37 => nicheJamb,
                    <= 40 => door,
                    41 => dais,
                    42 => grate,
                    _ => solid,
                };
            }
            return rows;
        }

        //==================== 材质常量 ====================

        private const ushort Brick = TileID.GreenDungeonBrick;
        private const ushort WallBase = WallID.GreenDungeonUnsafe;
        private const ushort WallSlab = WallID.GreenDungeonSlabUnsafe;
        private const ushort WallTile = WallID.GreenDungeonTileUnsafe;
        /// <summary>绿砖平台 placeStyle=8（原版 Item 1386），平台帧 = style*18</summary>
        private const short GreenPlatformFrameY = 8 * 18;

        //==================== 构建 ====================

        /// <summary>
        /// 在 origin（tile 左上角）落一间泄洪堂并登记到运行时看守。
        /// 生成期与运行期（测试钥匙）通用；运行期联机的区块同步由调用方负责。
        /// 生成期零液体：踝水由看守 arm 时运行期写入。
        /// </summary>
        internal static void Place(int originX, int originY) {
            ValidatePrefab();

            for (int ry = 0; ry < Height; ry++) {
                string row = Rows[ry];
                for (int rx = 0; rx < Width; rx++) {
                    int x = originX + rx;
                    int y = originY + ry;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    char ch = row[rx];
                    switch (ch) {
                        case '#':
                            SetSolid(x, y, Brick, WallSlab);
                            break;
                        case '.':
                            ClearCell(x, y, WallBase);
                            break;
                        case ',':
                            ClearCell(x, y, WallSlab);
                            break;
                        case ':':
                            ClearCell(x, y, WallTile);
                            break;
                        case '|':
                            //立管：瓷面墙 + 灰漆纵带，涨水前它先喷雾轰鸣（预告在前）
                            ClearCell(x, y, WallTile);
                            WorldGen.paintWall(x, y, PaintID.GrayPaint);
                            break;
                        case '=':
                            //水位刻度线：telegraph 实体化，水永远只涨到下一道可见刻度
                            ClearCell(x, y, WallTile);
                            WorldGen.paintWall(x, y, PaintID.DeepBluePaint);
                            break;
                        case 'G':
                            //排水格栅：绿砖平台 + 灰漆（死亡演出的泄洪口，战利品落点）
                            SetPlatform(x, y, WallSlab);
                            WorldGen.paintTile(x, y, PaintID.GrayPaint);
                            break;
                        case 'D':
                            ClearCell(x, y, WallBase);
                            break;
                        default:
                            CWRMod.Instance.Logger.Warn(
                                $"[FloodGalleryRoom] 未知图例字符 '{ch}' at ({rx},{ry})");
                            break;
                    }
                }
            }

            //装修遍：阀台拉杆（纯装饰，真正的触发是站立判定）。
            //杆下埋一粒隐形红线：DungeonworldValveTile 的 HasWireNearby 会因此跳过它，
            //玩家右键只翻杆面，绝不会误触 L4 全局水位机关
            int leverX = originX + ValveOffset.X;
            int leverY = originY + ValveOffset.Y + 2;
            Tile leverWireTile = Main.tile[leverX, leverY];
            leverWireTile.RedWire = true;
            bool leverOk = TryPlaceObject(leverX, leverY - 2, TileID.Lever, 0);
            if (!leverOk) {
                CWRMod.Instance.Logger.Warn(
                    $"[FloodGalleryRoom] 阀台拉杆放置失败 at tile ({leverX},{leverY - 2})，站立触发不受影响");
            }

            //自框收尾：直写区域全量帧修（生成期 P80 会再跑一遍，重复无害）
            WorldGen.RangeFrame(originX - 1, originY - 1, originX + Width + 1, originY + Height + 1);

            FloodGalleryWatcher.RegisterRoom(new Point(originX, originY));
            //刷怪静默区（IMPL-D 接口，门禁自检；Boss 房内不刷普通敌怪）
            NPCs.Elites.DungeonworldEliteDirector.RegisterQuietZone(
                Bounds(new Point(originX, originY)), 12, "泄洪堂");
            CWRMod.Instance.Logger.Info(
                $"[FloodGalleryRoom] 落成 origin=({originX},{originY}) 拉杆={(leverOk ? "成" : "拒")}");
        }

        //==================== 受约束写入（镜像 GaolBossRoom 语义，自包含）====================

        private static void SetSolid(int x, int y, ushort type, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
        }

        private static void ClearCell(int x, int y, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
        }

        private static void SetPlatform(int x, int y, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Platforms;
            tile.TileFrameY = GreenPlatformFrameY;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
        }

        /// <summary>多格挂件锚定放置：原点纵向试两格，成功以场上出现该 tile 为准</summary>
        private static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }

        //==================== 校验（构造性保证优先，失败即硬错误）====================

        private static bool validated;

        private static void ValidatePrefab() {
            if (validated) {
                return;
            }
            if (Rows.Length != Height) {
                throw new InvalidOperationException(
                    $"[FloodGalleryRoom] prefab 行数 {Rows.Length} != Height {Height}");
            }
            for (int i = 0; i < Rows.Length; i++) {
                if (Rows[i].Length != Width) {
                    throw new InvalidOperationException(
                        $"[FloodGalleryRoom] prefab 第 {i} 行长 {Rows[i].Length} != Width {Width}");
                }
            }
            validated = true;
        }
    }
}
