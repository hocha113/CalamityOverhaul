using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //====================================================================
    //L3其余花名册房型(ROOMS-L3 §1 #1/#4/#5/#6/#7,全部纯算法):
    //  阅览大厅=长桌阵+夹层环廊(上区门面)
    //  目录厅=层内hub,目录柜(地牢梳妆台)双层环阵+落地钟
    //  抄写室=贴墙书台语法的室内化,墨渍做旧的原点
    //  灯房/开关廊=灭灯玩法教学房(全灭+找开关,一关多灯收尾)
    //  井站段/忏悔室/坠落房间A=跨层公共构件,归公共构件波,本文件不做
    //  禁书区=单入口封闭子区+大奖(钟声门门面framing,门体待运行时BellRiteSystem)
    //写入只走TileBrush+原版放置函数;家具拒绝即计数,fail loud交日志
    //====================================================================
    internal static class L3Rooms
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
                    CWRMod.Instance.Logger.Warn($"[L3Rooms] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //===告示牌文案(玩家可见,game-prose-voice纪律:具体物件+平收,不拔高)===
        internal const string SignIntake = "楼上牢里下来的档，先晾三天去潮，再上架。名字抄两份：一份进册，一份钉在门背后。";
        internal const string SignRegistry = "借阅登记：写名字，写层号。还书的划掉自己。没划掉的，馆里替你记着。";
        internal const string SignLampRule = "巡馆条例：灯灭了先找开关，开关不出十二步。烛火不许过第三排书。";
        internal const string SignVault = "禁书区。钟不响，门不开。灯自己灭了，别替它点。";

        //整包络重盖蓝砖+开内膛,所有房型共用第一遍(清预览残余)
        internal static void StampAndCarve(RoomNode room, ushort wall) {
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L3Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, wall);
        }

        //地板级标准门/拱插槽(底沿与地板齐平,§2.5接缝规则)
        internal static DoorSocket FloorDoor(RoomNode room, SocketSide side)
            => new(side, room.FloorTop - 3 - room.Bounds.Top, SocketKind.Door, 3);

        internal static DoorSocket FloorArch(RoomNode room, SocketSide side)
            => new(side, room.FloorTop - 4 - room.Bounds.Top, SocketKind.Archway, 4);

        //椅子放置:面向翻转镜像原版长桌语法(:29360-29368,两格frameX+=18)
        private static bool PlaceChair(int x, int standRow, bool flip, ref Tally tally) {
            bool ok = L3Palette.TryPlaceTile(x, standRow, TileID.Chairs, L3Palette.StyleChair);
            if (ok && flip) {
                Main.tile[x, standRow].TileFrameX += 18;
                Main.tile[x, standRow - 1].TileFrameX += 18;
            }
            tally.Add(ok, "椅", x, standRow);
            return ok;
        }

        //桌面摆件:镜像原版长桌台面分支(:29387-29413)——蜡烛/书/杯/墨瓶按权重
        private static void FurnishTabletop(int x, int surfaceRow, UnifiedRandom rand, ref Tally tally) {
            int roll = rand.Next(100);
            if (roll < 30) {
                tally.Add(L3Palette.PlaceOnSurface(x, surfaceRow, TileID.Candles, L3Palette.StyleCandle),
                    "桌面蜡烛", x, surfaceRow);
            }
            else if (roll < 65) {
                tally.Add(L3Palette.PlaceBook(x, surfaceRow, rand), "桌面书", x, surfaceRow);
            }
            else if (roll < 82) {
                tally.Add(L3Palette.PlaceOnSurface(x, surfaceRow, TileID.Bowls), "杯盏", x, surfaceRow);
            }
            else {
                tally.Add(L3Palette.PlaceInkBottle(x, surfaceRow, rand), "墨瓶", x, surfaceRow);
            }
        }

        //==================== 阅览大厅(#1):长桌阵+夹层环廊 ====================

        internal static Point ReadingHallInteriorSize(UnifiedRandom rand)
            => new(rand.Next(56, 89), rand.Next(20, 27));

        /// <summary>withIntakeSign=顶层首厅挂"罪档入库"告示(L2→L3隔离带呼应,ROOMS-L3 §4)</summary>
        internal static Tally BuildReadingHall(RoomNode room, UnifiedRandom rand, bool withIntakeSign) {
            var tally = new Tally();
            StampAndCarve(room, L3Palette.WallBase);
            int floor = room.FloorTop;
            int iL = room.InteriorLeft, iR = room.InteriorRight;

            //夹层环廊:两侧墙沿平台带(floor-8)+内端登步平台(floor-4),跳距≤4(F2)
            int bandW = System.Math.Clamp((iR - iL) / 4, 8, 16);
            TileBrush.PlatformRow(iL, iL + bandW, floor - 8, L3Palette.PlatformFrameY);
            TileBrush.PlatformRow(iR - bandW, iR, floor - 8, L3Palette.PlatformFrameY);
            TileBrush.PlatformRow(iL + bandW, iL + bandW + 3, floor - 4, L3Palette.PlatformFrameY);
            TileBrush.PlatformRow(iR - bandW - 3, iR - bandW, floor - 4, L3Palette.PlatformFrameY);
            //环廊书墙(书架锚SolidWithTop=平台合法,F7)
            for (int x = iL + 1; x + 3 <= iL + bandW; x += 5) {
                tally.Add(L3Palette.TryPlaceTile(x + 1, floor - 9, TileID.Bookcases, L3Palette.StyleBookcase),
                    "环廊书架", x + 1, floor - 9);
            }
            for (int x = iR - bandW + 1; x + 3 <= iR - 1; x += 5) {
                tally.Add(L3Palette.TryPlaceTile(x + 1, floor - 9, TileID.Bookcases, L3Palette.StyleBookcase),
                    "环廊书架", x + 1, floor - 9);
            }

            //长桌阵:桌+双椅(左椅面向翻转)+台面摆件,节距9(§2.4-③走道豁免之上取舒适值)
            for (int tx = iL + bandW + 6; tx <= iR - bandW - 6; tx += 9) {
                bool tableOk = L3Palette.TryPlaceTile(tx, floor - 1, TileID.Tables, L3Palette.StyleTable);
                tally.Add(tableOk, "长桌", tx, floor - 1);
                if (!tableOk) {
                    continue;
                }
                PlaceChair(tx - 2, floor - 1, flip: true, ref tally);
                PlaceChair(tx + 2, floor - 1, flip: false, ref tally);
                for (int dx = -1; dx <= 1; dx++) {
                    if (rand.NextBool()) {
                        FurnishTabletop(tx + dx, floor - 3, rand, ref tally);
                    }
                }
                //桌间落地灯(蓝地牢灯,阅览区全亮)
                if (rand.NextBool(3)) {
                    tally.Add(L3Palette.TryPlaceTile(tx + 4, floor - 1, TileID.Lamps, L3Palette.StyleLamp),
                        "落地灯", tx + 4, floor - 1);
                }
            }

            //吊灯:亮区全亮,不接开关(灭灯语言只在迷宫/禁书收口,ROOMS-L3 §2.2)
            for (int x = iL + (iR - iL) / 4; x < iR - 4; x += (iR - iL) / 3) {
                if (L3Lights.PlaceChandelier(x, room.InteriorTop)) {
                    L3Lights.LampsLit++;
                }
            }

            //挂画横排:原版间隔7语法(RESEARCH §1.1d-挂画),挂在桌面与夹层之间的墙带
            for (int x = iL + 6; x < iR - 6; x += 7) {
                if (L3Palette.InBlueInterior(x, floor - 6)) {
                    L3Palette.PlacePainting(x, floor - 6);
                }
            }

            //落地钟:档案与时间母题(ROOMS-L3 §2.1,阅览厅/目录厅各1)
            tally.Add(L3Palette.TryPlaceTile(iR - 3, floor - 1, TileID.GrandfatherClocks, L3Palette.StyleClock),
                "落地钟", iR - 3, floor - 1);

            if (withIntakeSign) {
                tally.Add(L3Palette.PlaceSignWithText(iL + 2, floor - 1, SignIntake), "罪档告示", iL + 2, floor - 1);
            }

            //地面散书两三本
            for (int c = rand.Next(2, 4); c > 0; c--) {
                L3Palette.PlaceBook(rand.Next(iL + 2, iR - 2), floor - 1, rand);
            }
            L3Palette.MoldUnderShelves(room.Bounds, rand);
            return tally;
        }

        //==================== 目录厅(#4):目录柜环阵hub ====================

        internal static Point CatalogInteriorSize(UnifiedRandom rand)
            => new(rand.Next(26, 37), rand.Next(14, 17));

        internal static Tally BuildCatalog(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L3Palette.WallBase);
            int floor = room.FloorTop;
            int iL = room.InteriorLeft, iR = room.InteriorRight;

            //上层环台:整幅平台(floor-7)+中央登步(floor-4),平台可下穿上跳
            TileBrush.PlatformRow(iL + 1, iR - 1, floor - 7, L3Palette.PlatformFrameY);
            int stepX = (iL + iR) / 2 - 1;
            TileBrush.PlatformRow(stepX, stepX + 3, floor - 4, L3Palette.PlatformFrameY);

            //目录柜环阵:地牢梳妆台(tile88蓝样式5)地面+环台双层,柜顶摆件
            void DresserRow(int standRow) {
                for (int x = iL + 2; x + 3 <= iR - 2; x += 5) {
                    //中央登步列留空
                    if (x + 3 > stepX && x < stepX + 3 && standRow == floor - 8) {
                        continue;
                    }
                    bool ok = L3Palette.TryPlaceTile(x + 1, standRow, TileID.Dressers, L3Palette.StyleDresser);
                    tally.Add(ok, "目录柜", x + 1, standRow);
                    if (ok && rand.NextBool()) {
                        //柜顶:登记册(书)或墨瓶
                        if (rand.NextBool()) {
                            L3Palette.PlaceBook(x + rand.Next(3), standRow - 2, rand);
                        }
                        else {
                            L3Palette.PlaceInkBottle(x + rand.Next(3), standRow - 2, rand);
                        }
                    }
                }
            }
            DresserRow(floor - 1);
            DresserRow(floor - 8);

            //落地钟+蛊惑桌(原版地牢保底家具,:29340先例)+分区旗一对(INDEX §7旗帜低档=定向)
            tally.Add(L3Palette.TryPlaceTile(iL + 3, floor - 1, TileID.GrandfatherClocks, L3Palette.StyleClock),
                "落地钟", iL + 3, floor - 1);
            tally.Add(L3Palette.TryPlaceTile(iR - 4, floor - 1, TileID.BewitchingTable),
                "蛊惑桌", iR - 4, floor - 1);
            tally.Add(L3Palette.TryPlaceObject(iL + 2, room.InteriorTop, TileID.Banners, L3Palette.StyleBannerA),
                "分区旗", iL + 2, room.InteriorTop);
            tally.Add(L3Palette.TryPlaceObject(iR - 3, room.InteriorTop, TileID.Banners, L3Palette.StyleBannerB),
                "分区旗", iR - 3, room.InteriorTop);

            //hub照明:单吊灯全亮+登记告示
            if (L3Lights.PlaceChandelier((iL + iR) / 2, room.InteriorTop)) {
                L3Lights.LampsLit++;
            }
            tally.Add(L3Palette.PlaceSignWithText(stepX - 2, floor - 1, SignRegistry), "登记告示", stepX - 2, floor - 1);

            L3Palette.MoldUnderShelves(room.Bounds, rand);
            return tally;
        }

        //==================== 抄写室(#5):书台语法室内化 ====================

        internal static Point ScriptoriumInteriorSize(UnifiedRandom rand)
            => new(rand.Next(10, 17), rand.Next(6, 8));

        internal static Tally BuildScriptorium(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L3Palette.WallBase);
            int floor = room.FloorTop;
            int iL = room.InteriorLeft, iR = room.InteriorRight;

            //抄写台:工作台+面向翻转的椅,台面书/墨瓶/杯(原版台面分支:29449-29487)
            int deskX = iL + 2;
            bool deskOk = L3Palette.TryPlaceTile(deskX, floor - 1, TileID.WorkBenches, L3Palette.StyleWorkbench);
            tally.Add(deskOk, "抄写台", deskX, floor - 1);
            if (deskOk) {
                PlaceChair(deskX - 1, floor - 1, flip: true, ref tally);
                FurnishTabletop(deskX, floor - 2, rand, ref tally);
                FurnishTabletop(deskX + 1, floor - 2, rand, ref tally);
                //桌下墨渍(做旧签名原点,INDEX §3)
                L3Palette.InkStreak(deskX, floor - 1, 1);
            }

            //右半:炼药桌(原版地牢保底家具,:29331先例)或第二张抄写位
            if (iR - iL >= 12 && rand.Next(100) < 40) {
                tally.Add(L3Palette.TryPlaceTile(iR - 3, floor - 1, TileID.AlchemyTable),
                    "炼药桌", iR - 3, floor - 1);
            }
            else {
                tally.Add(WorldGen.PlacePot(iR - 2, floor - 1, TileID.Pots,
                    rand.Next(L3Palette.PotStyleMin, L3Palette.PotStyleMax + 1)), "罐", iR - 2, floor - 1);
            }

            //贴墙书台:小龛平台2宽+书(F30语法室内化)
            int nookX = (iL + iR) / 2;
            TileBrush.PlatformRow(nookX, nookX + 2, floor - 4, L3Palette.PlatformFrameY);
            tally.Add(L3Palette.PlaceBook(nookX, floor - 5, rand), "龛上书", nookX, floor - 5);

            //烛台一盏(抄写间自亮,不入灭灯回路)
            tally.Add(L3Palette.PlaceOnSurface(iL + 1, floor - 1, TileID.Candelabras, L3Palette.StyleCandelabra),
                "烛台", iL + 1, floor - 1);

            L3Palette.MoldUnderShelves(room.Bounds, rand);
            return tally;
        }

        //==================== 灯房/开关廊(#6):灭灯玩法教学房 ====================

        internal static Point LampGalleryInteriorSize(UnifiedRandom rand)
            => new(rand.Next(22, 35), 6);

        internal static Tally BuildLampGallery(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L3Palette.WallBase);
            int floor = room.FloorTop;
            int iL = room.InteriorLeft, iR = room.InteriorRight;
            int ceil = room.InteriorTop;

            //3~5盏灯全灭:前n-2盏单灯单开关(开关贴灯下±2列,空间对应=教学),
            //末两盏共用一只开关(一关多灯,教学升级,ROOMS-L3 §1)
            int lamps = System.Math.Clamp((iR - iL) / 7, 3, 5);
            var xs = new int[lamps];
            for (int i = 0; i < lamps; i++) {
                xs[i] = iL + 3 + i * (iR - iL - 6) / System.Math.Max(1, lamps - 1);
            }
            for (int i = 0; i < lamps - 2; i++) {
                if (!L3Lights.PlaceLantern(xs[i], ceil, caged: i % 2 == 1)) {
                    tally.Add(false, "教学灯笼", xs[i], ceil);
                    continue;
                }
                tally.Placed++;
                int sx = System.Math.Clamp(xs[i] + rand.Next(-2, 3), iL, iR - 1);
                if (L3Lights.TryPlaceSwitch(sx, floor - 1)) {
                    L3Lights.WireStaircase(sx, floor - 1, xs[i], ceil);
                    L3Lights.ExtinguishLantern(xs[i], ceil);
                    L3Lights.LampsOff++;
                }
                else {
                    L3Lights.LampsLit++;
                    CWRMod.Instance.Logger.Warn($"[L3Rooms] 灯房开关落位失败 at ({sx},{floor - 1}),该灯保持点亮");
                }
            }
            //末两盏成串
            int chained = L3Lights.PlaceLampChain(new[] { xs[lamps - 2], xs[lamps - 1] }, ceil,
                iR - 2, floor - 1, caged: true, rand);
            tally.Placed += chained;

            //巡馆条例告示+墙面小书台(黑屋里摸到的第一件东西是规则)
            tally.Add(L3Palette.PlaceSignWithText(iL + 1, floor - 1, SignLampRule), "条例告示", iL + 1, floor - 1);
            TileBrush.PlatformRow(iL + 4, iL + 6, floor - 3, L3Palette.PlatformFrameY);
            L3Palette.PlaceBook(iL + 4, floor - 4, rand);

            return tally;
        }

        //==================== 禁书区(#7):单入口封闭子区+大奖 ====================

        internal static Point VaultInteriorSize(UnifiedRandom rand)
            => new(rand.Next(36, 57), rand.Next(18, 25));

        /// <summary>
        /// 禁书区主体:Slab墙9成(ROOMS-L3 §0)+双行密架格间+无撒布照明。
        /// 唯一入口的Archway由调用方开洞后再调SealVaultEntrance补门面framing;
        /// 门体=运行时BellRiteSystem(【事实 §4.1】),生成期只留过梁+封条+预告斑。
        /// </summary>
        internal static Tally BuildVault(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L3Palette.WallSlab);
            int floor = room.FloorTop;
            int iL = room.InteriorLeft, iR = room.InteriorRight;
            int h = floor - room.InteriorTop;

            //一成基础墙小圆斑回混(Slab约9成,§0墙基调)
            for (int d = 0; d < 3; d++) {
                L3Palette.WallDisk(rand.Next(iL + 4, iR - 4), rand.Next(room.InteriorTop + 3, floor - 3),
                    rand.Next(3, 6), L3Palette.WallBase);
            }

            //双行格间:下行阅架,上行藏珍;行间楼板+单缺口(幽闭尺度12x8,ROOMS-L3 §1)
            int rowH = (h - 2) / 2;
            int upperFloor = floor - rowH - 2;
            for (int x = iL; x < iR; x++) {
                TileBrush.SetSolid(x, upperFloor, L3Palette.Brick);
                TileBrush.SetSolid(x, upperFloor + 1, L3Palette.Brick);
            }
            int gapX = (iL + iR) / 2 - 1;
            TileBrush.CarveRect(gapX, upperFloor, gapX + 3, upperFloor + 2, L3Palette.WallSlab);
            TileBrush.PlatformRow(gapX, gapX + 3, upperFloor, L3Palette.PlatformFrameY);
            for (int py = floor - 4; py > upperFloor + 1; py -= 4) {
                TileBrush.PlatformRow(gapX, gapX + 3, py, L3Palette.PlatformFrameY);
            }

            //两行各切格间(12~14宽,密架aisle=3)
            var cellEdges = new System.Collections.Generic.List<(int floorRow, int left, int right)>();
            foreach (int rowFloor in new[] { floor, upperFloor }) {
                int rowTop = rowFloor == floor ? upperFloor + 2 : room.InteriorTop;
                int cursor = iL;
                while (true) {
                    int cw = rand.Next(12, 15);
                    if (iR - cursor < cw + 2 + 12) {
                        cellEdges.Add((rowFloor, cursor, iR));
                        break;
                    }
                    cellEdges.Add((rowFloor, cursor, cursor + cw));
                    for (int dx = 0; dx < 2; dx++) {
                        for (int y = rowTop; y < rowFloor; y++) {
                            TileBrush.SetSolid(cursor + cw + dx, y, L3Palette.Brick);
                        }
                    }
                    TileBrush.CarveRect(cursor + cw, rowFloor - 3, cursor + cw + 2, rowFloor, L3Palette.WallSlab);
                    cursor += cw + 2;
                }
            }

            //密架+大奖:全格间峰值书架;金箱2~3只沉在上行两端与下行远端(钟声门后,ROOMS-L3 §3)
            int goldChests = 0;
            foreach ((int rowFloor, int left, int right) in cellEdges) {
                for (int x = left + 1; x + 3 <= right - 1; x += 6) {
                    tally.Add(L3Palette.TryPlaceTile(x + 1, rowFloor - 1, TileID.Bookcases, L3Palette.StyleBookcase),
                        "禁书架", x + 1, rowFloor - 1);
                }
            }
            void Gold(int x, int standRow) {
                if (goldChests < 3 && L3Palette.PlaceChestWithLoot(x, standRow, gold: true)) {
                    goldChests++;
                    tally.Placed++;
                }
            }
            Gold(iL + 5, upperFloor - 1);
            Gold(iR - 6, upperFloor - 1);
            Gold(iR - 8, floor - 1);
            if (goldChests == 0) {
                CWRMod.Instance.Logger.Error($"[L3Rooms] 禁书区零金箱@{room.Bounds},大奖纪律违约,责任=密架排布挤占");
            }

            //玩法灯收口:两盏灭灯,开关藏在异格间(找开关点亮一片的收口,ROOMS-L3 §1)
            int lampA = iL + (iR - iL) / 4;
            int lampB = iR - (iR - iL) / 4;
            if (L3Lights.PlaceLantern(lampA, upperFloor + 2, caged: true)) {
                if (L3Lights.TryPlaceSwitch(lampB + 1, floor - 1)) {
                    L3Lights.WireStaircase(lampB + 1, floor - 1, lampA, upperFloor + 2);
                    L3Lights.ExtinguishLantern(lampA, upperFloor + 2);
                    L3Lights.LampsOff++;
                }
                else {
                    L3Lights.LampsLit++;
                }
            }
            if (L3Lights.PlaceLantern(lampB, room.InteriorTop, caged: true)) {
                if (L3Lights.TryPlaceSwitch(lampA - 1, upperFloor - 1)) {
                    L3Lights.WireStaircase(lampA - 1, upperFloor - 1, lampB, room.InteriorTop);
                    L3Lights.ExtinguishLantern(lampB, room.InteriorTop);
                    L3Lights.LampsOff++;
                }
                else {
                    L3Lights.LampsLit++;
                }
            }

            //稀疏水蜡烛+蛛网(尘封感,INDEX §3蛛网L3=禁书区少量)
            for (int c = 0; c < 2; c++) {
                if (L3Palette.PlaceOnSurface(rand.Next(iL + 2, iR - 2), floor - 1, TileID.WaterCandle)) {
                    tally.Placed++;
                }
            }
            for (int c = 0; c < 6; c++) {
                int wx = rand.Next(iL + 1, iR - 1);
                int wy = rand.Next(room.InteriorTop, floor - 1);
                if (L3Palette.InBlueInterior(wx, wy)) {
                    WorldGen.PlaceTile(wx, wy, TileID.Cobweb, mute: true);
                }
            }

            //重墨霉:签名密度全层峰值(paint层)
            for (int d = 0; d < 4; d++) {
                L3Palette.MoldBlotch(rand.Next(iL + 3, iR - 3), rand.Next(room.InteriorTop + 2, floor - 1),
                    rand.Next(2, 5), rand);
            }
            L3Palette.MoldUnderShelves(room.Bounds, rand);
            return tally;
        }

        /// <summary>
        /// 禁书区门面framing:调用方用OpenWallSocket开出唯一Archway后调用。
        /// 过梁换裂纹蓝砖(材质区分,不改碰撞,§2.5-3);洞壁墙面刷黑=封条+门体预告斑
        /// (运行时BellRiteSystem在此挂钟声门体,INDEX §4)。
        /// </summary>
        internal static void SealVaultEntrance(RoomNode room, DoorSocket socket) {
            int top = room.Bounds.Top + socket.Offset;
            int left = socket.Side == SocketSide.Left ? room.Bounds.Left : room.InteriorRight;
            //过梁:开口正上一行的壳砖换裂纹蓝(几何不变)
            for (int dx = 0; dx < DungeonworldMetrics.RoomShellThick; dx++) {
                TileBrush.SetSolid(left + dx, top - 1, L3Palette.CrackedBrick);
            }
            //封条:洞内两列墙面刷黑(paint层),即门体留位标记
            for (int dx = 0; dx < DungeonworldMetrics.RoomShellThick; dx++) {
                for (int dy = 0; dy < socket.Width; dy++) {
                    Tile tile = Main.tile[left + dx, top + dy];
                    if (!tile.HasTile && tile.WallType != WallID.None) {
                        tile.WallColor = L3Palette.PaintInk;
                    }
                }
            }
            //警示告示立在洞外一步
            int signX = socket.Side == SocketSide.Left ? room.Bounds.Left - 2 : room.Bounds.Right + 1;
            L3Palette.PlaceSignWithText(signX, room.FloorTop - 1, SignVault);
        }
    }
}
