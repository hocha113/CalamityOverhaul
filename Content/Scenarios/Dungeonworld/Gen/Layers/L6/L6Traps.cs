using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //====================================================================
    //L6陷阱语法库(峰值形态,ROOMS-L6 §1母题库;公平性底线=STRUCTURES §2.4-⑥)
    //
    //类型编号对源校正(WorldGen.cs L5556-5923,placeTrap本体):
    //  type0=飞镖+灰压板(墙下style2,L5983)  type1=巨石落石(活石塞+石巢,L5637)
    //  type2=埋地炸药  type3=间歇泉，D表"type2巨石/type3炸药"系research笔误,
    //  本层落石用type1;炸药(type2)与间歇泉(type3)全层禁用(炸几何/离题)
    //
    //每处杀招的预告手段(逐母题注释),统一底线:
    //  压板可见(灰板)+箭垛/落石巢剪影+油渍引导线(paint)+躲避龛2x3+段间2格净缓冲;
    //  wire原版语义玩家不可见(INDEX §7),可读性全部由可见件承担
    //
    //A型走廊剖面(内膛高9):
    //  [龛带4行(默认实心;躲避龛/活塞槽/落石巢在此)] + [行走5行] + 地板
    //B型走廊剖面(内膛高15):
    //  [龛带4] + [机关道5] + [中层地板1(裂砖段在此)] + [下厅5] + 地板
    //====================================================================
    internal static class L6Traps
    {
        //A型走廊龛带高度(躲避龛3空+1平台;落石巢按placeTrap type1扫描窗恰为此4行)
        internal const int NicheBand = 4;
        internal const int WalkH = 5;
        internal const int AInteriorH = NicheBand + WalkH;              //9
        internal const int BInteriorH = NicheBand + WalkH + 1 + 5;      //15,下厅净高5

        internal struct Tally
        {
            internal int TrapsPlaced;
            internal int TrapsFailed;
            internal int FurnPlaced;
            internal int FurnRejected;
            internal int Segments;
            //置位=走廊入口需要警示牌(致命母题:落石/刺坑)
            internal bool WantsSign;

            internal void Furn(bool ok, string what, int x, int y) {
                if (ok) {
                    FurnPlaced++;
                }
                else {
                    FurnRejected++;
                    CWRMod.Instance.Logger.Warn($"[L6Traps] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //==================== 母题池(密度梯度按折序解禁,ROOMS-L6 §3密度曲线) ====================

        internal enum Motif { Conveyor, Dart, DartNet, Boulder, PistonSlot, GearCrush }

        /// <summary>按威胁层级掷一段A型母题;首段强制低威胁(ROOMS-L6:节奏由浅入深)</summary>
        internal static Motif RollMotif(UnifiedRandom rand, int tier, bool firstSegment, Motif previous) {
            for (int guard = 0; guard < 8; guard++) {
                Motif roll = tier switch {
                    0 => rand.NextBool(2) ? Motif.Conveyor : Motif.Dart,
                    1 => rand.Next(4) switch { 0 => Motif.Conveyor, 1 => Motif.Dart, 2 => Motif.DartNet, _ => Motif.PistonSlot },
                    2 => rand.Next(5) switch {
                        0 => Motif.Dart,
                        1 => Motif.DartNet,
                        2 => Motif.Boulder,
                        3 => Motif.PistonSlot,
                        _ => Motif.GearCrush,
                    },
                    _ => rand.Next(5) switch {
                        0 => Motif.Dart,
                        1 => Motif.DartNet,
                        2 => Motif.Boulder,
                        3 => Motif.GearCrush,
                        _ => Motif.DartNet,
                    },
                };
                if (firstSegment && roll is Motif.DartNet or Motif.Boulder or Motif.GearCrush) {
                    continue;   //首段只许低威胁(传送带/单镖/活塞留位)
                }
                if (roll == previous) {
                    continue;   //同母题不连续(ROOMS-L6母题排布)
                }
                return roll;
            }
            return firstSegment ? Motif.Conveyor : Motif.Dart;
        }

        /// <summary>母题的段长档(A型段长12~20+镖网特许加长,§2.4-⑥)</summary>
        internal static int RollSegLen(UnifiedRandom rand, Motif motif) => motif switch {
            Motif.Conveyor => rand.Next(12, 17),
            Motif.Dart => rand.Next(14, 19),
            Motif.DartNet => rand.Next(23, 27),
            Motif.Boulder => rand.Next(13, 17),
            Motif.GearCrush => rand.Next(12, 16),
            _ => rand.Next(9, 13),
        };

        //==================== 原版函数包装(F35复用;拒绝记数,fail loud交调用方) ====================

        /// <summary>
        /// 飞镖+压板(placeTrap type0)。镖位=板行上0~2行向两侧扫首个实心、
        /// 距离(5,50)合法(对源L5562/L5598);板落点需3宽x3高净空(L5488-5513)。
        /// 调用方保证:板列两侧7~10格内有2高箭垛(镖的家),板行=floor-1。
        /// </summary>
        internal static bool TryPlaceDart(int plateX, int plateRow, ref Tally tally) {
            bool ok = false;
            for (int attempt = 0; attempt < 6 && !ok; attempt++) {
                ok = WorldGen.placeTrap(plateX + attempt % 2, plateRow, 0);
            }
            if (ok) {
                tally.TrapsPlaced++;
            }
            else {
                tally.TrapsFailed++;
                CWRMod.Instance.Logger.Warn($"[L6Traps] 飞镖placeTrap六次未成 at ({plateX},{plateRow})");
            }
            return ok;
        }

        /// <summary>
        /// 落石(placeTrap type1)。要求板行上8行起有6宽x4高全实心巢区且含>=3格
        /// 石/土/泥(L5677-5718)：全砖世界必失败,调用方须先SeedBoulderNest;
        /// 函数自凿2宽落槽+石质巢领+活石塞(巢领剪影即预告,L5735-5779)。
        /// </summary>
        internal static bool TryPlaceBoulder(int plateX, int plateRow, ref Tally tally) {
            bool ok = false;
            //巢心横移±1由函数内部再掷,种石区已按±1覆盖
            int[] jitter = [0, 1, -1, 0];
            for (int attempt = 0; attempt < jitter.Length && !ok; attempt++) {
                ok = WorldGen.placeTrap(plateX + jitter[attempt], plateRow, 1);
            }
            if (ok) {
                tally.TrapsPlaced++;
            }
            else {
                tally.TrapsFailed++;
                CWRMod.Instance.Logger.Warn($"[L6Traps] 落石placeTrap四次未成 at ({plateX},{plateRow})");
            }
            return ok;
        }

        //==================== A型段构件 ====================

        /// <summary>
        /// 躲避龛2x3(§2.4-⑥构造性躲避):龛带内3行空+底沿平台,自地板满跳6行可入
        /// (F2稳定跨越≤6);镖行(floor-1..-3)与滚石路径(floor-2..-1)均打不到龛内。
        /// </summary>
        internal static void CarvePocket(RoomNode room, int x, ref Tally tally, bool lantern = false) {
            int nicheTop = room.InteriorTop;
            TileBrush.CarveRect(x, nicheTop, x + 2, nicheTop + 3, L6Palette.WallSlab);
            TileBrush.PlatformRow(x, x + 2, nicheTop + 3, L6Palette.PlatformFrameY);
            if (lantern) {
                tally.Furn(L6Palette.TryPlaceObject(x, nicheTop, TileID.HangingLanterns,
                    L6Palette.LanternBrassStyle), "龛内黄铜灯笼", x, nicheTop);
            }
        }

        /// <summary>2高箭垛柱:飞镖的合法停靠点兼剪影预告;顶留3行净空可跳越(F3)</summary>
        internal static void BuildPier(RoomNode room, int x) {
            int floor = room.FloorTop;
            TileBrush.SetSolid(x, floor - 1, L6Palette.Brick);
            TileBrush.SetSolid(x, floor - 2, L6Palette.Brick);
        }

        /// <summary>
        /// 哑炮腔彩蛋(ROOMS-L6 §3):龛带内密封2x2藏物腔,行走面天花一格裂砖
        /// 提示"可破"(原版裂=危险/可破语言,镜像L2牢栅暗塞先例);拆开得罐。
        /// </summary>
        internal static void CarveDudCavity(RoomNode room, int x, UnifiedRandom rand, ref Tally tally) {
            int nicheTop = room.InteriorTop;
            TileBrush.CarveRect(x, nicheTop + 1, x + 2, nicheTop + 3, L6Palette.WallSlab);
            tally.Furn(WorldGen.PlacePot(x, nicheTop + 2, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                "哑炮腔藏罐", x, nicheTop + 2);
            //行走面天花的裂砖提示格
            TileBrush.SetSolid(x, nicheTop + 3, L6Palette.CrackedBrick);
        }

        //==================== A型母题实现(段内坐标=内膛列区间[segL,segR)) ====================

        /// <summary>
        /// 母题:传送带段(低威胁调剂/首段)。预告手段:带面本身可见+零伤害。
        /// 传送带纯碰撞机制(Collision.cs L3419读ConveyorDirection),不依赖UpdateMech。
        /// </summary>
        internal static void SegConveyor(RoomNode room, int segL, int segR, UnifiedRandom rand, ref Tally tally) {
            int floor = room.FloorTop;
            ushort belt = rand.NextBool(2) ? L6Palette.BeltPushRight : L6Palette.BeltPushLeft;
            for (int x = segL + 1; x < segR - 1; x++) {
                TileBrush.SetSolid(x, floor, belt);
            }
            CarvePocket(room, segL + (segR - segL) / 2 - 1, ref tally, lantern: true);
            tally.Segments++;
        }

        /// <summary>
        /// 母题:单镖段。预告手段:箭垛剪影(2高柱)+灰压板可见+板前油渍引导线
        /// +段中躲避龛+龛灯照明(禁无光,INDEX §3)。1/4掷哑炮腔彩蛋。
        /// </summary>
        internal static void SegDart(RoomNode room, int segL, int segR, UnifiedRandom rand, ref Tally tally) {
            int floor = room.FloorTop;
            int pier = segL + 2;
            int plate = pier + rand.Next(7, 10);
            plate = System.Math.Min(plate, segR - 3);

            BuildPier(room, pier);
            CarvePocket(room, pier + 3, ref tally, lantern: true);
            L6Palette.OilStreakFloor(plate - 3, floor, 3);
            L6Palette.OilStreakFloor(plate + 1, floor, 2);
            TryPlaceDart(plate, floor - 1, ref tally);

            if (rand.NextBool(4)) {
                CarveDudCavity(room, System.Math.Min(plate + 4, segR - 3), rand, ref tally);
            }
            tally.Segments++;
        }

        /// <summary>
        /// 母题:压力板网+交叉射界(延时链)。两板两镖,双箭垛夹段;镖侧向由两侧
        /// 合法停靠随机落定(L5947-5973),交叉火线自然形成。预告手段:双箭垛剪影
        /// +两块灰压板全可见+全段油渍引导+板间高位躲避龛。
        /// 注:所谓"延时"是空间错位链(踩A板起跳,落点附近是B板),不用定时器(F17停摆)。
        /// </summary>
        internal static void SegDartNet(RoomNode room, int segL, int segR, UnifiedRandom rand, ref Tally tally) {
            int floor = room.FloorTop;
            int pierA = segL + 2;
            int plate1 = pierA + rand.Next(7, 9);
            int plate2 = plate1 + rand.Next(5, 7);
            int pierB = System.Math.Min(plate2 + rand.Next(7, 9), segR - 2);

            BuildPier(room, pierA);
            BuildPier(room, pierB);
            //龛悬在两板之间的上方,交叉火线打不到(镖行最高floor-3,龛台floor-6)
            CarvePocket(room, (plate1 + plate2) / 2, ref tally, lantern: true);
            L6Palette.OilStreakFloor(plate1 - 3, floor, plate2 - plate1 + 5);
            TryPlaceDart(plate1, floor - 1, ref tally);
            TryPlaceDart(plate2, floor - 1, ref tally);
            tally.Segments++;
        }

        /// <summary>
        /// 母题:落石段(placeTrap type1显式启用,巢区先种石)。预告手段:
        /// 天花石质巢领+2宽落槽+活石塞剪影(函数自带,L5735-5779)+槽下焦油垂滴
        /// +板可见+邻位躲避龛;走廊入口另立警示牌(WantsSign)。
        /// </summary>
        internal static void SegBoulder(RoomNode room, int segL, int segR, UnifiedRandom rand, ref Tally tally) {
            int floor = room.FloorTop;
            int nicheTop = room.InteriorTop;
            int plate = (segL + segR) / 2;

            //巢区种石:板行上8行起6宽x4高须全实心且>=3格石(L5677-5718);
            //±1巢心抖动一并覆盖,石质补丁本身就是"岩巢"剪影
            for (int x = plate - 4; x <= plate + 5; x++) {
                for (int y = nicheTop; y < nicheTop + NicheBand; y++) {
                    TileBrush.SetSolid(x, y, L6Palette.NestStone);
                }
            }
            CarvePocket(room, System.Math.Max(segL + 1, plate - 7), ref tally, lantern: true);
            L6Palette.OilStreakFloor(plate - 3, floor, 3);

            if (TryPlaceBoulder(plate, floor - 1, ref tally)) {
                //落槽两侧焦油垂滴(机油顺槽淌下,读作"这里有机器"的做旧信号)
                L6Palette.TarDrip(plate - 2, nicheTop + 4, 2);
                L6Palette.TarDrip(plate + 3, nicheTop + 4, 2);
                tally.WantsSign = true;
            }
            tally.Segments++;
        }

        /// <summary>
        /// 母题:活塞推杆。龛带内3x3帧精确空槽+Cog缸体+焦油垂滴+登记L6MachineSlots。
        /// 登记的帧只是缸体包络,不含行程，运行时由Machines\DungeonworldMachines
        /// 现场往下量到行走面再决定捶多深。
        /// </summary>
        internal static void SegPistonSlot(RoomNode room, int segL, int segR, UnifiedRandom rand, ref Tally tally) {
            int nicheTop = room.InteriorTop;
            int sx = (segL + segR) / 2 - 1;
            TileBrush.CarveRect(sx, nicheTop + 1, sx + 3, nicheTop + 3, L6Palette.WallSlab);
            TileBrush.SetSolid(sx, nicheTop, L6Palette.CogBlock);
            TileBrush.SetSolid(sx + 1, nicheTop, L6Palette.CogBlock);
            TileBrush.SetSolid(sx + 2, nicheTop, L6Palette.CogBlock);
            L6Palette.TarDrip(sx + 1, nicheTop + 3, 3);
            L6MachineSlots.Register(L6SlotKind.Piston,
                new Rectangle(sx, nicheTop + 1, 3, 3), "机关走廊活塞槽,头朝下捶向行走面");
            CarvePocket(room, System.Math.Max(segL + 1, sx - 5), ref tally);
            tally.Segments++;
        }

        /// <summary>
        /// 母题:齿轮碾压。行走带顶两行Cog轮齿剪影,净空3(F1低威胁净空档);
        /// 预告手段:轮齿本身+焦油垂滴+全段油渍+躲避龛(碾轮上膛段不咬人,
        /// 那半秒就是给玩家钻龛的)。登记段宽x行走带包络,运行时由
        /// Machines\DungeonworldMachines驱动碾轮沿行走面横扫。
        /// </summary>
        internal static void SegGearCrush(RoomNode room, int segL, int segR, UnifiedRandom rand, ref Tally tally) {
            int floor = room.FloorTop;
            int nicheTop = room.InteriorTop;
            int start = segL + 2 + rand.Next(0, 2);
            for (int x = start; x < segR - 2; x += 3) {
                TileBrush.SetSolid(x, floor - 4, L6Palette.CogBlock);
                TileBrush.SetSolid(x, floor - 5, L6Palette.CogBlock);
                L6Palette.TarDrip(x, floor - 3, 2);
            }
            L6Palette.OilStreakFloor(segL + 1, floor, System.Math.Max(2, segR - segL - 2));
            CarvePocket(room, (segL + segR) / 2 - 1, ref tally, lantern: true);
            L6MachineSlots.Register(L6SlotKind.GearCrush,
                new Rectangle(segL, nicheTop, System.Math.Max(1, segR - segL), NicheBand + WalkH),
                "机关走廊齿轮碾压段,轮齿朝下扫过行走面");
            tally.Segments++;
        }

        //==================== B型构件(裂砖假地板走廊的中层/下厅语法) ====================

        /// <summary>
        /// B型:裂砖假地板跨段(F31)+可选组合技。预告手段:跨段前3格单格裂砖预告
        /// (镜像L2教学"第一课"语言)+裂纹贴图本身+下厅灯照明;刺坑变体另立警示牌。
        /// spikes=下厅地板衬刺(宽≤5,下厅通行者可satisfying跳越,F2水平跳距>6格);
        /// dartOver=跨段后箭垛+跨段前压板(组合技:贪快踩板,镖沿机关道追身)。
        /// </summary>
        internal static void CrackedSpan(RoomNode room, int midFloor, int spanL, int spanR,
            bool spikes, bool dartOver, UnifiedRandom rand, ref Tally tally) {
            int lowerFloor = room.FloorTop;

            //预告格+跨段本体
            TileBrush.SetSolid(spanL - 3, midFloor, L6Palette.CrackedBrick);
            for (int x = spanL; x < spanR; x++) {
                TileBrush.SetSolid(x, midFloor, L6Palette.CrackedBrick);
            }

            if (spikes) {
                int mid = (spanL + spanR) / 2;
                int half = System.Math.Min(2, (spanR - spanL) / 2);
                for (int x = mid - half; x <= mid + half; x++) {
                    TileBrush.SetSolid(x, lowerFloor, TileID.Spikes);
                }
                tally.WantsSign = true;
            }
            else {
                //软着陆下厅:安慰奖罐+油渍(渣场语汇,无骨堆，骨归L5)
                tally.Furn(WorldGen.PlacePot(spanL + 1, lowerFloor - 1, TileID.Pots,
                    rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                    "缓冲室罐", spanL + 1, lowerFloor - 1);
                L6Palette.OilStreakFloor(spanL, lowerFloor, spanR - spanL);
            }

            if (dartOver) {
                //机关道层的追身镖:板在跨段前、箭垛在跨段后,飞线横穿跨段上空
                int pier = System.Math.Min(spanR + 2, room.InteriorRight - 2);
                TileBrush.SetSolid(pier, midFloor - 1, L6Palette.Brick);
                TileBrush.SetSolid(pier, midFloor - 2, L6Palette.Brick);
                int plate = System.Math.Max(spanL - 5, room.InteriorLeft + 3);
                L6Palette.OilStreakFloor(plate - 2, midFloor, 2);
                TryPlaceDart(plate, midFloor - 1, ref tally);
            }
            tally.Segments++;
        }

        /// <summary>
        /// B型:机关道端头梯口(镜像L2教学廊先例):中层开3宽口+双平台,
        /// 下厅↔机关道可往返(掉下去顺梯爬回,软着陆条款闭环)。
        /// </summary>
        internal static void DeckLadder(RoomNode room, int midFloor, int x) {
            TileBrush.CarveRect(x, midFloor, x + 3, midFloor + 1, L6Palette.WallTiled);
            TileBrush.PlatformRow(x, x + 3, midFloor, L6Palette.PlatformFrameY);
            TileBrush.PlatformRow(x, x + 3, midFloor + 3, L6Palette.PlatformFrameY);
        }
    }
}
