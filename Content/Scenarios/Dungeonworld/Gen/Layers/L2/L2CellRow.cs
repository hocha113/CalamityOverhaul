using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    //囚室排 archetype(STRUCTURES §2.4-②,纯算法压测件)
    //
    //2D落地语法(侧视图,一条走读线):
    //  [壳][门厅][隔墙2][囚室]...[隔墙2][囚室][隔墙2][门厅|尾段][壳]
    //每道隔墙开一个"口",走读=连续开门穿室,"门后有东西"教学的节拍器;
    //排内地板齐平禁高差;尽端排(约1/3)以拷问室或牢栅藏物室收尾。
    //
    //口的形态(比例语法,决定论掷点):
    //  Door   真门板(粉门style18)：主体,约2/3
    //  Broken 破栅口(3高全开,顶格残留铁栏墙+锈痕)：约1/3
    //  Barred 牢栅封死(上部1宽透视缝+铁栏墙,下部裂粉砖暗塞):
    //         "看得见拿不到",答案=原版"裂砖可破"语言,只用于尾段藏物室
    internal static class L2CellRow
    {
        //==================== 计划参数(掷定后尺寸即冻结,供预留) ====================

        internal enum TailKind { None, Torture, Showcase }

        internal struct RowPlan
        {
            internal int CellCount;     //普通囚室数 4~8
            internal int CellWidth;     //单室净宽 6~9
            internal int RowHeight;     //排内净高 5~7(藏物尾段强制≥6)
            internal TailKind Tail;     //尾段形态,None=两端贯通
        }

        internal const int VestWidth = 4;       //门厅净宽(容楼梯井3宽落口)
        internal const int PartThick = 2;       //隔墙厚(§2.4-②)

        /// <summary>掷一份排计划;allowTail=允许生成尽端排(拷问/藏物)</summary>
        internal static RowPlan Roll(UnifiedRandom rand, bool allowTail) {
            var plan = new RowPlan {
                CellCount = rand.Next(4, 9),
                CellWidth = rand.Next(6, 10),
                RowHeight = rand.Next(5, 8),
                Tail = TailKind.None,
            };
            if (allowTail && rand.NextBool(3)) {
                plan.Tail = rand.NextBool(3) ? TailKind.Showcase : TailKind.Torture;
            }
            if (plan.Tail == TailKind.Showcase && plan.RowHeight < 6) {
                //透视缝(2)+实梁(1)+裂塞(3)竖向共需6行
                plan.RowHeight = 6;
            }
            return plan;
        }

        /// <summary>计划的内膛净尺寸(不含壳),TryPlace预留用</summary>
        internal static Point InteriorSize(RowPlan plan) {
            int width = VestWidth + plan.CellCount * (PartThick + plan.CellWidth);
            width += plan.Tail switch {
                //拷问室=两间合并(§2.4-②"特殊两间"),含入口隔墙
                TailKind.Torture => PartThick + plan.CellWidth * 2 + 2,
                TailKind.Showcase => PartThick + plan.CellWidth,
                //贯通排:右侧再接一道隔墙+门厅
                _ => PartThick + VestWidth,
            };
            return new Point(width, plan.RowHeight);
        }

        internal struct RowReport
        {
            internal int Cells;
            internal int DoorsPlaced;
            internal int DoorsFailed;
            internal int FurniturePlaced;
            internal int FurnitureRejected;
        }

        //==================== 构建(房间已预留;stamp→隔墙→口→装修,几何一遍冻结) ====================

        internal static RowReport Build(RoomNode room, RowPlan plan, UnifiedRandom rand) {
            var report = new RowReport();
            int floor = room.FloorTop;              //地板首行(实心)
            int ceilTop = floor - plan.RowHeight;   //内膛顶行

            //整包络重盖粉砖(M0蓝底换皮+清预览残余),再开内膛
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L2Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, ceilTop, room.InteriorRight, floor, L2Palette.WallBase);

            //隔墙列表:cursor自左门厅右缘起,每段=隔墙+室
            int cursor = room.InteriorLeft + VestWidth;
            int segments = plan.CellCount + (plan.Tail == TailKind.None ? 0 : 1);
            var mouths = new System.Collections.Generic.List<(int x, MouthKind kind)>();

            for (int i = 0; i < segments; i++) {
                bool isTailPart = plan.Tail != TailKind.None && i == segments - 1;
                //重立隔墙2厚
                for (int dx = 0; dx < PartThick; dx++) {
                    for (int y = ceilTop; y < floor; y++) {
                        TileBrush.SetSolid(cursor + dx, y, L2Palette.Brick);
                    }
                }
                MouthKind mouth = isTailPart
                    ? plan.Tail == TailKind.Showcase ? MouthKind.Barred : MouthKind.Door
                    : rand.NextBool(3) ? MouthKind.Broken : MouthKind.Door;
                mouths.Add((cursor, mouth));
                cursor += PartThick;
                cursor += isTailPart
                    ? plan.Tail == TailKind.Torture ? plan.CellWidth * 2 + 2 : plan.CellWidth
                    : plan.CellWidth;
            }
            //贯通排:尾门厅前再立一道隔墙(带门)
            if (plan.Tail == TailKind.None) {
                for (int dx = 0; dx < PartThick; dx++) {
                    for (int y = ceilTop; y < floor; y++) {
                        TileBrush.SetSolid(cursor + dx, y, L2Palette.Brick);
                    }
                }
                mouths.Add((cursor, MouthKind.Door));
            }

            //开口(几何段)
            foreach ((int x, MouthKind kind) in mouths) {
                CarveMouth(x, floor, ceilTop, kind);
            }

            //装修段:几何已冻结,门板/家具统一合法锚定
            foreach ((int x, MouthKind kind) in mouths) {
                if (kind != MouthKind.Door) {
                    continue;
                }
                //门板放前列,后列留作门框净空(DoorAudit两侧可站/一侧可开)
                if (WorldGen.PlaceDoor(x, floor - 2, TileID.ClosedDoor, L2Palette.DoorStyle)) {
                    report.DoorsPlaced++;
                }
                else {
                    report.DoorsFailed++;
                    CWRMod.Instance.Logger.Warn($"[L2CellRow] 门板放置失败 at ({x},{floor - 2})");
                }
            }

            FurnishCells(room, plan, rand, mouths, ref report);
            report.Cells = plan.CellCount;
            return report;
        }

        internal enum MouthKind { Door, Broken, Barred }

        //口的三种形态;口高固定3、底沿与地板齐平(§2.5接缝规则)
        private static void CarveMouth(int left, int floor, int ceilTop, MouthKind kind) {
            switch (kind) {
                case MouthKind.Door:
                case MouthKind.Broken:
                    TileBrush.CarveRect(left, floor - 3, left + PartThick, floor, L2Palette.WallBase);
                    if (kind == MouthKind.Broken) {
                        //顶格残留铁栏墙=被扯断的栅,锈痕自栅根垂下
                        for (int dx = 0; dx < PartThick; dx++) {
                            Main.tile[left + dx, floor - 3].WallType = L2Palette.WallFence;
                            L2Palette.RustStreak(left + dx, floor - 2, 2);
                        }
                    }
                    break;
                case MouthKind.Barred:
                    //上部透视缝(2高,1宽不可通行)+铁栏墙;下部3高裂粉砖暗塞(原版"裂=可破"语言)
                    for (int dx = 0; dx < PartThick; dx++) {
                        int x = left + dx;
                        TileBrush.CarveRect(x, floor - 5, x + 1, floor - 3, L2Palette.WallFence);
                        for (int y = floor - 3; y < floor; y++) {
                            TileBrush.SetSolid(x, y, L2Palette.CrackedBrick);
                        }
                        //锈染在透视缝的铁栏墙上(实心塞后的墙不可见)
                        L2Palette.RustStreak(x, floor - 5, 2);
                    }
                    break;
            }
        }

        //==================== 室内装修(比例语法:空/骨/干草/藏物,留白为主) ====================

        private enum CellKind { Empty, Bones, Hay, Reward }

        private static void FurnishCells(RoomNode room, RowPlan plan, UnifiedRandom rand,
            System.Collections.Generic.List<(int x, MouthKind kind)> mouths, ref RowReport report) {
            int floor = room.FloorTop;
            int ceilTop = floor - plan.RowHeight;
            bool rewardUsed = false;

            for (int i = 0; i < plan.CellCount; i++) {
                int cellLeft = mouths[i].x + PartThick;
                int cellRight = cellLeft + plan.CellWidth;   //半开
                int mid = (cellLeft + cellRight) / 2;

                //链灯笼节拍:每2室一盏,挂内膛顶行
                if (i % 2 == 0) {
                    Place(L2Palette.TryPlaceObject(mid, ceilTop, TileID.HangingLanterns,
                        L2Palette.LanternChainStyle), "链灯笼", mid, ceilTop, ref report);
                }

                //铐挂读法:约1/3囚室天花垂短链+锈渍(死铁,静止,不发光)
                if (rand.NextBool(3)) {
                    int cx = rand.Next(cellLeft + 1, cellRight - 1);
                    int links = L2Palette.HangChain(cx, ceilTop, rand.Next(2, 4));
                    if (links > 0) {
                        L2Palette.RustStreak(cx, ceilTop + links, rand.Next(2, 4));
                    }
                }

                CellKind kind = RollCell(rand, ref rewardUsed);
                switch (kind) {
                    case CellKind.Bones:
                        //骨堆≤2件/室(INDEX §3限定形态)
                        int piles = rand.Next(1, 3);
                        for (int p = 0; p < piles; p++) {
                            int bx = rand.Next(cellLeft + 1, cellRight - 1);
                            Place(WorldGen.PlaceSmallPile(bx, floor - 1,
                                rand.Next(L2Palette.SmallBone2x1Min, L2Palette.SmallBone2x1Max), 1),
                                "骨堆", bx, floor - 1, ref report);
                        }
                        break;
                    case CellKind.Hay:
                        //干草铺:每室至多1处2~3格"床位"
                        int hayW = rand.Next(2, 4);
                        int hayL = rand.NextBool() ? cellLeft + 1 : cellRight - 1 - hayW;
                        for (int dx = 0; dx < hayW; dx++) {
                            TileBrush.SetSolid(hayL + dx, floor - 1, TileID.HayBlock);
                        }
                        report.FurniturePlaced++;
                        break;
                    case CellKind.Reward:
                        //门后奖励(§2.4-②教学;箱内战利品归M4轮换表)
                        Place(WorldGen.PlaceChest(mid, floor - 1, TileID.Containers,
                            notNearOtherChests: false, L2Palette.ChestBarrelStyle) >= 0,
                            "木桶", mid, floor - 1, ref report);
                        Place(WorldGen.PlacePot(cellLeft + 1, floor - 1,
                            TileID.Pots, rand.Next(L2Palette.PotStyleMin, L2Palette.PotStyleMax)),
                            "罐", cellLeft + 1, floor - 1, ref report);
                        break;
                    default:
                        //留白室:偶发单格裂砖地面(墙皮剥落做旧,F12计入群系)
                        if (rand.NextBool(3)) {
                            TileBrush.SetSolid(rand.Next(cellLeft, cellRight), floor, L2Palette.CrackedBrick);
                        }
                        break;
                }
            }

            //尾段装修
            if (plan.Tail == TailKind.Torture) {
                FurnishTortureTail(mouths[^1].x + PartThick, plan.CellWidth * 2 + 2,
                    floor, ceilTop, rand, ref report);
            }
            else if (plan.Tail == TailKind.Showcase) {
                FurnishShowcaseTail(mouths[^1].x + PartThick, plan.CellWidth,
                    floor, rand, ref report);
            }
        }

        private static CellKind RollCell(UnifiedRandom rand, ref bool rewardUsed) {
            //权重:空40%/骨25%/干草20%/藏物15%(每排至多1间奖励,与敌人间不相邻的语义归运行时)
            int roll = rand.Next(100);
            if (roll < 40) {
                return CellKind.Empty;
            }
            if (roll < 65) {
                return CellKind.Bones;
            }
            if (roll < 85) {
                return CellKind.Hay;
            }
            if (rewardUsed) {
                return CellKind.Bones;
            }
            rewardUsed = true;
            return CellKind.Reward;
        }

        //拷问室:尖刺叙事单点(INDEX §3豁免)+刑架fallback(尖刺+静态链+地牢桌)+金箱槽(F35)
        private static void FurnishTortureTail(int left, int width, int floor, int ceilTop,
            UnifiedRandom rand, ref RowReport report) {
            int right = left + width;
            //背景带换板岩墙(旧血迹般的深色区,ROOMS-L2 §2.4)
            for (int x = left + width / 2; x < right; x++) {
                for (int y = ceilTop; y < floor; y++) {
                    if (!Main.tile[x, y].HasTile) {
                        Main.tile[x, y].WallType = L2Palette.WallSlab;
                    }
                }
            }
            //布局自左向右:刑桌(烛台上桌)|锁金箱|刑架区(双垂链+地板换刺),互不抢位
            int tableX = left + 2;
            Place(L2Palette.TryPlaceTile(tableX, floor - 1, TileID.Tables, L2Palette.TableStyle),
                "刑桌", tableX, floor - 1, ref report);
            Place(L2Palette.TryPlaceTile(tableX, floor - 3, TileID.Candelabras, L2Palette.CandelabraStyle),
                "桌面烛台", tableX, floor - 3, ref report);
            //金箱槽:房间箱轮换(F35),内容归M4
            int chestX = left + 5;
            Place(WorldGen.PlaceChest(chestX, floor - 1, TileID.Containers,
                notNearOtherChests: false, L2Palette.ChestLockedGoldStyle) >= 0,
                "锁金箱", chestX, floor - 1, ref report);
            //刑架:右四分位,双垂链+链下尖刺(尖刺仅此单点,INDEX §3豁免)
            int rackX = right - 4;
            for (int c = 0; c < 2; c++) {
                int cx = rackX - 1 + c * 2;
                int links = L2Palette.HangChain(cx, ceilTop, rand.Next(2, 4));
                if (links > 0) {
                    L2Palette.RustStreak(cx, ceilTop + links, 3);
                }
            }
            for (int x = rackX - 2; x <= rackX + 2 && x < right; x++) {
                TileBrush.SetSolid(x, floor, TileID.Spikes);
            }
            Place(L2Palette.TryPlaceObject(left + 1, ceilTop, TileID.HangingLanterns,
                L2Palette.LanternChainStyle), "链灯笼", left + 1, ceilTop, ref report);
        }

        //牢栅藏物室:透视缝里看得见的奖励,进路=裂砖暗塞(彩蛋钩子,ROOMS-L2 §3)
        private static void FurnishShowcaseTail(int left, int width, int floor,
            UnifiedRandom rand, ref RowReport report) {
            int mid = left + width / 2;
            Place(WorldGen.PlaceChest(mid, floor - 1, TileID.Containers,
                notNearOtherChests: false, L2Palette.ChestLockedGoldStyle) >= 0,
                "藏物金箱", mid, floor - 1, ref report);
            Place(WorldGen.PlacePot(left + 1, floor - 1, TileID.Pots,
                rand.Next(L2Palette.PotStyleMin, L2Palette.PotStyleMax)),
                "罐", left + 1, floor - 1, ref report);
            //一撮骨堆:上一任主人
            Place(WorldGen.PlaceSmallPile(left + width - 2, floor - 1,
                rand.Next(L2Palette.SmallBone1x1Min, L2Palette.SmallBone1x1Max), 0),
                "骨杂物", left + width - 2, floor - 1, ref report);
        }

        private static void Place(bool ok, string what, int x, int y, ref RowReport report) {
            if (ok) {
                report.FurniturePlaced++;
            }
            else {
                report.FurnitureRejected++;
                CWRMod.Instance.Logger.Warn($"[L2CellRow] {what}放置失败 at ({x},{y})");
            }
        }
    }
}
