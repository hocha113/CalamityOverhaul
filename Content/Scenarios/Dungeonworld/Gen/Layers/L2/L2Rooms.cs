using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    //L2其余表列房型(ROOMS-L2 §1花名册 #3~#7):
    //囚区长廊(巡逻潜行段首教)/看守室/刑场厅(唯一高房)/档案登记房/教学机关廊(全世界陷阱首现)
    //全部纯算法;写入只走TileBrush+原版放置函数,失败记日志跳过
    internal static class L2Rooms
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
                    CWRMod.Instance.Logger.Warn($"[L2Rooms] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //整包络重盖粉砖+开内膛(M0蓝底换皮),所有房型共用的第一遍
        internal static void StampAndCarve(RoomNode room, ushort wall) {
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L2Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, wall);
        }

        //==================== 囚区长廊(#3):直廊+顶龛躲避位,巡逻潜行段的几何保证 ====================

        //内膛高9=主廊6+顶龛3;龛间距12~18;躲避=攀龛内垂链悬停(死铁链兼掩体,§2.4-⑥壁龛构件换内容)
        internal static Point GalleryInteriorSize(UnifiedRandom rand)
            => new(rand.Next(60, 90), 9);

        internal static Tally BuildGallery(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            int stripTop = floor - 6;   //主廊内膛顶行

            StampAndCarve(room, L2Palette.WallBase);
            //回填顶部3行(龛带的实心底子),主廊净高6
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                for (int y = room.InteriorTop; y < stripTop; y++) {
                    TileBrush.SetSolid(x, y, L2Palette.Brick);
                }
            }

            //顶龛:2宽3高,间距12~18;龛内垂链下探至主廊上部,可攀入悬停
            int nicheX = room.InteriorLeft + rand.Next(6, 10);
            while (nicheX + 2 < room.InteriorRight - 4) {
                TileBrush.CarveRect(nicheX, room.InteriorTop, nicheX + 2, stripTop, L2Palette.WallSlab);
                int links = L2Palette.HangChain(nicheX, room.InteriorTop, 5);
                if (links > 0) {
                    L2Palette.RustStreak(nicheX + 1, room.InteriorTop + 1, 3);
                }
                nicheX += rand.Next(12, 19);
            }

            //链灯笼节拍(标档全亮)+纹章旗节点
            for (int x = room.InteriorLeft + 5; x < room.InteriorRight - 3; x += rand.Next(10, 15)) {
                tally.Add(L2Palette.TryPlaceObject(x, stripTop, TileID.HangingLanterns,
                    L2Palette.LanternChainStyle), "链灯笼", x, stripTop);
                if (rand.NextBool(2)) {
                    int bx = x + 3;
                    tally.Add(L2Palette.TryPlaceTile(bx, stripTop, TileID.Banners,
                        L2Palette.BannerStyleBase + rand.Next(2)), "纹章旗", bx, stripTop);
                }
            }
            return tally;
        }

        //==================== 看守室(#4):桌椅告示与"钥匙"意象,长廊的战术节点 ====================

        internal static Point GuardInteriorSize(UnifiedRandom rand)
            => new(rand.Next(10, 14), rand.Next(4, 6));

        internal static Tally BuildGuard(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L2Palette.WallBase);

            int mid = (room.InteriorLeft + room.InteriorRight) / 2;
            //桌+双椅(粉系样式对源:桌14/12,椅15/15;右椅镜像走vanilla同款frameX偏移交给PlaceTile样式)
            tally.Add(L2Palette.TryPlaceTile(mid, floor - 1, TileID.Tables, L2Palette.TableStyle),
                "地牢桌", mid, floor - 1);
            tally.Add(L2Palette.TryPlaceTile(mid - 2, floor - 1, TileID.Chairs, L2Palette.ChairStyle),
                "地牢椅", mid - 2, floor - 1);
            tally.Add(L2Palette.TryPlaceTile(mid + 2, floor - 1, TileID.Chairs, L2Palette.ChairStyle),
                "地牢椅", mid + 2, floor - 1);
            //桌面蜡烛(暖烛族,vanilla家具模板同款:桌基上两行)
            tally.Add(L2Palette.TryPlaceTile(mid, floor - 3, TileID.Candles, L2Palette.CandleStyle),
                "蜡烛", mid, floor - 3);
            //罪状/规章告示
            tally.Add(PlaceSignChecked(room.InteriorLeft + 1, floor - 1), "告示牌", room.InteriorLeft + 1, floor - 1);
            //储物桶+笼灯点缀(笼灯只作看守室点缀,主光族仍是链灯笼);灯偏离桌面蜡烛列防抢位
            tally.Add(WorldGen.PlaceChest(room.InteriorRight - 2, floor - 1, TileID.Containers,
                notNearOtherChests: false, L2Palette.ChestBarrelStyle) >= 0,
                "木桶", room.InteriorRight - 2, floor - 1);
            tally.Add(L2Palette.TryPlaceObject(mid - 3, room.InteriorTop, TileID.HangingLanterns,
                L2Palette.LanternCagedStyle), "笼灯", mid - 3, room.InteriorTop);
            //纹章旗3高,净高5起挂(净高4时地面家具带与旗尾抢位)
            if (floor - room.InteriorTop >= 5) {
                tally.Add(L2Palette.TryPlaceTile(room.InteriorLeft + 3, room.InteriorTop, TileID.Banners,
                    L2Palette.BannerStyleBase + rand.Next(2)), "纹章旗", room.InteriorLeft + 3, room.InteriorTop);
            }
            return tally;
        }

        //==================== 刑场厅(#5):本层唯一高房,呼吸段;顶垂链阵+处刑台 ====================

        internal static Point HallInteriorSize(UnifiedRandom rand)
            => new(rand.Next(30, 41), rand.Next(16, 23));

        internal static Tally BuildHall(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L2Palette.WallBase);

            int mid = (room.InteriorLeft + room.InteriorRight) / 2;
            //处刑台:8宽两级收分(边1高中2高,1格台阶自动登F3)
            int daisHalf = 4;
            for (int x = mid - daisHalf; x <= mid + daisHalf; x++) {
                int h = x == mid - daisHalf || x == mid + daisHalf ? 1 : 2;
                for (int dy = 1; dy <= h; dy++) {
                    TileBrush.SetSolid(x, floor - dy, L2Palette.Brick);
                }
            }
            //台后板岩墙深色带(旧血迹深色区集中在刑场厅周边,ROOMS-L2 §2.4)
            for (int x = mid - daisHalf - 2; x <= mid + daisHalf + 2; x++) {
                for (int y = floor - 12; y < floor; y++) {
                    if (WorldGen.InWorld(x, y) && !Main.tile[x, y].HasTile) {
                        Main.tile[x, y].WallType = L2Palette.WallSlab;
                    }
                }
            }

            //链灯笼先挂(占位优先),垂链阵随后见缝落锚,避免互斥拒绝噪声
            for (int x = room.InteriorLeft + 4; x < room.InteriorRight - 3; x += rand.Next(9, 13)) {
                tally.Add(L2Palette.TryPlaceObject(x, room.InteriorTop, TileID.HangingLanterns,
                    L2Palette.LanternChainStyle), "链灯笼", x, room.InteriorTop);
            }

            //顶垂链阵:全形态主导权的主秀场;台正上双链垂到台上方3格(绞架读法,死铁静止)
            for (int x = room.InteriorLeft + 2; x < room.InteriorRight - 2; x += rand.Next(3, 6)) {
                bool overDais = x >= mid - 2 && x <= mid + 2;
                int len = overDais
                    ? floor - 2 - 3 - room.InteriorTop
                    : rand.Next(4, 11);
                int links = L2Palette.HangChain(x, room.InteriorTop, len);
                if (links > 2 && rand.NextBool(2)) {
                    L2Palette.RustStreak(x, room.InteriorTop + links, rand.Next(2, 5));
                }
            }

            //纹章旗+台侧烛台;台脚一撮骨杂物(行刑余绪,限地面杂物形态)
            tally.Add(L2Palette.TryPlaceTile(mid - daisHalf - 3, floor - 1, TileID.Candelabras,
                L2Palette.CandelabraStyle), "烛台", mid - daisHalf - 3, floor - 1);
            tally.Add(L2Palette.TryPlaceTile(mid - daisHalf - 5, room.InteriorTop, TileID.Banners,
                L2Palette.BannerStyleBase), "纹章旗", mid - daisHalf - 5, room.InteriorTop);
            tally.Add(L2Palette.TryPlaceTile(mid + daisHalf + 5, room.InteriorTop, TileID.Banners,
                L2Palette.BannerStyleBase + 1), "纹章旗", mid + daisHalf + 5, room.InteriorTop);
            tally.Add(WorldGen.PlaceSmallPile(mid + daisHalf + 2, floor - 1,
                WorldGen.genRand.Next(L2Palette.SmallBone1x1Min, L2Palette.SmallBone1x1Max), 0),
                "骨杂物", mid + daisHalf + 2, floor - 1);
            return tally;
        }

        //==================== 档案登记房(#6):罪档登记处,向L3的叙事预告(书写母题单点豁免) ====================

        internal static Point RegistryInteriorSize(UnifiedRandom rand)
            => new(rand.Next(10, 15), rand.Next(5, 7));

        internal static Tally BuildRegistry(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int floor = room.FloorTop;
            StampAndCarve(room, L2Palette.WallBase);

            int left = room.InteriorLeft;
            //架位背景带先换板岩墙(书架精灵有镂空,先墙后架)再落书架单座(INDEX §3登记房豁免)
            for (int x = left + 1; x <= left + 3; x++) {
                for (int y = floor - 4; y < floor; y++) {
                    if (!Main.tile[x, y].HasTile) {
                        Main.tile[x, y].WallType = L2Palette.WallSlab;
                    }
                }
            }
            tally.Add(L2Palette.TryPlaceTile(left + 2, floor - 1, TileID.Bookcases, L2Palette.BookcaseStyle),
                "书架", left + 2, floor - 1);
            //登记台:桌+椅+桌面散书与蜡烛(vanilla家具模板的桌面小物语法)
            int deskX = left + 6;
            tally.Add(L2Palette.TryPlaceTile(deskX, floor - 1, TileID.Tables, L2Palette.TableStyle),
                "登记桌", deskX, floor - 1);
            tally.Add(L2Palette.TryPlaceTile(deskX - 2, floor - 1, TileID.Chairs, L2Palette.ChairStyle),
                "登记椅", deskX - 2, floor - 1);
            tally.Add(L2Palette.TryPlaceTile(deskX - 1, floor - 3, TileID.Books, 0), "散书", deskX - 1, floor - 3);
            tally.Add(L2Palette.TryPlaceTile(deskX + 1, floor - 3, TileID.Candles, L2Palette.CandleStyle),
                "蜡烛", deskX + 1, floor - 3);
            //罪档告示+笼灯
            tally.Add(PlaceSignChecked(room.InteriorRight - 2, floor - 1), "告示牌", room.InteriorRight - 2, floor - 1);
            tally.Add(L2Palette.TryPlaceObject(deskX, room.InteriorTop, TileID.HangingLanterns,
                L2Palette.LanternChainStyle), "链灯笼", deskX, room.InteriorTop);
            return tally;
        }

        //==================== 教学机关廊(#7):全世界陷阱首现,双层软着陆结构 ====================
        //
        //剖面(上通道=机关道,下厅=贯通路径兼缓冲室,§2.4-⑥"内部空间不接图边"):
        //   [梯口][行走段][裂砖预告1格][压力板+飞镖(箭垛柱)][裂砖段6~8]→[奖励龛]
        //   ────────中层地板(机关道地板,裂砖段即在此行)────────
        //   [缓冲下厅5高:骨堆软着陆+安慰奖罐;两端Door接排链]
        //踩裂即坠下厅(高差5~6,无尖刺,骨堆视觉);上道走完=奖励;软着陆条款闭环

        internal const int TrapUpperH = 5;   //上机关道净高
        internal const int TrapLowerH = 5;   //下厅净高

        internal static Point TrapCorridorInteriorSize(UnifiedRandom rand)
            => new(rand.Next(32, 37), TrapUpperH + 1 + TrapLowerH);

        internal static Tally BuildTrapCorridor(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            int lowerFloor = room.FloorTop;              //下厅地板=对外接驳地板
            int midFloor = lowerFloor - TrapLowerH - 1;  //中层地板行(上道立足)
            int upperTop = midFloor - TrapUpperH;        //上道内膛顶行

            StampAndCarve(room, L2Palette.WallBase);
            //重立中层地板(整行),再按分段开裂/开梯
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                TileBrush.SetSolid(x, midFloor, L2Palette.Brick);
            }

            int left = room.InteriorLeft;
            int right = room.InteriorRight;

            //--梯口:左端2宽,中层开洞盖平台+半程平台,下厅↔上道可往返(回廊台阶条款)
            int ladderX = left;
            TileBrush.CarveRect(ladderX, midFloor, ladderX + 2, midFloor + 1, L2Palette.WallBase);
            TileBrush.PlatformRow(ladderX, ladderX + 2, midFloor, L2Palette.PlatformFrameY);
            TileBrush.PlatformRow(ladderX, ladderX + 2, midFloor + 3, L2Palette.PlatformFrameY);

            //--分段坐标(箭垛柱距压板须>5:placeTrap镖位合法距离(5,50),对源L5598)
            int cueX = ladderX + rand.Next(5, 7);        //裂砖预告(单格,踩碎即坠=第一课)
            int plateX = cueX + 3;                       //压力板
            int pierX = plateX + rand.Next(6, 8);        //箭垛柱(2高,飞镖的家)
            int crackL = pierX + 2;                      //裂砖段左缘
            int crackR = System.Math.Min(crackL + rand.Next(6, 9), right - 5);
            int prizeX = right - 2;                      //奖励龛

            //裂砖预告与裂砖段(原版"裂=危险"语言,F31)
            TileBrush.SetSolid(cueX, midFloor, L2Palette.CrackedBrick);
            for (int x = crackL; x < crackR; x++) {
                TileBrush.SetSolid(x, midFloor, L2Palette.CrackedBrick);
            }
            //箭垛柱:2高实心,vanilla placeTrap的扫描停靠点
            TileBrush.SetSolid(pierX, midFloor - 1, L2Palette.Brick);
            TileBrush.SetSolid(pierX, midFloor - 2, L2Palette.Brick);

            //压力板+飞镖:复用placeTrap(F35;type0=飞镖+灰压板)
            //扫描行=板上1~3行:右向撞柱(2/3),左向被梯口平台放行后撞左壳——两侧皆合法镖位
            bool trapOk = false;
            for (int attempt = 0; attempt < 6 && !trapOk; attempt++) {
                trapOk = WorldGen.placeTrap(plateX + attempt % 2, midFloor - 1, 0);
            }
            if (!trapOk) {
                CWRMod.Instance.Logger.Warn($"[L2Rooms] 教学飞镖placeTrap六次未成 at ({plateX},{midFloor - 1})");
            }
            else {
                tally.Placed++;
            }

            //奖励龛:走完机关道的战利品(内容归M4,先落容器)
            tally.Add(WorldGen.PlaceChest(prizeX - 1, midFloor - 1, TileID.Containers,
                notNearOtherChests: false, L2Palette.ChestBarrelStyle) >= 0,
                "奖励桶", prizeX - 1, midFloor - 1);

            //--下厅:骨堆软着陆视觉+安慰奖罐+灯
            for (int i = 0; i < 3; i++) {
                int bx = rand.Next(crackL, System.Math.Max(crackL + 1, crackR));
                tally.Add(WorldGen.PlaceSmallPile(bx, lowerFloor - 1,
                    rand.Next(L2Palette.SmallBone2x1Min, L2Palette.SmallBone2x1Max), 1),
                    "软着陆骨堆", bx, lowerFloor - 1);
            }
            tally.Add(WorldGen.PlacePot(left + 4, lowerFloor - 1, TileID.Pots,
                rand.Next(L2Palette.PotStyleMin, L2Palette.PotStyleMax)),
                "安慰奖罐", left + 4, lowerFloor - 1);
            tally.Add(WorldGen.PlacePot(right - 6, lowerFloor - 1, TileID.Pots,
                rand.Next(L2Palette.PotStyleMin, L2Palette.PotStyleMax)),
                "安慰奖罐", right - 6, lowerFloor - 1);
            //下厅灯挂在梯口侧实心段(裂砖段下方悬灯会随裂砖破碎掉锚)
            tally.Add(L2Palette.TryPlaceObject(ladderX + 3, midFloor + 1, TileID.HangingLanterns,
                L2Palette.LanternChainStyle), "链灯笼", ladderX + 3, midFloor + 1);

            //--警示:梯口告示(教学机关廊入口警示,ROOMS-L2 §2.1)
            tally.Add(PlaceSignChecked(ladderX + 3, lowerFloor - 1), "警示牌", ladderX + 3, lowerFloor - 1);
            //上道灯:板前一盏照亮机关段
            tally.Add(L2Palette.TryPlaceObject(cueX - 2, upperTop, TileID.HangingLanterns,
                L2Palette.LanternChainStyle), "链灯笼", cueX - 2, upperTop);
            return tally;
        }

        //告示牌放置校验(PlaceSign签名对源WorldGen.cs L35944);
        //内部锚定可能微调落位,按2x2邻域验收
        private static bool PlaceSignChecked(int x, int y) {
            WorldGen.PlaceSign(x, y, TileID.Signs);
            for (int dx = 0; dx <= 1; dx++) {
                for (int dy = -1; dy <= 0; dy++) {
                    Tile t = Main.tile[x + dx, y + dy];
                    if (t.HasTile && t.TileType == TileID.Signs) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
