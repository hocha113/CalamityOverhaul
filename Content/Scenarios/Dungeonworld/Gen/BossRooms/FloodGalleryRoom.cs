using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using System;
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
    /// 做旧与杂物全走确定性块散列（零 genRand）：生成管线与测试钥匙两条路出同一间房，
    /// 且不动 P30 之后的随机流（R4）。做旧签名遵守 ROOMS-L4（双水线+苔藓），
    /// 锁链只取"水下横躺"限定形态，杂物全贴边角，开阔战斗区保持零家具。
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
        //| 空+瓷面墙+灰漆(立管)  = 空+瓷面墙+深蓝漆(水位刻度线)
        //G 原版排水格栅(tile 546,实心且透液:战利品搁得住、泄洪漏得下;死亡演出刷棕漆锈裂)
        //T 绿砖平台+灰漆(王座台座:平台不挡碰撞,蛰伏体锚点 ThroneWorldPos 语义不动)
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
            //门插槽底行 + 阀台（11..13 一格高台阶）+ 王座台座（65..70 平台，龛底抬高一格）
            string dais = "DDD" + new string('.', 8) + "###" + "..." + new string('#', 6)
                + new string('.', 28) + new string('#', 6) + new string('.', 8) + new string('T', 6) + "DDD";
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

        //做旧签名的块散列盐（LayerTint.BlockPatch，零 genRand；异盐防各签名同相/与层染同相）
        private const int SaltTiled = 0x77A3;
        private const int SaltSlab = 0x4F1D;
        private const int SaltMoss = 0x2B67;
        private const int SaltLine = 0x69E5;

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
                            //排水格栅：原版 Grate(tile 546)，实心且透液——战利品搁在栅面上,
                            //死亡演出的整槽泄洪从栅缝漏干（L4Palette.Grate 同料,层内语汇一致）。
                            //生成期不上漆（新装的铁色）,锈是死亡演出刷的棕漆（PaintGrateCracked）
                            SetSolid(x, y, L4Palette.Grate, WallSlab);
                            break;
                        case 'T':
                            //王座台座：龛底抬一格的平台座（不挡碰撞,蛰伏体锚点语义不动）,
                            //灰漆=常年水汽结的水垢,让台座从绿砖里读出"石座"一层
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

            //做旧遍（全 wall/paint 层,零碰撞几何改动,零 genRand）：先做旧再装修,
            //水线泡痕先落墙,链/罐/牌盖在痕迹前面,层次天然正确
            ApplyWeathering(originX, originY);

            //装修遍：告示/罐/沉链/吊灯（PlaceObject 系,拒绝记日志不硬失败）
            PlaceFurnishings(originX, originY);

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

        //==================== 做旧签名（ROOMS-L4:双水线+苔藓;确定性块散列,生成/钥匙两路同相）====================

        /// <summary>
        /// 泡旧分带 + 三道水线痕 + 立管管箍 + 苔藓深度梯度。
        /// 全走 wall/paint 层（STRUCTURES §3.2-6），碰撞几何零改动；
        /// 只动未上漆格，立管灰/刻度深蓝/龛背板岩等语义面不会被冲掉。
        /// </summary>
        private static void ApplyWeathering(int originX, int originY) {
            //①泡旧分带:开区基础绿墙按"被水泡过多深"换变体(瓷面成片切入+板岩按深度加密)。
            //  手法同源 L4Palette.BandWalls,但那边吃 genRand,本房为两路决定论改走块散列
            for (int ry = InteriorTop; ry < FloorRel; ry++) {
                int slabCoverage = ry >= Scale1SurfaceRel ? 60 : ry >= Scale2SurfaceRel ? 35 : 12;
                for (int rx = InteriorLeft; rx <= InteriorRight; rx++) {
                    int x = originX + rx, y = originY + ry;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (t.HasTile || t.WallType != WallBase || t.WallColor != PaintID.None) {
                        continue;
                    }
                    if (LayerTint.BlockPatch(x, y, 10, SaltTiled)) {
                        t.WallType = WallTile;
                    }
                    else if (LayerTint.BlockPatch(x, y, slabCoverage, SaltSlab)) {
                        t.WallType = WallSlab;
                    }
                }
            }

            //②三道水线痕(L4 双水线语义:灰=满水痕,黑=常驻低水痕;断续=年久斑驳):
            //  两道刻度下沿各一道灰痕("仪式水真到过这里"),踝水行一道黑痕(日常泡着的记录)
            StainRow(originX, originY, Scale1SurfaceRel + 1, L4Palette.HighLinePaint, 55);
            StainRow(originX, originY, Scale2SurfaceRel + 1, L4Palette.HighLinePaint, 45);
            StainRow(originX, originY, AnkleSurfaceRel, L4Palette.LowLinePaint, 70);

            //③立管管箍:灰管每6行一节黑箍(法兰),纵带升格成"分节的铅管"。
            //  只覆写立管自己的灰漆格,刻度行(深蓝)与实心格天然跳过
            foreach (int pipeCol in new[] { PipeLeftCol, PipeRightCol }) {
                for (int ry = PipeTopRel + 3; ry <= PipeBottomRel; ry += 6) {
                    for (int dx = 0; dx < 2; dx++) {
                        int x = originX + pipeCol + dx, y = originY + ry;
                        Tile t = Main.tile[x, y];
                        if (!t.HasTile && t.WallColor == PaintID.GrayPaint) {
                            WorldGen.paintWall(x, y, PaintID.BlackPaint);
                        }
                    }
                }
            }

            //④苔藓:深绿漆点染可见砖面,越近水越密(踝水带最密),立管口下方水汽养苔加密。
            //  块散列出成片苔斑而非椒盐(L4Scatter.MossDaubs 的房内定制版)
            for (int ry = InteriorTop; ry <= FloorRel; ry++) {
                int coverage = ry >= 38 ? 42 : ry >= Scale1SurfaceRel ? 26 : ry >= Scale2SurfaceRel ? 12 : 4;
                for (int rx = 2; rx <= Width - 3; rx++) {
                    int x = originX + rx, y = originY + ry;
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (!t.HasTile || t.TileType != Brick || t.TileColor != PaintID.None
                        || !HasAirNeighbor(x, y)) {
                        continue;
                    }
                    int c = NearPipeMouth(rx, ry) ? coverage + 26 : coverage;
                    if (LayerTint.BlockPatch(x, y, c, SaltMoss)) {
                        WorldGen.paintTile(x, y, L4Palette.MossPaint);
                    }
                }
            }
        }

        /// <summary>断续水线痕:沿 rel 行给未上漆墙面刷漆,块散列断点=斑驳不满涂</summary>
        private static void StainRow(int originX, int originY, int rel, byte paint, int coverage) {
            int y = originY + rel;
            for (int rx = InteriorLeft; rx <= InteriorRight; rx++) {
                int x = originX + rx;
                if (!WorldGen.InWorld(x, y, 5)) {
                    continue;
                }
                Tile t = Main.tile[x, y];
                if (t.HasTile || t.WallType == 0 || t.WallColor != PaintID.None) {
                    continue;
                }
                if (LayerTint.BlockPatch(x, y, coverage, SaltLine)) {
                    WorldGen.paintWall(x, y, paint);
                }
            }
        }

        private static bool HasAirNeighbor(int x, int y)
            => !Main.tile[x - 1, y].HasTile || !Main.tile[x + 1, y].HasTile
            || !Main.tile[x, y - 1].HasTile || !Main.tile[x, y + 1].HasTile;

        /// <summary>立管口(PipeBottomRel)下方小矩形:管口滴水养出的密苔区</summary>
        private static bool NearPipeMouth(int rx, int ry) {
            if (ry < PipeBottomRel || ry > PipeBottomRel + 6) {
                return false;
            }
            return Math.Abs(rx - (PipeLeftCol + 1)) <= 3 || Math.Abs(rx - (PipeRightCol + 1)) <= 3;
        }

        //==================== 杂物与告示（定点装修,非撒布;开阔战斗区零家具,全贴边角/壁架端头）====================

        //阀台引导(先说做什么会发生什么,再指认房内可见物;巡水员口吻,玩家可见中文无破折号)
        private const string ValveSignText =
            "巡水员留字:别上阀台。人在台上站稳,闸就当你是来放水的。水一涨只认墙上的蓝刻度,不认人。";

        private static void PlaceFurnishings(int originX, int originY) {
            int placed = 0, rejected = 0;
            void Tally(bool ok, string what, int x, int y) {
                if (ok) {
                    placed++;
                }
                else {
                    rejected++;
                    CWRMod.Instance.Logger.Warn($"[FloodGalleryRoom] {what}放置失败 at ({x},{y})");
                }
            }

            int floorStand = originY + FloorRel - 1;
            //告示牌:阀台左邻,进门平走第一眼(门插槽底沿即地板)
            Tally(L4Palette.PlaceSignWithText(originX + 8, floorStand, ValveSignText),
                "阀台告示", originX + 8, floorStand);

            //罐:失物读法,全贴边角(左角地面一只,双壁架端头各一只;开阔走位区不放)
            Tally(WorldGen.PlacePot(originX + 4, floorStand, TileID.Pots, PotStyleAt(originX + 4)),
                "左角罐", originX + 4, floorStand);
            Tally(WorldGen.PlacePot(originX + 9, originY + 27, TileID.Pots, PotStyleAt(originX + 9)),
                "左壁架罐", originX + 9, originY + 27);
            Tally(WorldGen.PlacePot(originX + 64, originY + 27, TileID.Pots, PotStyleAt(originX + 64)),
                "右壁架罐", originX + 64, originY + 27);

            //沉链三段:横躺贴地(INDEX §3 的 L4 锁链唯一许可形态)。铺干版见 LayChainDry 注释
            int chainRow = originY + FloorRel - 1;
            LayChainDry(originX + 24, chainRow, 6);
            LayChainDry(originX + 44, chainRow, 5);
            LayChainDry(originX + 58, chainRow, 4);

            //吊灯:拱带下四盏油布壁灯(L4 灯纪律"干道标/水下零":全挂刻度二之上,P3 深水也淹不到)
            foreach (int rx in new[] { 9, 27, 46, 64 }) {
                Tally(L4Palette.TryPlaceObject(originX + rx, originY + InteriorTop,
                    TileID.HangingLanterns, L4Palette.LanternSconceStyle),
                    "拱带吊灯", originX + rx, originY + InteriorTop);
            }

            CWRMod.Instance.Logger.Info($"[FloodGalleryRoom] 装修完毕 落{placed}拒{rejected}");
        }

        /// <summary>罐样式:确定性坐标散列(零genRand,两路同相),样式域沿 L4 地牢罐 10~12</summary>
        private static int PotStyleAt(int x)
            => L4Palette.PotStyleMin + (x * 7 + 3) % (L4Palette.PotStyleMax - L4Palette.PotStyleMin);

        /// <summary>
        /// 干铺沉链:镜像 L4Palette.LaySunkenChain(横向、贴地、遇占即停),但不查液体。
        /// 本房生成期零液体是设计口径(水全归看守运行期写),踝水 arm 后链即为水下沉链,
        /// INDEX §3 的形态裁决(横躺贴地)在运行态实质成立。
        /// </summary>
        private static int LayChainDry(int x, int y, int length) {
            int placedLinks = 0;
            for (int i = 0; i < length; i++) {
                if (!WorldGen.InWorld(x + i, y, 5)) {
                    break;
                }
                Tile tile = Main.tile[x + i, y];
                if (tile.HasTile) {
                    break;
                }
                tile.HasTile = true;
                tile.TileType = TileID.Chain;
                tile.Slope = SlopeType.Solid;
                tile.IsHalfBlock = false;
                placedLinks++;
            }
            return placedLinks;
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
