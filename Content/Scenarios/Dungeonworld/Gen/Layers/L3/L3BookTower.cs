using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //====================================================================
    //书塔(ROOMS-L3 §1-#3,纯算法):消化纵深的竖向书库,兼层内垂直捷径——
    //
    //2D落地语法(正视图,自上而下一条走读线):
    //  [上层检索廊]═楼梯井(对齐塔芯光井)═
    //  [壳][书架|光井3宽|书架][壳]   ←实心甲板每6行一层,光井处开平台缺口
    //  [    甲板k+1(灯笼吊光井侧)    ]  ←甲板间光井内吊半程平台(竖距3,F2)
    //  [壳][书架|光井|奖励龛][壳]
    //  [    塔底地板→落口井→本层检索廊 ]
    //·光井=3宽通高竖槽,塔的攀爬轴;甲板在光井处只盖平台(下落按S,上行蹬跳);
    //·灯笼自甲板底吊在光井侧,2/3熄灭+开关在下一层甲板(灭灯玩法在塔内成串,F33);
    //·书龛节拍:每2~3层甲板一处端头奖励位(壁龛2x3换皮=书龛,INDEX §4);
    //·塔顶甲板行与塔芯x外露给L3Content,上层检索廊的楼梯井沿光井轴直落塔顶。
    //====================================================================
    internal static class L3BookTower
    {
        internal struct TowerPlan
        {
            internal int Width;    //内膛净宽 12~14
            internal int Height;   //内膛净高 38~50(条带上限内)
        }

        internal struct TowerReport
        {
            internal int Decks;
            internal int ShelvesPlaced;
            internal int ShelvesRejected;
            internal int Rewards;
            internal int WellLeft;     //光井左缘(世界坐标),上接楼梯井对齐用
            internal int TopDeckRow;   //塔顶甲板行(实心/平台所在行)
        }

        /// <summary>掷塔计划;maxInteriorH=条带净高上限,不足38返回false(该甲板弃塔)</summary>
        internal static bool TryRoll(UnifiedRandom rand, int maxInteriorH, out TowerPlan plan) {
            plan = default;
            if (maxInteriorH < 38) {
                return false;
            }
            plan.Width = rand.Next(12, 15);
            plan.Height = System.Math.Min(maxInteriorH, rand.Next(40, 51));
            return true;
        }

        internal static Point InteriorSize(TowerPlan plan) => new(plan.Width, plan.Height);

        internal static TowerReport Build(RoomNode room, TowerPlan plan, UnifiedRandom rand) {
            var report = new TowerReport();
            int floor = room.FloorTop;
            int top = floor - plan.Height;
            int wellLeft = (room.InteriorLeft + room.InteriorRight) / 2 - 1;
            report.WellLeft = wellLeft;

            //整包络重盖+开内膛
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L3Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, top, room.InteriorRight, floor, L3Palette.WallBase);

            //甲板:自地板向上每6行一层实心板,光井3宽处换平台(缺口即攀爬轴)
            var deckRows = new System.Collections.Generic.List<int>();
            for (int deckRow = floor - 6; deckRow >= top + 5; deckRow -= 6) {
                for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                    if (x >= wellLeft && x < wellLeft + 3) {
                        continue;
                    }
                    TileBrush.SetSolid(x, deckRow,
                        rand.Next(100) < 5 ? L3Palette.CrackedBrick : L3Palette.Brick);
                }
                TileBrush.PlatformRow(wellLeft, wellLeft + 3, deckRow, L3Palette.PlatformFrameY);
                //甲板间光井半程平台,攀爬竖距3(F2余量充足)
                TileBrush.PlatformRow(wellLeft, wellLeft + 3, deckRow + 3, L3Palette.PlatformFrameY);
                deckRows.Add(deckRow);
            }
            //塔底与首层甲板间的半程平台
            if (deckRows.Count > 0) {
                TileBrush.PlatformRow(wellLeft, wellLeft + 3, floor - 3, L3Palette.PlatformFrameY);
            }
            report.Decks = deckRows.Count;
            report.TopDeckRow = deckRows.Count > 0 ? deckRows[^1] : floor;

            //==================== 装修:书架/灯串/书龛 ====================

            //甲板书架:光井两侧各一列(侧段宽4~6恰容3宽架+过身位)
            int leftShelfX = room.InteriorLeft + 1;
            int rightShelfX = room.InteriorRight - 4;
            int nookBeat = rand.Next(2, 4);
            bool chestUsed = false;
            for (int k = 0; k < deckRows.Count; k++) {
                int standRow = deckRows[k] - 1;
                bool nookDeck = (k + 1) % nookBeat == 0;

                //左侧书架
                if (L3Palette.TryPlaceTile(leftShelfX + 1, standRow, TileID.Bookcases, L3Palette.StyleBookcase)) {
                    report.ShelvesPlaced++;
                }
                else {
                    report.ShelvesRejected++;
                }
                //右侧:书龛节拍甲板改奖励位,其余书架
                if (nookDeck) {
                    bool ok;
                    if (!chestUsed && k >= deckRows.Count / 2) {
                        //塔身中上部藏一只木箱(书龛藏支线奖励,ROOMS-L3 §1)
                        ok = L3Palette.PlaceChestWithLoot(rightShelfX + 1, standRow, gold: false);
                        chestUsed |= ok;
                    }
                    else {
                        ok = WorldGen.PlacePot(rightShelfX + 1, standRow, TileID.Pots,
                            rand.Next(L3Palette.PotStyleMin, L3Palette.PotStyleMax + 1));
                        L3Palette.PlaceBook(rightShelfX + 3, standRow, rand);
                    }
                    if (ok) {
                        report.Rewards++;
                    }
                }
                else if (L3Palette.TryPlaceTile(rightShelfX + 1, standRow, TileID.Bookcases, L3Palette.StyleBookcase)) {
                    report.ShelvesPlaced++;
                }
                else {
                    report.ShelvesRejected++;
                }

                //灯笼吊在甲板底的光井侧列,2/3熄灭+开关藏下一层甲板(灭灯串,F33)
                int lampX = rand.NextBool() ? wellLeft - 1 : wellLeft + 3;
                if (L3Lights.PlaceLantern(lampX, deckRows[k] + 1, caged: false)) {
                    int switchRow = k > 0 ? deckRows[k - 1] - 1 : floor - 1;
                    int switchX = lampX <= wellLeft ? room.InteriorLeft : room.InteriorRight - 1;
                    if (L3Lights.TryPlaceSwitch(switchX, switchRow)) {
                        L3Lights.WireStaircase(switchX, switchRow, lampX, deckRows[k] + 1);
                        if (rand.Next(3) > 0) {
                            L3Lights.ExtinguishLantern(lampX, deckRows[k] + 1);
                            L3Lights.LampsOff++;
                        }
                        else {
                            L3Lights.LampsLit++;
                        }
                    }
                    else {
                        L3Lights.LampsLit++;
                    }
                }
            }

            //塔底地面:散书+墨瓶(阅读者掉落的痕迹)
            L3Palette.PlaceBook(room.InteriorLeft + 1, floor - 1, rand);
            L3Palette.PlaceInkBottle(room.InteriorRight - 2, floor - 1, rand);

            //墨霉做旧
            L3Palette.MoldUnderShelves(room.Bounds, rand);
            return report;
        }
    }
}
