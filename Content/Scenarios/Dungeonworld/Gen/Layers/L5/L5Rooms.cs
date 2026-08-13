using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //L5房型库(ROOMS-L5 §1花名册,全部纯算法):
    //骨柱大厅(#1)/龛壁廊(#2)/亡灵集市(#3壳)/圣骨堂(#5内部)/坑陷阱场(#6干坑两型)/深巷骨室(#7节点)
    //骨井(#4)与游走连接由L5Content编排;井站/忏悔室/坠落房间B=跨层公共构件,归公共构件波(L2先例)
    //骨砌语法:骨块结构件+粉砖收边维持群系计数(F12/F13);写入只走TileBrush,家具经原版放置函数
    internal static class L5Rooms
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
                    CWRMod.Instance.Logger.Warn($"[L5Rooms] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //房型交付信息:装修记账+对外接口位(距Bounds.Left的列偏移,-1=该房不开此类口)
        internal struct RoomInfo
        {
            internal Tally Tally;
            //地板PlatformGap许可位(井口/下行坑道出发点,建造时保证该带无家具无坑)
            internal int FloorGapOffset;
            //天花PlatformGap许可位(高位落口,ROOMS-L5门插槽;下方保证有承接平台/横档)
            internal int CeilGapOffset;
        }

        //整包络重盖粉砖+开内膛(M0蓝底换皮),所有房型共用第一遍(L2Rooms.StampAndCarve同构)
        internal static void StampAndCarve(RoomNode room, ushort wall) {
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L5Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, wall);
        }

        //==================== 骨柱大厅(#1):柱林节奏+夹层环廊,本层节点大房 ====================

        internal static Point HallInteriorSize(UnifiedRandom rand)
            => new(rand.Next(42, 73), rand.Next(18, 27));

        internal static RoomInfo BuildHall(RoomNode room, UnifiedRandom rand) {
            var info = new RoomInfo { FloorGapOffset = DungeonworldMetrics.RoomShellThick + 2, CeilGapOffset = DungeonworldMetrics.RoomShellThick + 2 };
            StampAndCarve(room, L5Palette.WallSlab);
            int floor = room.FloorTop;
            int top = room.InteriorTop;
            int iw = room.InteriorRight - room.InteriorLeft;

            //夹层环廊:两侧墙平台带+之字梯(§2.4-⑤);带宽≤12,高取内高一半
            int mezzY = floor - System.Math.Clamp((floor - top) / 2, 7, 11);
            int bandW = System.Math.Min(12, iw / 4);
            TileBrush.PlatformRow(room.InteriorLeft, room.InteriorLeft + bandW, mezzY, L5Palette.PlatformBone);
            TileBrush.PlatformRow(room.InteriorRight - bandW, room.InteriorRight, mezzY, L5Palette.PlatformBone);
            //之字梯:带内缘下方交错横档,竖距4(F2满跳6.6留余量)
            for (int side = 0; side < 2; side++) {
                int edge = side == 0 ? room.InteriorLeft + bandW : room.InteriorRight - bandW - 2;
                int zig = 0;
                for (int y = mezzY + 4; y < floor; y += 4) {
                    int sx = edge + (zig++ % 2 == 0 ? 0 : side == 0 ? 2 : -2);
                    TileBrush.SetPlatform(sx, y, L5Palette.PlatformBone);
                    TileBrush.SetPlatform(sx + 1, y, L5Palette.PlatformBone);
                }
            }

            //骨柱:3~4宽、柱距10~14、柱头柱础外扩1(檐口语法,§2.4-⑤);
            //柱身受控侵蚀:单侧抠1格深、连续3~5行、剩余厚度≥2(§3.2-6)
            int spanL = room.InteriorLeft + bandW + 4;
            int spanR = room.InteriorRight - bandW - 4;
            int px = spanL + rand.Next(0, 3);
            var bays = new System.Collections.Generic.List<int>();
            int lastRight = spanL;
            while (true) {
                int pw = rand.Next(3, 5);
                if (px + pw > spanR) {
                    break;
                }
                for (int x = px; x < px + pw; x++) {
                    for (int y = top; y < floor; y++) {
                        TileBrush.SetSolid(x, y, L5Palette.Bone);
                    }
                }
                //柱头(顶行)/柱础(底行)外扩
                TileBrush.SetSolid(px - 1, top, L5Palette.Bone);
                TileBrush.SetSolid(px + pw, top, L5Palette.Bone);
                TileBrush.SetSolid(px - 1, floor - 1, L5Palette.Bone);
                TileBrush.SetSolid(px + pw, floor - 1, L5Palette.Bone);
                if (rand.NextBool(3)) {
                    int ex = rand.NextBool(2) ? px : px + pw - 1;
                    int ey = rand.Next(top + 3, floor - 7);
                    for (int len = rand.Next(3, 6); len > 0 && ey < floor - 3; len--, ey++) {
                        TileBrush.ClearCell(ex, ey, L5Palette.WallSlab);
                    }
                }
                //柱基骨堆+尘白(骨屑落在柱脚)
                info.Tally.Add(L5Palette.PlaceSmallBones(px - 2, floor - 1, rand), "柱基骨堆", px - 2, floor - 1);
                L5Palette.DustFloorRun(px - 2, floor, pw + 4);
                L5Palette.DustWallWash(px - 2, top, px + pw + 2, top + 2);

                bays.Add((lastRight + px) / 2);
                lastRight = px + pw;
                px += pw + rand.Next(10, 15);
            }
            bays.Add((lastRight + spanR) / 2);

            //柱间天花:骨吊灯与吊笼交替(吊笼=绷直承重悬链的本层限定形态)
            for (int i = 0; i < bays.Count; i++) {
                int bx = bays[i];
                if (i % 2 == 0) {
                    info.Tally.Add(L5Palette.TryPlaceObject(bx, top, TileID.Chandeliers,
                        L5Palette.ChandelierBone), "骨吊灯", bx, top);
                }
                else {
                    info.Tally.Add(L5Palette.HangingBasket(bx, top, rand), "吊笼", bx, top);
                }
            }
            //夹层带下挂骨灯笼(平台顶锚alternate合法)
            info.Tally.Add(L5Palette.TryPlaceObject(room.InteriorLeft + bandW / 2, mezzY + 1,
                TileID.HangingLanterns, L5Palette.LanternBone), "夹层灯", room.InteriorLeft + bandW / 2, mezzY + 1);
            //地面散件:罐+大骨堆
            info.Tally.Add(L5Palette.PlaceUrn(room.InteriorLeft + bandW + 2, floor - 1, rand),
                "骨灰瓮", room.InteriorLeft + bandW + 2, floor - 1);
            info.Tally.Add(L5Palette.PlaceLargeBones(room.InteriorRight - bandW - 3, floor - 1, rand),
                "大骨堆", room.InteriorRight - bandW - 3, floor - 1);
            return info;
        }

        //==================== 龛壁廊(#2):两壁葬龛阵列,骨窖身份房 ====================

        internal static Point GalleryInteriorSize(UnifiedRandom rand)
            => new(rand.Next(30, 57), rand.Next(8, 12));

        internal static RoomInfo BuildGallery(RoomNode room, UnifiedRandom rand) {
            var info = new RoomInfo { FloorGapOffset = DungeonworldMetrics.RoomShellThick + 2, CeilGapOffset = DungeonworldMetrics.RoomShellThick + 2 };
            StampAndCarve(room, L5Palette.WallSlab);
            int floor = room.FloorTop;
            int top = room.InteriorTop;
            int corridorTop = top + 3;

            //回填顶3行作龛带(L2顶龛先例同构),主廊净高=内高-3
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                for (int y = top; y < corridorTop; y++) {
                    TileBrush.SetSolid(x, y, L5Palette.Brick);
                }
            }

            //葬龛阵列:2x3龛+骨框柱,间距4~6;左缘6列留净(接口位)
            //龛内容轮换[骨堆/骨灰瓮/烛],同型间隔≥2龛由三型循环构造保证;
            //另掷一龛为"新鲜空龛"(无尘无物,叙事留白,ROOMS-L5 §1)
            var nicheXs = new System.Collections.Generic.List<int>();
            int nx = room.InteriorLeft + 7;
            while (nx + 3 < room.InteriorRight - 2) {
                nicheXs.Add(nx);
                nx += 2 + rand.Next(4, 7);
            }
            int freshIdx = nicheXs.Count > 0 ? rand.Next(nicheXs.Count) : -1;
            for (int i = 0; i < nicheXs.Count; i++) {
                int x0 = nicheXs[i];
                TileBrush.CarveRect(x0, top, x0 + 2, corridorTop, L5Palette.WallTiled);
                //骨框柱(骨咬砖的接缝语法)
                for (int y = top; y < corridorTop; y++) {
                    TileBrush.SetSolid(x0 - 1, y, L5Palette.Bone);
                    TileBrush.SetSolid(x0 + 2, y, L5Palette.Bone);
                }
                if (i == freshIdx) {
                    continue; //新鲜挖出的空位
                }
                //天花龛底铺骨平台作站面:2D里"两壁葬龛"的上壁落地;
                //轮换骨堆/骨灰瓮/烛(ROOMS-L5 §1),蛛网作做旧点缀不占轮换名额
                int shelfY = corridorTop - 1;
                TileBrush.PlatformRow(x0, x0 + 2, shelfY, L5Palette.PlatformBone);
                switch (i % 3) {
                    case 0:
                        info.Tally.Add(L5Palette.TryPlaceTile(x0, shelfY - 1, TileID.Candles,
                            L5Palette.CandlePink), "龛烛", x0, shelfY - 1);
                        break;
                    case 1:
                        info.Tally.Add(L5Palette.PlaceUrn(x0, shelfY - 1, rand), "龛瓮", x0, shelfY - 1);
                        break;
                    default:
                        info.Tally.Add(L5Palette.PlaceSmallBones(x0, shelfY - 1, rand), "龛骨", x0, shelfY - 1);
                        break;
                }
                if (i % 2 == 0) {
                    WorldGen.PlaceTile(x0 + 1, top, TileID.Cobweb, mute: true);
                }
                L5Palette.DustWallWash(x0, top, x0 + 2, corridorTop);
                //隔龛地crypt:地板下藏骨穴,平台唇上可行走(2D版地面葬龛)
                if (i % 2 == 1) {
                    CarveFloorCrypt(x0, floor, rand, ref info.Tally);
                }
            }

            //廊内散件与主灯
            int mid = (room.InteriorLeft + room.InteriorRight) / 2;
            info.Tally.Add(L5Palette.TryPlaceObject(mid, corridorTop, TileID.HangingLanterns,
                L5Palette.LanternBone), "廊灯", mid, corridorTop);
            info.Tally.Add(L5Palette.PlaceUrn(room.InteriorLeft + 4, floor - 1, rand),
                "骨灰瓮", room.InteriorLeft + 4, floor - 1);
            info.Tally.Add(L5Palette.PlaceSmallBones(mid + 3, floor - 1, rand), "骨堆", mid + 3, floor - 1);
            return info;
        }

        //地crypt公共件:3宽2深地板下骨穴+骨平台唇(龛壁廊/深巷骨室共用)
        private static void CarveFloorCrypt(int x0, int floor, UnifiedRandom rand, ref Tally tally) {
            TileBrush.CarveRect(x0, floor, x0 + 3, floor + 2, L5Palette.WallTiled);
            TileBrush.PlatformRow(x0, x0 + 3, floor, L5Palette.PlatformBone);
            tally.Add(L5Palette.PlaceSmallBones(x0 + 1, floor + 1, rand), "crypt骨", x0 + 1, floor + 1);
            L5Palette.DustWallWash(x0, floor, x0 + 3, floor + 2);
        }

        //==================== 亡灵集市(#3):Safe壳+熄火集市占位(篝火/台面/空货架) ====================
        //商贩NPC与商单归后续波(ROOMS-L5 §1:本表只保证站位平地与摊前净空)

        internal static Point MarketInteriorSize(UnifiedRandom rand)
            => new(rand.Next(56, 73), rand.Next(18, 23));

        //摊型五选(ROOMS-L5 §1【待签字】按保守解落原版件):骨匠/烛贩/故物/汤锅/空摊
        private enum StallKind { BoneWright, Chandler, Curios, SoupPot, Empty }

        internal static RoomInfo BuildMarket(RoomNode room, UnifiedRandom rand) {
            var info = new RoomInfo { FloorGapOffset = DungeonworldMetrics.RoomShellThick + 2, CeilGapOffset = DungeonworldMetrics.RoomShellThick + 2 };
            //集市局部用基础墙9(墙面20%配比落点;Safe房刷怪治理归运行时§4.5)
            StampAndCarve(room, L5Palette.WallBase);
            int floor = room.FloorTop;
            int top = room.InteriorTop;
            int mid = (room.InteriorLeft + room.InteriorRight) / 2;

            //中央篝火=中途唯一的暖;骨桌骨椅休息位+骨吊灯+集市幡(样式12/13变体墙组)
            info.Tally.Add(L5Palette.TryPlaceObject(mid, floor - 1, TileID.Campfire,
                L5Palette.CampfireBone), "骨篝火", mid, floor - 1);
            info.Tally.Add(L5Palette.TryPlaceTile(mid - 4, floor - 1, TileID.Tables, L5Palette.TableBone),
                "骨桌", mid - 4, floor - 1);
            info.Tally.Add(L5Palette.TryPlaceTile(mid - 6, floor - 1, TileID.Chairs, L5Palette.ChairBone),
                "骨椅", mid - 6, floor - 1);
            info.Tally.Add(L5Palette.TryPlaceTile(mid + 4, floor - 1, TileID.Tables, L5Palette.TableBone),
                "骨桌", mid + 4, floor - 1);
            info.Tally.Add(L5Palette.TryPlaceTile(mid + 6, floor - 1, TileID.Chairs, L5Palette.ChairBone),
                "骨椅", mid + 6, floor - 1);
            info.Tally.Add(L5Palette.TryPlaceObject(mid, top, TileID.Chandeliers,
                L5Palette.ChandelierBone), "集市吊灯", mid, top);
            info.Tally.Add(L5Palette.TryPlaceTile(mid - 9, top, TileID.Banners, L5Palette.BannerMarketA),
                "集市幡", mid - 9, top);
            info.Tally.Add(L5Palette.TryPlaceTile(mid + 9, top, TileID.Banners, L5Palette.BannerMarketB),
                "集市幡", mid + 9, top);

            //摊位带:左缘6列留净(接口位),摊距≥13保证每摊3x3站位+摊前4宽净空;篝火区±11不摆摊
            StallKind[] rotation = [StallKind.BoneWright, StallKind.Chandler, StallKind.SoupPot,
                StallKind.Curios, StallKind.Empty];
            int stallIdx = rand.Next(rotation.Length);
            int stalls = 0;
            int sx = room.InteriorLeft + 7 + rand.Next(0, 3);
            while (sx + 8 < room.InteriorRight - 2 && stalls < 5) {
                if (System.Math.Abs(sx + 2 - mid) < 11) {
                    sx = mid + 11;
                    continue;
                }
                BuildStall(sx, floor, rotation[stallIdx++ % rotation.Length], rand, ref info.Tally);
                stalls++;
                sx += rand.Next(13, 17);
            }

            //集市局部"高"光照:骨灯笼节拍补光+地面烛台
            for (int x = room.InteriorLeft + 8; x < room.InteriorRight - 4; x += rand.Next(11, 15)) {
                if (System.Math.Abs(x - mid) > 4) {
                    info.Tally.Add(L5Palette.TryPlaceObject(x, top, TileID.HangingLanterns,
                        L5Palette.LanternBone), "集市灯", x, top);
                }
            }
            info.Tally.Add(L5Palette.TryPlaceTile(room.InteriorRight - 4, floor - 1, TileID.Candelabras,
                L5Palette.CandelabraPink), "烛台", room.InteriorRight - 4, floor - 1);
            //尘白地带:人踩出来的市集中带
            L5Palette.DustFloorRun(mid - 14, floor, 28);
            return info;
        }

        //摊位=骨块台脚x2+骨平台台面4宽+双层空货架3宽+摊灯;摊型只换陈设
        private static void BuildStall(int x, int floor, StallKind kind, UnifiedRandom rand, ref Tally tally) {
            TileBrush.SetSolid(x, floor - 1, L5Palette.Bone);
            TileBrush.SetSolid(x + 3, floor - 1, L5Palette.Bone);
            for (int dx = 0; dx < 4; dx++) {
                TileBrush.SetPlatform(x + dx, floor - 2, L5Palette.PlatformBone);
            }
            //空货架(熄火集市占位:货由M4战利品表/商贩波填充)
            for (int dx = 0; dx < 3; dx++) {
                TileBrush.SetPlatform(x + dx, floor - 5, L5Palette.PlatformBone);
                TileBrush.SetPlatform(x + dx, floor - 8, L5Palette.PlatformBone);
            }
            //摊灯:顶层货架下挂笼灯(平台顶锚alternate)
            tally.Add(L5Palette.TryPlaceObject(x + 1, floor - 7, TileID.HangingLanterns,
                L5Palette.LanternCaged), "摊灯", x + 1, floor - 7);
            switch (kind) {
                case StallKind.BoneWright:
                    tally.Add(L5Palette.TryPlaceTile(x + 6, floor - 1, TileID.WorkBenches,
                        L5Palette.WorkBenchBone), "骨匠台", x + 6, floor - 1);
                    tally.Add(L5Palette.PlaceLargeBones(x - 2, floor - 1, rand), "骨料堆", x - 2, floor - 1);
                    break;
                case StallKind.Chandler:
                    tally.Add(L5Palette.TryPlaceTile(x + 1, floor - 3, TileID.Candles,
                        L5Palette.CandlePink), "台面蜡烛", x + 1, floor - 3);
                    tally.Add(L5Palette.TryPlaceTile(x + 6, floor - 1, TileID.Candelabras,
                        L5Palette.CandelabraPink), "烛台", x + 6, floor - 1);
                    break;
                case StallKind.Curios:
                    tally.Add(L5Palette.PlaceUrn(x + 6, floor - 1, rand), "故物瓮", x + 6, floor - 1);
                    tally.Add(L5Palette.PlaceUrn(x - 2, floor - 1, rand), "故物瓮", x - 2, floor - 1);
                    break;
                case StallKind.SoupPot:
                    tally.Add(L5Palette.TryPlaceObject(x + 6, floor - 1, TileID.CookingPots, 0),
                        "汤锅", x + 6, floor - 1);
                    tally.Add(L5Palette.TryPlaceTile(x + 1, floor - 3, TileID.Candles,
                        L5Palette.CandlePink), "台面蜡烛", x + 1, floor - 3);
                    break;
                default:
                    //空摊:叙事留白,只积尘结网
                    WorldGen.PlaceTile(x + 1, floor - 4, TileID.Cobweb, mute: true);
                    break;
            }
            L5Palette.DustFloorRun(x - 1, floor, 6);
        }

        //==================== 圣骨堂(#5):钟声门宝库内部(门面/门禁TP归钟声门机构波) ====================

        internal static Point OssuaryInteriorSize(UnifiedRandom rand)
            => new(rand.Next(20, 29), rand.Next(12, 15));

        internal static RoomInfo BuildOssuary(RoomNode room, UnifiedRandom rand) {
            var info = new RoomInfo { FloorGapOffset = -1, CeilGapOffset = -1 };
            StampAndCarve(room, L5Palette.WallTiled);
            int floor = room.FloorTop;
            int top = room.InteriorTop;
            int mid = (room.InteriorLeft + room.InteriorRight) / 2;

            //骨须台:两级收分骨砌祭台(边1高中2高,1格台阶自动登F3)
            for (int x = mid - 3; x <= mid + 3; x++) {
                int h = x == mid - 3 || x == mid + 3 ? 1 : 2;
                for (int dy = 1; dy <= h; dy++) {
                    TileBrush.SetSolid(x, floor - dy, L5Palette.Bone);
                }
            }
            //大奖占位:锁金箱(F35;正式轮换表归M4)
            info.Tally.Add(PlaceGoldChest(mid - 1, floor - 3), "圣骨堂金箱", mid - 1, floor - 3);

            //骨柱一对flanking祭台+圣物龛带(顶3行龛带,骨灯笼守龛)
            for (int side = -1; side <= 1; side += 2) {
                int cx = mid + side * 7;
                if (cx - 1 <= room.InteriorLeft || cx + 1 >= room.InteriorRight - 1) {
                    continue;
                }
                for (int y = top; y < floor; y++) {
                    TileBrush.SetSolid(cx, y, L5Palette.Bone);
                    TileBrush.SetSolid(cx + 1, y, L5Palette.Bone);
                }
                TileBrush.SetSolid(cx - 1, top, L5Palette.Bone);
                TileBrush.SetSolid(cx + 2, top, L5Palette.Bone);
                TileBrush.SetSolid(cx - 1, floor - 1, L5Palette.Bone);
                TileBrush.SetSolid(cx + 2, floor - 1, L5Palette.Bone);
            }
            info.Tally.Add(L5Palette.TryPlaceObject(mid, top, TileID.Chandeliers,
                L5Palette.ChandelierBone), "堂吊灯", mid, top);
            info.Tally.Add(L5Palette.TryPlaceTile(mid - 5, floor - 1, TileID.Candelabras,
                L5Palette.CandelabraPink), "烛台", mid - 5, floor - 1);
            info.Tally.Add(L5Palette.TryPlaceTile(mid + 5, floor - 1, TileID.Candelabras,
                L5Palette.CandelabraPink), "烛台", mid + 5, floor - 1);
            info.Tally.Add(L5Palette.PlaceUrn(room.InteriorLeft + 2, floor - 1, rand),
                "圣物瓮", room.InteriorLeft + 2, floor - 1);
            info.Tally.Add(L5Palette.PlaceUrn(room.InteriorRight - 3, floor - 1, rand),
                "圣物瓮", room.InteriorRight - 3, floor - 1);
            info.Tally.Add(L5Palette.PlaceLargeBones(room.InteriorLeft + 5, floor - 1, rand),
                "圣骨堆", room.InteriorLeft + 5, floor - 1);
            //尘白重涂:最老的骨窖区
            L5Palette.DustWallWash(room.InteriorLeft, top, room.InteriorRight, top + 3);
            L5Palette.DustFloorRun(room.InteriorLeft, floor, room.InteriorRight - room.InteriorLeft);
            return info;
        }

        //金箱+占位补给(正式战利品表对位M4;镜像L1.PlaceChestWithLoot形制)
        private static bool PlaceGoldChest(int x, int standRow) {
            int index = WorldGen.PlaceChest(x, standRow, TileID.Containers,
                notNearOtherChests: false, L5Palette.ChestLockedGold);
            if (index < 0) {
                return false;
            }
            Chest chest = Main.chest[index];
            int slot = 0;
            void Add(int itemId, int stack) {
                if (slot >= chest.item.Length) {
                    return;
                }
                chest.item[slot] = new Item();
                chest.item[slot].SetDefaults(itemId);
                chest.item[slot].stack = stack;
                slot++;
            }
            Add(ItemID.GoldCoin, 5);
            Add(ItemID.HealingPotion, 2);
            return true;
        }

        //==================== 坑陷阱场(#6):干坑两型量产带(F31参数化,注水坑归L4) ====================

        internal struct PitReport
        {
            internal int BonePits;
            internal int SpikePits;
            internal int SkippedPits;
        }

        internal static Point PitFieldInteriorSize(UnifiedRandom rand)
            => new(rand.Next(28, 41), 8);

        /// <summary>
        /// 坑陷阱场:通道段+地板干坑。坑竖井穿房壳向下挖(楼梯井穿地板同构),
        /// 井体足印先过ctx.Grid.TryReserve,被跨层预留/别的结构挡住=跳过该坑(fail loud)。
        /// 可读性纪律:骨坑裂砖预告2格、刺坑4格(加倍),预告落在实心地面上,
        /// 踩碎只塌1格=零伤害的"这里有裂砖"第一课;坑口整跨假地板才是真坑。
        /// spikeBudget=全层刺坑余额(出现比≤1/3,ROOMS-L5 §1)。
        /// </summary>
        internal static RoomInfo BuildPitField(RoomNode room, UnifiedRandom rand,
            LayerBuildContext ctx, ref int spikeBudget, ref PitReport pits) {
            var info = new RoomInfo { FloorGapOffset = -1, CeilGapOffset = -1 };
            StampAndCarve(room, L5Palette.WallSlab);
            int floor = room.FloorTop;
            int top = room.InteriorTop;

            //端头骨灯笼:进场就能看清第一段地板(可读性阀门)
            info.Tally.Add(L5Palette.TryPlaceObject(room.InteriorLeft + 2, top, TileID.HangingLanterns,
                L5Palette.LanternBone), "场灯", room.InteriorLeft + 2, top);
            info.Tally.Add(L5Palette.TryPlaceObject(room.InteriorRight - 3, top, TileID.HangingLanterns,
                L5Palette.LanternBone), "场灯", room.InteriorRight - 3, top);

            int count = 2 + (room.InteriorRight - room.InteriorLeft >= 36 && rand.NextBool(2) ? 1 : 0);
            int cursor = room.InteriorLeft + 6 + rand.Next(0, 3);
            for (int i = 0; i < count; i++) {
                int pw = rand.Next(4, 6);
                if (cursor + pw > room.InteriorRight - 6) {
                    break;
                }
                bool spike = spikeBudget > 0 && rand.Next(3) == 0;
                int depth = rand.Next(19, 33);
                //井体+2圈壳的足印预留(§3.2-3;失败=让位跳过,不硬写)
                var shaft = new Rectangle(cursor - 2, room.Bounds.Bottom + DungeonworldMetrics.RoomPadding,
                    pw + 4, depth);
                if (!ctx.Grid.TryReserve(shaft, 0)) {
                    pits.SkippedPits++;
                    CWRMod.Instance.Logger.Warn($"[L5Rooms] 坑井足印被占,跳过 at ({cursor},{floor})");
                    cursor += pw + rand.Next(8, 12);
                    continue;
                }
                if (spike) {
                    spikeBudget--;
                    pits.SpikePits++;
                }
                else {
                    pits.BonePits++;
                }
                CarvePit(cursor, pw, floor, depth, spike, rand, ref info.Tally);
                cursor += pw + rand.Next(8, 12);
            }

            //坑间实心段:骨堆/瓮点缀+尘白;低档飞镖(INDEX §7矩阵L5=低):1/4掷一组
            info.Tally.Add(L5Palette.PlaceSmallBones(room.InteriorLeft + 4, floor - 1, rand),
                "场间骨堆", room.InteriorLeft + 4, floor - 1);
            info.Tally.Add(L5Palette.PlaceUrn(room.InteriorRight - 4, floor - 1, rand),
                "场间瓮", room.InteriorRight - 4, floor - 1);
            L5Palette.DustFloorRun(room.InteriorLeft + 2, floor, 5);
            if (rand.Next(4) == 0) {
                //placeTrap自带镖位合法性扫描(F35;L2教学廊先例),失败静默由计数报告
                bool trapOk = WorldGen.placeTrap(room.InteriorLeft + 4, floor - 1, 0);
                if (trapOk) {
                    info.Tally.Placed++;
                }
            }
            return info;
        }

        /// <summary>
        /// 看样用:在已刻画的坑场内强制落一骨坑一刺坑,把裂砖预告长度差(2 vs 4)并排晒出来。
        /// 正式生成仍走 BuildPitField 随机预算。
        /// </summary>
        internal static void StampShowcasePits(RoomNode room, OccupancyGrid grid,
            UnifiedRandom rand, ref Tally tally) {
            int floor = room.FloorTop;
            int[] xs = [room.InteriorLeft + 6, room.InteriorLeft + 18];
            int[] widths = [4, 5];
            bool[] spikes = [false, true];
            const int depth = 22;
            for (int i = 0; i < 2; i++) {
                int px = xs[i], pw = widths[i];
                if (px + pw > room.InteriorRight - 4) {
                    continue;
                }
                var shaft = new Rectangle(px - 2, room.Bounds.Bottom + DungeonworldMetrics.RoomPadding,
                    pw + 4, depth);
                if (!grid.TryReserve(shaft, 0)) {
                    CWRMod.Instance.Logger.Warn($"[L5Rooms] 看样坑井足印被占 at ({px},{floor})");
                    continue;
                }
                CarvePit(px, pw, floor, depth, spikes[i], rand, ref tally);
            }
        }

        //干坑本体:裂砖假地板口+竖井+两型内衬+回程交错横档(骨坑两壁/刺坑净壁侧)
        private static void CarvePit(int px, int pw, int floor, int depth, bool spike,
            UnifiedRandom rand, ref Tally tally) {
            int bottom = floor + depth;
            //竖井(墙换Tiled:更老的骨窖区);坑底行保持实心作落面
            TileBrush.CarveRect(px, floor + 1, px + pw, bottom, L5Palette.WallTiled);
            //假地板口:整跨裂砖(F31);预告:实心地面上的裂砖,骨坑2格/刺坑4格(加倍)
            for (int x = px; x < px + pw; x++) {
                TileBrush.SetSolid(x, floor, L5Palette.CrackedBrick);
            }
            int cue = spike ? 4 : 2;
            for (int i = 1; i <= cue; i++) {
                TileBrush.SetSolid(px - i, floor, L5Palette.CrackedBrick);
                TileBrush.SetSolid(px + pw - 1 + i, floor, L5Palette.CrackedBrick);
            }
            if (spike) {
                //底面+左壁下3行铺尖刺,内侧交错嵌一圈(RESEARCH §1.2e语法);右壁留净=回程壁
                for (int x = px; x < px + pw; x++) {
                    TileBrush.SetSolid(x, bottom, TileID.Spikes);
                }
                for (int dy = 1; dy <= 3; dy++) {
                    TileBrush.SetSolid(px - 1, bottom - dy, TileID.Spikes);
                }
                for (int x = px + 1; x < px + pw - 1; x += 2) {
                    TileBrush.SetSolid(x, bottom - 1, TileID.Spikes);
                }
                for (int y = bottom - 5; y > floor + 3; y -= 4) {
                    TileBrush.SetPlatform(px + pw - 2, y, L5Palette.PlatformBone);
                    TileBrush.SetPlatform(px + pw - 1, y, L5Palette.PlatformBone);
                }
            }
            else {
                //骨坑:坑底骨堆缓冲(惩罚=绕路+伏击,零直伤)+两壁交错回程横档
                tally.Add(L5Palette.PlaceLargeBones(px + pw / 2, bottom - 1, rand), "坑底骨堆", px + pw / 2, bottom - 1);
                L5Palette.PlaceSmallBones(px + 1, bottom - 1, rand);
                L5Palette.DustFloorRun(px, bottom, pw);
                int zig = 0;
                for (int y = bottom - 4; y > floor + 3; y -= 4) {
                    int sx = zig++ % 2 == 0 ? px : px + pw - 2;
                    TileBrush.SetPlatform(sx, y, L5Palette.PlatformBone);
                    TileBrush.SetPlatform(sx + 1, y, L5Palette.PlatformBone);
                }
            }
        }

        //==================== 深巷骨室(#7节点):无光带的窄小骨室,巷段之间的节点 ====================

        internal static Point BoneCellInteriorSize(UnifiedRandom rand)
            => new(rand.Next(12, 19), rand.Next(7, 9));

        internal static RoomInfo BuildBoneCell(RoomNode room, UnifiedRandom rand) {
            var info = new RoomInfo { FloorGapOffset = DungeonworldMetrics.RoomShellThick + 2, CeilGapOffset = DungeonworldMetrics.RoomShellThick + 2 };
            StampAndCarve(room, L5Palette.WallTiled);
            int floor = room.FloorTop;
            int mid = (room.InteriorLeft + room.InteriorRight) / 2;

            //零灯:无光区二现靠携带光源(INDEX §3裁决,巷口"最后的灯"由内容层挂)
            info.Tally.Add(L5Palette.PlaceLargeBones(mid, floor - 1, rand), "骨室大堆", mid, floor - 1);
            info.Tally.Add(L5Palette.PlaceSmallBones(room.InteriorLeft + 3, floor - 1, rand),
                "骨室骨堆", room.InteriorLeft + 3, floor - 1);
            info.Tally.Add(L5Palette.PlaceUrn(room.InteriorRight - 3, floor - 1, rand),
                "骨室瓮", room.InteriorRight - 3, floor - 1);
            if (mid + 4 < room.InteriorRight - 1) {
                CarveFloorCrypt(mid + 2, floor, rand, ref info.Tally);
            }
            L5Palette.DustWallWash(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.InteriorTop + 2);
            return info;
        }

        //==================== 接口开口公共件 ====================

        /// <summary>
        /// 天花PlatformGap(高位落口):穿壳开洞+洞口盖平台,并向下每4行补横档到地板上2行
        /// (落点缓冲+可回攀,F2;横档只落空气格,不碰家具/夹层平台)
        /// </summary>
        internal static void OpenCeilingGap(RoomNode room, int offset, int width, ushort wall, short frameY) {
            int left = room.Bounds.Left + offset;
            TileBrush.CarveRect(left, room.Bounds.Top, left + width, room.InteriorTop, wall);
            TileBrush.PlatformRow(left, left + width, room.InteriorTop, frameY);
            for (int y = room.InteriorTop + 4; y <= room.FloorTop - 2; y += 4) {
                for (int x = left; x < left + width; x++) {
                    if (WorldGen.InWorld(x, y, 5) && !Main.tile[x, y].HasTile) {
                        TileBrush.SetPlatform(x, y, frameY);
                    }
                }
            }
        }

        /// <summary>
        /// 深巷藏龛:自巷中点向下探地板(≤8行),地板下开3宽3深藏骨穴:
        /// 平台唇+骨灰瓮+骨堆+尘白(黑暗探索的正反馈,ROOMS-L5 §1-7)
        /// </summary>
        internal static bool PocketReward(Point mid, UnifiedRandom rand) {
            for (int i = 0; i <= 8; i++) {
                int y = mid.Y + i;
                if (!WorldGen.InWorld(mid.X + 2, y + 1, 5)) {
                    return false;
                }
                if (Main.tile[mid.X, y].HasTile || !L5Palette.IsSolid(mid.X, y + 1)
                    || !L5Palette.IsSolid(mid.X + 1, y + 1) || !L5Palette.IsSolid(mid.X + 2, y + 1)) {
                    continue;
                }
                int fl = y + 1;
                TileBrush.CarveRect(mid.X, fl, mid.X + 3, fl + 3, L5Palette.WallTiled);
                TileBrush.PlatformRow(mid.X, mid.X + 3, fl, L5Palette.PlatformBone);
                L5Palette.PlaceUrn(mid.X + 1, fl + 2, rand);
                L5Palette.PlaceSmallBones(mid.X + 1, fl + 2, rand);
                L5Palette.DustWallWash(mid.X, fl, mid.X + 3, fl + 3);
                return true;
            }
            return false;
        }
    }
}
