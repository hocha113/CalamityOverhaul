using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 深牢禁室：深牢怨灵的专属 Boss 房（牢狱层演出型唯一建筑，STRUCTURES §2.2 归档为
    /// 字符画 prefab + 语义槽）。自包含构建器：几何直写 + 家具语义槽统一走
    /// WorldGen.PlaceObject/PlaceTile（锚定校验免费获得，放不下记日志跳过），
    /// 末尾自框（RangeFrame）。不依赖 Gen\ 其他类型，A 路只需一行注册 + 一处传坐标。
    /// 尺寸推导（像素数值取自 DeepGaolWraith 实装招式）：
    /// 内膛宽 56 格=896px ≥ 囚笼直径 520px + 两侧走位 ≥180px；
    /// 内膛高 35 格=560px ≥ 囚笼直径 520px（笼心在玩家，落地时笼底咬进地板属预期）；
    /// 横贯拉锁半长 620px 超出房宽，锚头钉进侧墙正是语义本体；
    /// 隐袭闪现 ±260px 贴墙时可能没入墙体，怨灵为穿墙鬼魂，属人设内行为。
    /// </summary>
    internal static class GaolBossRoom
    {
        //==================== 尺寸与语义槽（tile 坐标，相对 prefab 左上角）====================

        internal const int Width = 62;
        internal const int Height = 42;

        /// <summary>祭坛槽：蛰伏骷髅头悬停中心所在格（镣铐台上方 3 格）</summary>
        internal static readonly Point AltarOffset = new(30, 33);
        /// <summary>左右门插槽：Archway 3 深 × 4 高，底沿与室内地板齐平（STRUCTURES §2.5）</summary>
        internal static readonly Point LeftDoorOffset = new(0, 34);
        internal static readonly Point RightDoorOffset = new(59, 34);
        internal const int DoorHeight = 4;

        /// <summary>祭坛槽的世界像素坐标（骷髅头/怨灵的锚点）</summary>
        internal static Vector2 AltarWorldPos(Point origin)
            => new((origin.X + AltarOffset.X) * 16f + 16f, (origin.Y + AltarOffset.Y) * 16f + 8f);

        internal static Rectangle Bounds(Point origin) => new(origin.X, origin.Y, Width, Height);

        //==================== 字符画（图例见 ParseCell；行长运行期断言，fail loud）====================
        //# 实心粉砖  . 空+粉墙  , 空+板岩粉墙(祭坛背景带)  : 空+瓷面粉墙(顶拱带)
        //- 粉砖平台  h 垂链(tile 214)  D 门插槽(空,登记)  A 祭坛槽(空,登记)
        //L 笼式吊灯槽  b 旗帜槽  c 水蜡烛槽（家具槽落墙随左邻几何字符）

        private static readonly string[] Rows = [
            "##############################################################",
            "##############################################################",
            "##############################################################",
            "###:::::h:::L::h::::b:::h:::::L::::::h:::b::::h::L:::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###............h........h............h........h............###",
            "###............h........h............h........h............###",
            "###............h........h............h........h............###",
            "###............h........h............h........h............###",
            "###.....................h............h.....................###",
            "###.....................h............h.....................###",
            "###.....................h............h.....................###",
            "###.....................h............h.....................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###........................................................###",
            "###-------...............,,,,,,,,,,,,...............-------###",
            "###......................,,,,,,,,,,,,......................###",
            "###......c...............,,,,,,,,,,,,...............c......###",
            "###.....####.............,,,,,,,,,,,,.............####.....###",
            "###......##..............,,,,,,,,,,,,..............##......###",
            "###......##..............,,,,,,,,,,,,..............##......###",
            "###----..##..............,,,,,,,,,,,,..............##..----###",
            "###......##..............,,,,,A,,,,,,..............##......###",
            "DDD......##..............,,,,,,,,,,,,..............##......DDD",
            "DDD......##..............,,,,,,,,,,,,..............##......DDD",
            "DDD......##..............,c,######,c,..............##......DDD",
            "DDD......##..............,##########,..............##......DDD",
            "##############################################################",
            "##############################################################",
            "##############################################################",
            "##############################################################",
        ];

        //==================== 材质常量 ====================

        private const ushort Brick = TileID.PinkDungeonBrick;
        private const ushort WallBase = WallID.PinkDungeonUnsafe;
        private const ushort WallSlab = WallID.PinkDungeonSlabUnsafe;
        private const ushort WallTile = WallID.PinkDungeonTileUnsafe;
        /// <summary>粉砖平台 placeStyle=7（原版 Item 1385），平台帧 = style*18</summary>
        private const short PinkPlatformFrameY = 7 * 18;

        //==================== 构建 ====================

        /// <summary>
        /// 在 origin（tile 左上角）落一间深牢禁室并登记到运行时注册表。
        /// 生成期与运行期（测试物品）通用；运行期联机的区块同步由调用方负责。
        /// </summary>
        internal static void Place(int originX, int originY) {
            ValidatePrefab();

            var furniture = new List<(int x, int y, char kind)>();

            //几何遍（含垂链，链由构造保证顶锚：h 只出现在实心天花或上一节链之下）
            for (int ry = 0; ry < Height; ry++) {
                string row = Rows[ry];
                ushort lastWall = WallBase;
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
                            lastWall = WallBase;
                            break;
                        case ',':
                            ClearCell(x, y, WallSlab);
                            lastWall = WallSlab;
                            break;
                        case ':':
                            ClearCell(x, y, WallTile);
                            lastWall = WallTile;
                            break;
                        case '-':
                            SetPlatform(x, y, lastWall);
                            break;
                        case 'h':
                            SetChain(x, y, lastWall);
                            break;
                        case 'D':
                        case 'A':
                            //门/祭坛都是空格，语义由偏移常量与注册表承载
                            ClearCell(x, y, ch == 'A' ? WallSlab : WallBase);
                            break;
                        case 'L':
                        case 'b':
                        case 'c':
                            //家具槽先清空落墙（随左邻），坐标记下来等几何冻结后统一放置
                            ClearCell(x, y, lastWall);
                            furniture.Add((x, y, ch));
                            break;
                        default:
                            CWRMod.Instance.Logger.Warn(
                                $"[GaolBossRoom] 未知图例字符 '{ch}' at ({rx},{ry})");
                            break;
                    }
                }
            }

            //装修遍：几何已冻结，语义槽统一走合法锚定，放不下记日志跳过（防退化纪律）
            int placed = 0, failed = 0;
            foreach ((int x, int y, char kind) in furniture) {
                bool ok = kind switch {
                    'L' => TryPlaceObject(x, y, TileID.HangingLanterns, 2),
                    'b' => TryPlaceObject(x, y, TileID.Banners, WorldGen.genRand.Next(4)),
                    'c' => WorldGen.PlaceTile(x, y, TileID.WaterCandle, mute: true)
                           && Main.tile[x, y].TileType == TileID.WaterCandle,
                    _ => false,
                };
                if (ok) {
                    placed++;
                }
                else {
                    failed++;
                    CWRMod.Instance.Logger.Warn(
                        $"[GaolBossRoom] 家具槽 '{kind}' 放置失败 at tile ({x},{y})");
                }
            }

            //自框收尾：直写区域全量帧修（生成期 P80 会再跑一遍，重复无害）
            WorldGen.RangeFrame(originX - 1, originY - 1, originX + Width + 1, originY + Height + 1);

            GaolBossRoomWatcher.RegisterRoom(new Point(originX, originY));
            CWRMod.Instance.Logger.Info(
                $"[GaolBossRoom] 落成 origin=({originX},{originY}) 家具 {placed} 放置 / {failed} 拒绝");
        }

        //==================== 受约束写入（镜像 TileBrush 语义，自包含免依赖 A 的 WIP）====================

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
            tile.TileFrameY = PinkPlatformFrameY;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.WallType = wall;
            tile.LiquidAmount = 0;
        }

        private static void SetChain(int x, int y, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Chain;
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
                    $"[GaolBossRoom] prefab 行数 {Rows.Length} != Height {Height}");
            }
            for (int i = 0; i < Rows.Length; i++) {
                if (Rows[i].Length != Width) {
                    throw new InvalidOperationException(
                        $"[GaolBossRoom] prefab 第 {i} 行长 {Rows[i].Length} != Width {Width}");
                }
            }
            validated = true;
        }
    }
}
