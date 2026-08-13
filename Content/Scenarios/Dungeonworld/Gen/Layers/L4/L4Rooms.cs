using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //====================================================================
    //L4房型构建器(ROOMS-L4 §1花名册;井站/忏悔室=跨层公共构件,归公共构件波,本层不做)
    //
    //湿房统一几何契约(堰坎公理的工程化):
    //  * 每簇一条共享水线行 waterline;湿房水面一律锁在该行(满水态);
    //  * 湿房接驳口=「port」:通行洞开在[waterline-4,waterline)四行,
    //    洞底坎(=堰坎顶/沉槛)在waterline行保持实心——水面恰与坎顶齐平,
    //    settle后必然静定(§2.4-④"水面=堰坎顶-0");
    //  * 深湿房(沉没囚室/蓄水厅/深潜井)FloorTop下潜,水面仍锁共享水线;
    //  * 排水态通行:排空后房内爬升全部≤4格/跳(F2)或有平台梯,两态无死区。
    //写入纪律:几何走TileBrush,家具走原版放置函数校验版,拒绝记日志(§3.2-1)
    //====================================================================
    internal static class L4Rooms
    {
        internal struct Tally
        {
            internal int Placed;
            internal int Rejected;

            internal void Add(bool ok, string what, int x, int y) {
                if (ok) {
                    Placed++;
                }
                else {
                    Rejected++;
                    CWRMod.Instance.Logger.Warn($"[L4Rooms] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //整包络重盖绿砖+开内膛(M0蓝底换皮,镜像L2成规),所有房型共用第一遍
        internal static void StampAndCarve(RoomNode room, ushort wall) {
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, wall);
        }

        //==================== 半淹管廊(#1,本层主体) ====================
        //剖面(F=FloorTop,waterline=F-4):
        //  内膛顶F-10 ── 壁灯行(1x2灯体与走道人身错开)
        //  F-5 平台走道(水面上1行,净空5;两端搭上堰坎顶自动登1格,F3)
        //  F-4 水面行(=堰坎顶;两端堰坎2宽4高构造性锁水位)
        //  F-1..F-4 水体(4深,可潜;水面在走道正下,冒头即呼吸)
        //排水态:下层地板开放,4高堰坎一跳可越(F2满跳6.6)——通行权互换

        internal static Point GalleryInteriorSize(UnifiedRandom rand)
            => new(rand.Next(20, 35), 10);

        /// <summary>drained=true为干涸舱段(L4→L5过渡预告:不注水+龟裂棕漆点)</summary>
        internal static Tally BuildGallery(RoomNode room, int waterline, UnifiedRandom rand,
            bool drained = false, bool sunkenChest = false) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L4Palette.WallBase);

            //两端堰坎:2宽,自地板立到水线行(含)。必须用整砖——裂纹绿(482)踩踏即碎,
            //承重/坎顶一律禁用(裂砖只准当注水坑假地板;D表"堰坎侧面裂纹绿"以此工程裁决收窄)
            for (int dx = 0; dx < 2; dx++) {
                for (int y = waterline; y < floor; y++) {
                    TileBrush.SetSolid(room.InteriorLeft + dx, y, L4Palette.Brick);
                    TileBrush.SetSolid(room.InteriorRight - 1 - dx, y, L4Palette.Brick);
                }
            }
            int wetL = room.InteriorLeft + 2;
            int wetR = room.InteriorRight - 2;

            //干走道:水面上1行整跨平台(玩家按下可潜入,天然"潜水口盖平台"语义)
            TileBrush.PlatformRow(wetL, wetR, waterline - 1, L4Palette.PlatformFrameY);

            //舱段登记:满水=水线,排水=排空(干涸舱段不登记,永远无水)
            L4WaterWorks.Compartment compartment = null;
            if (!drained) {
                compartment = L4WaterWorks.Register($"管廊{room.Bounds.Left}",
                    new Rectangle(wetL, waterline, wetR - wetL, floor - waterline),
                    waterline, floor);
            }

            //气龛(潜水钟):水下通道每20~30格一座(ROOMS-L4 §1);顶盖+外侧吊柱,气袋2x2只开底口
            if (compartment != null) {
                for (int bx = wetL + 10; bx < wetR - 8; bx += rand.Next(20, 31)) {
                    TileBrush.SetSolid(bx, waterline + 1, L4Palette.Brick);
                    TileBrush.SetSolid(bx + 1, waterline + 1, L4Palette.Brick);
                    TileBrush.SetSolid(bx + 2, waterline + 1, L4Palette.Brick);
                    TileBrush.SetSolid(bx + 2, waterline + 2, L4Palette.Brick);
                    TileBrush.SetSolid(bx + 2, waterline + 3, L4Palette.Brick);
                    compartment.AirPockets.Add(new Rectangle(bx, waterline + 2, 2, 2));
                }
            }

            //油布壁灯:走道顶每8~12列一盏(干道"标"档,ROOMS-INDEX §7;水下零)
            for (int x = wetL + 3; x < wetR - 2; x += rand.Next(8, 13)) {
                tally.Add(L4Palette.TryPlaceObject(x, room.InteriorTop, TileID.HangingLanterns,
                    L4Palette.LanternSconceStyle), "油布壁灯", x, room.InteriorTop);
            }

            //水下杂物:罐(水下罐=冲进下水道的失物);沉链由撒布统一铺(P55)
            int potX = rand.Next(wetL + 2, wetR - 2);
            tally.Add(WorldGen.PlacePot(potX, floor - 1, TileID.Pots,
                rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax)), "水下罐", potX, floor - 1);

            //两态战利品钩子:约1/3管廊藏排水态才好拿的沉箱(M4填表,先落容器)
            if (sunkenChest) {
                int chestX = rand.Next(wetL + 2, wetR - 4);
                tally.Add(WorldGen.PlaceChest(chestX, floor - 1, TileID.Containers,
                    notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0,
                    "沉箱", chestX, floor - 1);
            }

            if (drained) {
                //干涸签名:龟裂棕漆点撒在地板砖面(paint层,§3.2-6)
                for (int i = 0; i < (wetR - wetL) / 3; i++) {
                    int px = rand.Next(wetL, wetR);
                    WorldGen.paintTile(px, floor, L4Palette.DryCrackPaint);
                }
            }
            return tally;
        }

        //==================== 沉没囚室(#4,水牢点题房) ====================
        //剖面(F=FloorTop=湿排地板+6,waterline=F-10):水深10;
        //囚隔墙4高+格栅帽(排水态一跳可越),隔墙顶上是泳道+水面;水面上4行空气带;
        //无门板:通行=贴水面泳道游过隔墙(INDEX §3与L2差异化裁决);
        //端壁之字平台(竖距4)保证排水态能爬回port;
        //两态战利品:满水=空气带尽端高龛箱(浮力上顶),排水=囚格底沉箱

        internal static Point SunkenCellInteriorSize(UnifiedRandom rand)
            => new(rand.Next(26, 35), 14);

        internal static Tally BuildSunkenCells(RoomNode room, int waterline, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L4Palette.WallSlab);   //整体泡在水下,Slab基调

            int wetL = room.InteriorLeft;
            int wetR = room.InteriorRight;

            //囚隔墙:2宽4高,间距6~8,分出"囚格";隔墙顶即栅位,盖格栅块(透视觉不透人)
            int px2 = wetL + rand.Next(5, 7);
            var cellFloors = new System.Collections.Generic.List<int>();
            int prevWall = wetL;
            while (px2 + 2 < wetR - 5) {
                for (int y = floor - 4; y < floor; y++) {
                    TileBrush.SetSolid(px2, y, L4Palette.Brick);
                    TileBrush.SetSolid(px2 + 1, y, L4Palette.Brick);
                }
                TileBrush.SetSolid(px2, floor - 5, L4Palette.Grate);
                TileBrush.SetSolid(px2 + 1, floor - 5, L4Palette.Grate);
                cellFloors.Add((prevWall + px2) / 2);
                prevWall = px2 + 2;
                px2 += rand.Next(8, 11);
            }
            cellFloors.Add((prevWall + wetR) / 2);

            //端壁之字平台:排水态爬升梯(竖距4,F2),满水态沉在水里无碍
            for (int step = 1; step <= 2; step++) {
                int y = floor - step * 4;
                TileBrush.PlatformRow(wetL, wetL + 3, y, L4Palette.PlatformFrameY);
                TileBrush.PlatformRow(wetR - 3, wetR, y, L4Palette.PlatformFrameY);
            }

            //满水态高龛:空气带里的实心龛台+沉箱(排水态11格高够不着,浮力上顶专属)
            int ledgeX = rand.NextBool(2) ? wetL + 1 : wetR - 3;
            int signX = ledgeX < (wetL + wetR) / 2 ? wetR - 4 : wetL + 2;
            TileBrush.SetSolid(signX, waterline - 1, L4Palette.Brick);
            TileBrush.SetSolid(signX + 1, waterline - 1, L4Palette.Brick);
            tally.Add(L4Palette.PlaceSignWithText(signX, waterline - 2, L4Palette.SunkenCellSignText),
                "沉没告示", signX, waterline - 2);
            TileBrush.SetSolid(ledgeX, waterline - 1, L4Palette.Brick);
            TileBrush.SetSolid(ledgeX + 1, waterline - 1, L4Palette.Brick);
            tally.Add(WorldGen.PlaceChest(ledgeX, waterline - 2, TileID.Containers,
                notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0,
                "高龛箱", ledgeX, waterline - 2);

            //排水态沉箱:中间某囚格底(两态战利品对儿的另一半)
            int sunkX = cellFloors[cellFloors.Count / 2];
            tally.Add(WorldGen.PlaceChest(sunkX, floor - 1, TileID.Containers,
                notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0,
                "囚格沉箱", sunkX, floor - 1);
            //囚格水下杂物:罐(沉链由撒布铺)
            foreach (int cx in cellFloors) {
                if (cx != sunkX && rand.NextBool(2)) {
                    tally.Add(WorldGen.PlacePot(cx, floor - 1, TileID.Pots,
                        rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax)), "囚格罐", cx, floor - 1);
                }
            }

            //舱段登记:满水=共享水线(深10),排水=排空
            L4WaterWorks.Register($"沉没囚室{room.Bounds.Left}",
                new Rectangle(wetL, waterline, wetR - wetL, floor - waterline),
                waterline, floor);
            return tally;
        }

        //==================== 蓄水大厅(#3,两态最戏剧化的大房) ====================
        //F=FloorTop=湿排地板+16,waterline=F-20:水深20;柱阵12高沉在水里;
        //空气带5行:高位环廊平台(水面上1行)沿两侧壁;端壁之字平台通厅底(排水态爬升);
        //满水=环廊通行+柱林潜泳,排水=厅底开放露沉箱沉链(ROOMS-L4 §1两态表)

        internal static Point ReservoirInteriorSize(UnifiedRandom rand)
            => new(rand.Next(44, 57), 26);

        internal static Tally BuildReservoir(RoomNode room, int waterline, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L4Palette.WallSlab);

            int wetL = room.InteriorLeft;
            int wetR = room.InteriorRight;

            //柱阵:2宽12高,柱头外扩1格(檐口语法破呆板,§2.4-⑤同构)
            for (int cx = wetL + rand.Next(6, 9); cx + 2 < wetR - 6; cx += rand.Next(10, 15)) {
                for (int y = floor - 12; y < floor; y++) {
                    TileBrush.SetSolid(cx, y, L4Palette.Brick);
                    TileBrush.SetSolid(cx + 1, y, L4Palette.Brick);
                }
                TileBrush.SetSolid(cx - 1, floor - 12, L4Palette.Brick);
                TileBrush.SetSolid(cx + 2, floor - 12, L4Palette.Brick);
            }

            //高位环廊:水面上1行的平台带,自两端port延伸(中段留空可跳水)
            int ringY = waterline - 1;
            int ringLen = (wetR - wetL) / 3;
            TileBrush.PlatformRow(wetL, wetL + ringLen, ringY, L4Palette.PlatformFrameY);
            TileBrush.PlatformRow(wetR - ringLen, wetR, ringY, L4Palette.PlatformFrameY);

            //端壁之字平台:水线→厅底(竖距4,交错两端),排水态的下行/回升梯
            int step2 = 0;
            for (int y = waterline + 3; y < floor - 1; y += 4) {
                bool left = step2++ % 2 == 0;
                if (left) {
                    TileBrush.PlatformRow(wetL, wetL + 3, y, L4Palette.PlatformFrameY);
                }
                else {
                    TileBrush.PlatformRow(wetR - 3, wetR, y, L4Palette.PlatformFrameY);
                }
            }

            //厅底两态战利品:沉箱+杂物罐(沉链由撒布补)
            int chestX = (wetL + wetR) / 2 + rand.Next(-6, 7);
            tally.Add(WorldGen.PlaceChest(chestX, floor - 1, TileID.Containers,
                notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0, "厅底沉箱", chestX, floor - 1);
            for (int i = 0; i < 3; i++) {
                int potX = rand.Next(wetL + 2, wetR - 2);
                tally.Add(WorldGen.PlacePot(potX, floor - 1, TileID.Pots,
                    rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax)), "厅底罐", potX, floor - 1);
            }
            //空气带照明:壁灯沿内膛顶
            for (int x = wetL + 4; x < wetR - 3; x += rand.Next(10, 14)) {
                tally.Add(L4Palette.TryPlaceObject(x, room.InteriorTop, TileID.HangingLanterns,
                    L4Palette.LanternSconceStyle), "油布壁灯", x, room.InteriorTop);
            }

            L4WaterWorks.Register($"蓄水厅{room.Bounds.Left}",
                new Rectangle(wetL, waterline, wetR - wetL, floor - waterline),
                waterline, floor);
            return tally;
        }

        //==================== 深潜井(#5变体裁决:房内自包含深水井,垂直泳感承担者) ====================
        //上部头舱(净高4,port接湿排),头舱地板中央开井口盖平台(防误落,§2.1);
        //井体5宽21深:满水=垂直泳道+井底沉箱(呼吸压力=主危险),
        //气龛(潜水钟,ROOMS-L4 §1"水下通道每20~30格设气龛"的井内版)每~8深一座;
        //排水态=井壁交错平台攀降,井底剩4深残水池。
        //(全权裁决记档:跨房水柱不可密封→"排水井满水泳道"收敛为本房型,见L4WaterWorks头注)

        internal static Point PlungeWellInteriorSize() => new(9, 28);

        internal static Tally BuildPlungeWell(RoomNode room, int waterline, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            int headFloor = waterline;             //头舱地板行=共享水线行(port沉槛同高)
            StampAndCarve(room, L4Palette.WallSlab);

            //头舱地板:实心一层,中央3宽井口盖平台
            int wellL = room.InteriorLeft + 2;     //井体5宽居中(内膛9宽)
            int wellR = wellL + 5;
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                TileBrush.SetSolid(x, headFloor, L4Palette.Brick);
            }
            int mouthL = wellL + 1;
            TileBrush.CarveRect(mouthL, headFloor, mouthL + 3, headFloor + 1, L4Palette.WallSlab);
            TileBrush.PlatformRow(mouthL, mouthL + 3, headFloor, L4Palette.PlatformFrameY);

            //井体:头舱地板下两侧回填成井壁,只留5宽井膛
            for (int y = headFloor + 1; y < floor; y++) {
                for (int x = room.InteriorLeft; x < wellL; x++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
                for (int x = wellR; x < room.InteriorRight; x++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
            }

            //井内攀降平台:竖距4交错(排水态可上下;满水态沉水无碍)
            int side = 0;
            for (int y = floor - 4; y > headFloor + 2; y -= 4) {
                if (side++ % 2 == 0) {
                    TileBrush.PlatformRow(wellL, wellL + 2, y, L4Palette.PlatformFrameY);
                }
                else {
                    TileBrush.PlatformRow(wellR - 2, wellR, y, L4Palette.PlatformFrameY);
                }
            }

            //气龛(潜水钟):贴左井壁的倒扣斗——顶盖3宽+外侧吊柱2高,气袋2x2只开底口;
            //水从下方顶不进来(液体无压力上溯),settle静定;满水态是换气点,排水态读作壁架
            var compartment = L4WaterWorks.Register($"深潜井{room.Bounds.Left}",
                new Rectangle(wellL, headFloor + 3, wellR - wellL, floor - headFloor - 3),
                headFloor + 3, floor - 4);
            int bells = 0;
            for (int y = headFloor + 10; y < floor - 8 && bells < 2; y += 8, bells++) {
                TileBrush.SetSolid(wellL, y - 1, L4Palette.Brick);
                TileBrush.SetSolid(wellL + 1, y - 1, L4Palette.Brick);
                TileBrush.SetSolid(wellL + 2, y - 1, L4Palette.Brick);
                TileBrush.SetSolid(wellL + 2, y, L4Palette.Brick);
                TileBrush.SetSolid(wellL + 2, y + 1, L4Palette.Brick);
                compartment.AirPockets.Add(new Rectangle(wellL, y, 2, 2));
            }

            //井底沉箱(满水态深潜奖励/排水态走楼梯白捡——低水位残水4深仍盖着它,潜一小口气)
            tally.Add(WorldGen.PlaceChest(wellL + 1, floor - 1, TileID.Containers,
                notNearOtherChests: false, L4Palette.ChestWaterStyle) >= 0, "井底沉箱", wellL + 1, floor - 1);

            //头舱照明+警戒告示
            tally.Add(L4Palette.TryPlaceObject(mouthL + 1, room.InteriorTop, TileID.HangingLanterns,
                L4Palette.LanternSconceStyle), "油布壁灯", mouthL + 1, room.InteriorTop);
            return tally;
        }

        //==================== 阀室(#2,一杆一室;两态机的拉杆钩子占位) ====================

        internal static Point ValveRoomInteriorSize(UnifiedRandom rand)
            => new(rand.Next(10, 14), rand.Next(5, 7));

        /// <param name="forcedSign">非空则覆盖轮换文案池(最底组L4→L5预告用)</param>
        internal static Tally BuildValveRoom(RoomNode room, UnifiedRandom rand, string forcedSign = null) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L4Palette.WallBase);

            int mid = (room.InteriorLeft + room.InteriorRight) / 2;
            //拉杆:全局水位控制的钩子占位(运行时TP接线归资产波,L4WaterWorks.ApplyState即其机制函数)
            tally.Add(L4Palette.TryPlaceLever(mid, floor - 1), "水位拉杆", mid, floor - 1);
            //阀台:工作台+桌面蜡烛(检修台读法)
            tally.Add(L4Palette.TryPlaceTile(mid - 3, floor - 1, TileID.WorkBenches,
                L4Palette.WorkBenchStyle), "检修台", mid - 3, floor - 1);
            tally.Add(L4Palette.TryPlaceTile(mid - 3, floor - 2, TileID.Candles,
                L4Palette.CandleStyle), "蜡烛", mid - 3, floor - 2);
            //水位告示(轮换文案池)+落地灯
            string text = forcedSign ?? L4Palette.ValveSignTexts[rand.Next(L4Palette.ValveSignTexts.Length)];
            tally.Add(L4Palette.PlaceSignWithText(room.InteriorLeft + 1, floor - 1, text),
                "水位告示", room.InteriorLeft + 1, floor - 1);
            tally.Add(L4Palette.TryPlaceTile(room.InteriorRight - 2, floor - 1, TileID.Lamps,
                L4Palette.LampStyle), "落地灯", room.InteriorRight - 2, floor - 1);
            return tally;
        }

        //==================== 主/次泵房(#6,水位系统实体锚) ====================
        //泵机TP=自制资产【必需,资产波】;本波交机器湾(留位)+锚点数据:
        //Slab背景湾+格栅地槽+灰漆管线走顶角(管件贴片的paint保守解,INDEX §7"可见管线走wall/paint")

        internal static Point PumpHouseInteriorSize(UnifiedRandom rand, bool main)
            => main ? new Point(rand.Next(26, 31), 13) : new Point(rand.Next(16, 21), 9);

        internal static Tally BuildPumpHouse(RoomNode room, UnifiedRandom rand, bool main) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L4Palette.WallBase);

            int bayL = room.InteriorLeft + 2;
            int bayW = main ? 9 : 6;
            int bayH = main ? 7 : 5;
            //机器湾:Slab背景+抬高1格的格栅地槽(泵机TP落位区,直写帧+AddInWorld归资产波)
            for (int x = bayL; x < bayL + bayW; x++) {
                for (int y = floor - bayH; y < floor; y++) {
                    Tile bay = Main.tile[x, y];
                    if (!bay.HasTile) {
                        bay.WallType = L4Palette.WallSlab;
                    }
                }
                TileBrush.SetSolid(x, floor - 1, L4Palette.Grate);
            }
            if (main) {
                //主泵房=全层水位系统实体锚(STRUCTURES §4.1 WaterLevelController挂点)
                L4WaterWorks.PumpMachineAnchor = new Point(bayL + bayW / 2, floor - 2);
            }
            //灰漆管线:沿机器湾顶角走横线(资产波换铅管ModWall贴片)
            for (int x = bayL - 1; x < room.InteriorRight - 1; x++) {
                if (!Main.tile[x, floor - bayH].HasTile
                    && Main.tile[x, floor - bayH].WallType != WallID.None) {
                    WorldGen.paintWall(x, floor - bayH, L4Palette.HighLinePaint);
                }
            }

            //湾侧拉杆(泵启闭意象)+检修桌椅
            int deskX = bayL + bayW + 3;
            tally.Add(L4Palette.TryPlaceLever(bayL + bayW + 1, floor - 2), "泵闸拉杆", bayL + bayW + 1, floor - 2);
            if (deskX + 4 < room.InteriorRight) {
                tally.Add(L4Palette.TryPlaceTile(deskX + 2, floor - 1, TileID.Tables,
                    L4Palette.TableStyle), "检修桌", deskX + 2, floor - 1);
                tally.Add(L4Palette.TryPlaceTile(deskX + 4, floor - 1, TileID.Chairs,
                    L4Palette.ChairStyle), "检修椅", deskX + 4, floor - 1);
            }
            //交接簿铭牌(七代呼应,ROOMS-L4 §3彩蛋钩子)+烛台+壁灯
            if (main) {
                tally.Add(L4Palette.PlaceSignWithText(room.InteriorRight - 2, floor - 1,
                    L4Palette.PumpLogSignText), "交接簿", room.InteriorRight - 2, floor - 1);
            }
            tally.Add(L4Palette.TryPlaceTile(deskX, floor - 1, TileID.Candelabras,
                L4Palette.CandelabraStyle), "烛台", deskX, floor - 1);
            tally.Add(L4Palette.TryPlaceObject(bayL + bayW / 2, room.InteriorTop, TileID.HangingLanterns,
                L4Palette.LanternSconceStyle), "油布壁灯", bayL + bayW / 2, room.InteriorTop);
            return tally;
        }

        //==================== 堰闸走廊(#7,干湿分界走廊) ====================
        //中段天花垂闸柱+1x5高闸门(388),两侧各一拉杆红线电驱
        //(Wiring.cs L1532:HitWire→ShiftTallGate,原版自带联机同步;
        //即时触发链不依赖UpdateMech,NormalUpdates=false下照常可用,F17)

        internal static Point GateCorridorInteriorSize(UnifiedRandom rand)
            => new(rand.Next(20, 25), 7);

        internal static Tally BuildGateCorridor(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L4Palette.WallBase);

            int gateX = (room.InteriorLeft + room.InteriorRight) / 2;
            //闸柱:天花垂下2格实心,与地板夹出1x5闸槽(388锚定:槽顶槽底实心,TileObjectData L2460)
            TileBrush.SetSolid(gateX, floor - 7, L4Palette.CrackedBrick);
            TileBrush.SetSolid(gateX, floor - 6, L4Palette.CrackedBrick);
            tally.Add(L4Palette.TryPlaceTallGate(gateX, floor - 5), "高闸门", gateX, floor - 5);

            //两侧拉杆:闸门两边都能开合,构造性防锁死(两态无死区纪律的门版)
            int leverL = room.InteriorLeft + 2;
            int leverR = room.InteriorRight - 3;
            tally.Add(L4Palette.TryPlaceLever(leverL, floor - 1), "闸左拉杆", leverL, floor - 1);
            tally.Add(L4Palette.TryPlaceLever(leverR, floor - 1), "闸右拉杆", leverR, floor - 1);

            //红线:沿内膛顶行走线(玩家无机械透镜不可见,INDEX §7),两杆各自连到闸槽
            //TML Tilemap索引器返回副本,必须先落到局部再写(镜像L3Lights.PaintRedWire)
            int wireY = floor - 6;
            for (int x = leverL; x <= leverR; x++) {
                Tile w = Main.tile[x, wireY];
                w.RedWire = true;
            }
            //竖引线:杆顶→顶线,闸槽→顶线
            for (int y = wireY; y <= floor - 1; y++) {
                Tile left = Main.tile[leverL, y];
                left.RedWire = true;
                Tile right = Main.tile[leverR, y];
                right.RedWire = true;
            }
            for (int y = floor - 5; y <= floor - 1; y++) {
                Tile g = Main.tile[gateX, y];
                g.RedWire = true;
            }

            //壁灯照闸
            tally.Add(L4Palette.TryPlaceObject(gateX - 4, room.InteriorTop, TileID.HangingLanterns,
                L4Palette.LanternSconceStyle), "油布壁灯", gateX - 4, room.InteriorTop);
            return tally;
        }

        //==================== 落水缓冲厅(#8,坠落房间A的落点;R2落点包络归管线路预留) ====================
        //F=FloorTop=干排地板+8:两端6宽干台肩与干排地板齐平(port在台肩上),
        //中央深水池6深两态恒满(接坠落防摔死,永不排空);池沿1格出水台阶

        internal const int SplashPoolDrop = 8;

        internal static Point SplashHallInteriorSize(UnifiedRandom rand)
            => new(rand.Next(30, 37), 12);

        internal static Tally BuildSplashHall(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            int ledgeFloor = floor - SplashPoolDrop;   //台肩站立面=干排地板行
            StampAndCarve(room, L4Palette.WallBase);

            //两端台肩:实心填到台面
            int shoulderW = 6;
            for (int x = room.InteriorLeft; x < room.InteriorLeft + shoulderW; x++) {
                for (int y = ledgeFloor; y < floor; y++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
            }
            for (int x = room.InteriorRight - shoulderW; x < room.InteriorRight; x++) {
                for (int y = ledgeFloor; y < floor; y++) {
                    TileBrush.SetSolid(x, y, L4Palette.Brick);
                }
            }
            //出水台阶:池沿1宽立柱,顶在水面上1格(池里一跳上台阶,再一步上台肩,F3)
            //整砖——可站立结构禁用裂纹绿(踩碎)
            int poolL = room.InteriorLeft + shoulderW;
            int poolR = room.InteriorRight - shoulderW;
            for (int y = floor - 7; y < floor; y++) {
                TileBrush.SetSolid(poolL, y, L4Palette.Brick);
                TileBrush.SetSolid(poolR - 1, y, L4Palette.Brick);
            }

            //深水池:6深,两态恒满(坠落缓冲是安全职责,不参与放排水)
            L4WaterWorks.Register($"落水池{room.Bounds.Left}",
                new Rectangle(poolL + 1, floor - 6, poolR - poolL - 2, 6),
                floor - 6, floor - 6);

            //告示+壁灯(池上方天花留空,坠落通道由管线路自上打通,R2)
            tally.Add(L4Palette.PlaceSignWithText(room.InteriorLeft + 2, ledgeFloor - 1,
                L4Palette.SplashSignText), "落水告示", room.InteriorLeft + 2, ledgeFloor - 1);
            tally.Add(L4Palette.TryPlaceObject(poolL + 2, room.InteriorTop, TileID.HangingLanterns,
                L4Palette.LanternSconceStyle), "油布壁灯", poolL + 2, room.InteriorTop);
            tally.Add(L4Palette.TryPlaceObject(poolR - 3, room.InteriorTop, TileID.HangingLanterns,
                L4Palette.LanternSconceStyle), "油布壁灯", poolR - 3, room.InteriorTop);
            return tally;
        }

        //==================== 接缝几何(层内自包含的port/link刻画,写入只走TileBrush) ====================

        /// <summary>
        /// 湿port:两房之间在[waterline-4,waterline)开4高通行洞,洞底坎(waterline行)保持实心=
        /// 沉槛,水面与坎顶齐平(§2.4-④)。跨段落砖统一换绿,消除M0蓝底接缝。
        /// </summary>
        internal static void CarveWetPort(int xFrom, int xTo, int waterline, ushort wall) {
            int left = System.Math.Min(xFrom, xTo);
            int right = System.Math.Max(xFrom, xTo);
            for (int x = left; x < right; x++) {
                //洞顶过梁与洞底沉槛重盖绿砖(接缝framing的材质区分,§2.5)
                TileBrush.SetSolid(x, waterline - 5, L4Palette.Brick);
                TileBrush.SetSolid(x, waterline, L4Palette.Brick);
            }
            TileBrush.CarveRect(left, waterline - 4, right, waterline, wall);
        }

        /// <summary>
        /// 干link:两房地板齐平的3高门洞+4高走廊+一侧门板(绿style17)。
        /// 门槽严格3高(F4:槽上下实心锚),走廊4高(§2.5净空)。
        /// </summary>
        internal static bool LinkDryRooms(RoomNode a, RoomNode b, int floor, ushort wall, ref Tally tally) {
            RoomNode leftRoom = a.Bounds.Left <= b.Bounds.Left ? a : b;
            RoomNode rightRoom = ReferenceEquals(leftRoom, a) ? b : a;
            int gapL = leftRoom.Bounds.Right;
            int gapR = rightRoom.Bounds.Left;
            if (gapR - gapL > 40) {
                return false;
            }
            //门槽穿左房右壳/右房左壳:3高
            TileBrush.CarveRect(leftRoom.InteriorRight, floor - 3, leftRoom.Bounds.Right, floor, wall);
            TileBrush.CarveRect(rightRoom.Bounds.Left, floor - 3, rightRoom.InteriorLeft, floor, wall);
            //走廊段4高+地板顶板换绿
            for (int x = gapL; x < gapR; x++) {
                TileBrush.SetSolid(x, floor, L4Palette.Brick);
                TileBrush.SetSolid(x, floor - 5, L4Palette.Brick);
            }
            TileBrush.CarveRect(gapL, floor - 4, gapR, floor, wall);
            //门板放左房槽外列(DoorAudit:槽上下实心✓两侧净空由走廊/内膛保证)
            int doorX = leftRoom.Bounds.Right - 1;
            tally.Add(WorldGen.PlaceDoor(doorX, floor - 2, TileID.ClosedDoor, L4Palette.DoorStyle),
                "绿门", doorX, floor - 2);
            return true;
        }

        /// <summary>
        /// 注水坑陷阱(F31参数化,ROOMS-L4 §3:全世界注水坑收归本层;禁尖刺内衬——
        /// 溺水压力即坑的牙):干走廊地板3宽裂纹绿假地板,下方5宽7深水袋,
        /// 坑内平台=出坑梯(水里一跳上平台,平台一跳出坑口)。两态恒水。
        /// </summary>
        internal static void CarveWaterPit(int mouthLeft, int floor, ushort wall) {
            //坑体:比口各宽1,7深
            TileBrush.CarveRect(mouthLeft - 1, floor + 1, mouthLeft + 4, floor + 8, wall);
            for (int x = mouthLeft; x < mouthLeft + 3; x++) {
                TileBrush.SetSolid(x, floor, L4Palette.CrackedBrick);
            }
            //出坑平台:水面上1行,贴坑左壁
            TileBrush.PlatformRow(mouthLeft - 1, mouthLeft + 1, floor + 3, L4Palette.PlatformFrameY);
            //水袋4深(水面=floor+4行顶,距坑口3行——落进去必湿身,爬出来不卡人)
            L4WaterWorks.Register($"注水坑{mouthLeft}",
                new Rectangle(mouthLeft - 1, floor + 4, 5, 4), floor + 4, floor + 4);
        }

        /// <summary>
        /// 侧井检修龛:贴井壁的6x4小龛(井壁检修龛语法,§2.5),罐+壁灯歇脚点。
        /// shaftEdgeX:leftSide=true时为井内膛左缘(龛开在井左),false时为井内膛右缘+1(龛开在井右);
        /// y须对齐井内平台行,保证跨步进龛地板齐平。只在龛自身一侧盖壳,不碰井膛。
        /// </summary>
        internal static void CarveShaftAlcove(int shaftEdgeX, int y, bool leftSide, UnifiedRandom rand) {
            int left = leftSide ? shaftEdgeX - 6 : shaftEdgeX;
            int right = left + 6;
            //壳:龛外侧1列+顶板+地板;开口面(贴井)不盖
            int shellL = leftSide ? left - 1 : left;
            int shellR = leftSide ? right : right + 1;
            for (int x = shellL; x < shellR; x++) {
                for (int yy = y - 5; yy <= y; yy++) {
                    TileBrush.SetSolid(x, yy, L4Palette.Brick);
                }
            }
            TileBrush.CarveRect(left, y - 4, right, y, L4Palette.WallSlab);
            WorldGen.PlacePot(left + 2, y - 1, TileID.Pots,
                rand.Next(L4Palette.PotStyleMin, L4Palette.PotStyleMax));
            L4Palette.TryPlaceObject(left + 3, y - 4, TileID.HangingLanterns, L4Palette.LanternSconceStyle);
        }
    }
}
