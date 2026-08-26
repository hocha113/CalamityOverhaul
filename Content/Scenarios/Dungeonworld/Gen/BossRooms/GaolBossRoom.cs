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
    /// 内膛高阶梯拱顶 33~36 格（中央 36 格=576px）≥ 囚笼直径 520px
    /// （笼心在玩家，落地时笼底咬进地板属预期；两侧收分仅蚀 7 列 x 2 行檐角，
    /// 低于 RoomShell.CeilingSetback 对常规房的许可量，不侵开阔区）；
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

        //==================== 字符画（图例见 Place 的解析 switch；行长运行期断言，fail loud）====================
        //# 实心粉砖  . 空+粉墙  , 空+板岩粉墙(祭坛背景带)  : 空+瓷面粉墙(顶拱带)
        //- 粉砖平台  h 垂链(tile 214)  D 门插槽(空,登记)  A 祭坛槽(空,登记)
        //L 笼式吊灯槽  b 旗帜槽  c 水蜡烛槽（家具槽落墙随左邻几何字符）
        //1~4 斜切粉砖(SlopeType 同值:1=左下实 2=右下实 3=左上实 4=右上实;
        //  顶拱角取向对齐 RoomShell.CornerArch 既有用法:左角4右角3,座沿左2右1)
        //彩玻墙层(仅动墙,不动碰撞): o 红彩玻(玫瑰窗芯) g 红彩玻+粉漆(花瓣)
        //  e 红彩玻+深粉漆(窗缘) m 板岩+灰漆(铅条/窗棂/垂带)
        //构图:阶梯拱顶自两侧收分至中央顶窗带,玫瑰窗(rx25-36,ry10-21)悬于祭坛正上,
        //  铅条垂带(m,rx30-31)把窗光"接"到祭坛背景带;两侧尖拱窄窗(rx17-19/42-44)
        //  中柱压一根铅条,读作牢窗铁栏;链长参差,锁链母题主导(ROOMS-L2 §主题锚)。

        private static readonly string[] Rows = [
            "##############################################################",
            "##############################################################",
            "#########################4:b::L:::b:3#########################",
            "#################4:::L::h::::::::::::h::L:::3#################",
            "#########4:::::h::::::::h::::::::::::h::::::::h:::::3#########",
            "###4::::h::::::h::::::::h::::::::::::h::::::::h::::::h::::3###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###:::::h::::::h::::::::h::::::::::::h::::::::h::::::h:::::###",
            "###.....h......h........h............h........h......h.....###",
            "###.....h......h........h....emme....h...............h.....###",
            "###.....h......h...........eggmmgge..h...............h.....###",
            "###.....h......h..........egggmmggge.h...............h.....###",
            "###............h..........egggmmggge.h...............h.....###",
            "###............h.........eggggggggggeh.....................###",
            "###............h.........emmmgoogmmmeh.....................###",
            "###............h.........emmmgoogmmmeh.....................###",
            "###......................egggggggggge......................###",
            "###.......................egggmmggge.......................###",
            "###.......................egggmmggge.......................###",
            "###........................eggmmgge........................###",
            "###..........................emme..........................###",
            "###...........................mm...........................###",
            "###...........................mm...........................###",
            "###...............e...........mm...........e...............###",
            "###..............ege..........mm..........ege..............###",
            "###-------.......gmg.....,,,,,,,,,,,,.....gmg.......-------###",
            "###..............gmg.....,,,,,,,,,,,,.....gmg..............###",
            "###......c.......gmg.....,,,,,,,,,,,,.....gmg.......c......###",
            "###.....2##1.....gmg.....,,,,,,,,,,,,.....gmg.....2##1.....###",
            "###......##......gmg.....,,,,,,,,,,,,.....gmg......##......###",
            "###......##......gmg.....,,,,,,,,,,,,.....gmg......##......###",
            "###----..##......eee.....,,,,,,,,,,,,.....eee......##..----###",
            "###......##..............,,,,,A,,,,,,..............##......###",
            "DDD......##..............,,,,,,,,,,,,..............##......DDD",
            "DDD......##..............,,,,,,,,,,,,..............##......DDD",
            "DDD......##..............,c2######1c,..............##......DDD",
            "DDD.....2##1.............2##########1.............2##1.....DDD",
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
        private const ushort WallGlass = WallID.RedStainedGlass;
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
            //锈渍种子：链底与吊灯下沿，金属件在墙上淌出的做旧垂痕（L2 签名，纯墙漆零碰撞）
            var rustSeeds = new List<(int x, int y)>();
            var chainBottom = new Dictionary<int, int>();

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
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                            //斜切收边砖，字符即 SlopeType 枚举值（图例注释有对照表）
                            SetSloped(x, y, Brick, WallSlab, (SlopeType)(ch - '0'));
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
                        case 'o':
                            //玫瑰窗芯：未上漆红彩玻，读作最烫的一点
                            ClearCell(x, y, WallGlass);
                            break;
                        case 'g':
                            //花瓣格：红彩玻上粉漆，压进囚粉主色
                            ClearCell(x, y, WallGlass, PaintID.PinkPaint);
                            break;
                        case 'e':
                            //窗缘格：深粉漆压暗，圈出窗形
                            ClearCell(x, y, WallGlass, PaintID.DeepPinkPaint);
                            break;
                        case 'm':
                            //铅条/窗棂：板岩灰漆，尖拱窗中柱读作牢窗铁栏
                            ClearCell(x, y, WallSlab, PaintID.GrayPaint);
                            break;
                        case '-':
                            SetPlatform(x, y, lastWall);
                            break;
                        case 'h':
                            SetChain(x, y, lastWall);
                            chainBottom[x] = y;
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
                            if (ch == 'L') {
                                rustSeeds.Add((x, y + 2));
                            }
                            break;
                        default:
                            CWRMod.Instance.Logger.Warn(
                                $"[GaolBossRoom] 未知图例字符 '{ch}' at ({rx},{ry})");
                            break;
                    }
                }
            }
            foreach (KeyValuePair<int, int> kv in chainBottom) {
                rustSeeds.Add((kv.Key, kv.Value + 1));
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

            //做旧遍：锈渍垂痕（坐标哈希定长，零 genRand，只动墙漆不动碰撞）
            foreach ((int sx, int sy) in rustSeeds) {
                PaintRustStreak(sx, sy);
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
            tile.TileColor = 0;
            tile.WallType = wall;
            tile.WallColor = 0;
            tile.LiquidAmount = 0;
        }

        private static void SetSloped(int x, int y, ushort type, ushort wall, SlopeType slope) {
            SetSolid(x, y, type, wall);
            Tile tile = Main.tile[x, y];
            tile.Slope = slope;
        }

        private static void ClearCell(int x, int y, ushort wall, byte wallPaint = 0) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.WallType = wall;
            tile.WallColor = wallPaint;
            tile.LiquidAmount = 0;
        }

        private static void SetPlatform(int x, int y, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Platforms;
            tile.TileFrameY = PinkPlatformFrameY;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.TileColor = 0;
            tile.WallType = wall;
            tile.WallColor = 0;
            tile.LiquidAmount = 0;
        }

        private static void SetChain(int x, int y, ushort wall) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.Chain;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.TileColor = 0;
            tile.WallType = wall;
            tile.WallColor = 0;
            tile.LiquidAmount = 0;
        }

        /// <summary>
        /// 锈渍垂痕：自种子格向下 3~6 格给裸墙上棕漆（长度取坐标哈希，零 genRand）。
        /// 只染本 prefab 的三种狱墙，撞到彩玻/铅条/实心砖即停，垂痕不会淌进窗里。
        /// </summary>
        private static void PaintRustStreak(int x, int y) {
            int len = 3 + (Hash(x, y) & 3);
            for (int dy = 0; dy < len; dy++) {
                Tile tile = Main.tile[x, y + dy];
                if (tile.HasTile && tile.TileType == Brick) {
                    break;
                }
                if (tile.WallType is not (WallBase or WallSlab or WallTile)) {
                    break;
                }
                if (tile.WallColor != 0) {
                    break;
                }
                tile.WallColor = PaintID.BrownPaint;
            }
        }

        /// <summary>坐标散列（对齐 LayerTint 的确定性做旧思路：装饰随机不进 genRand 账本）</summary>
        private static int Hash(int x, int y) {
            unchecked {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & int.MaxValue;
            }
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
