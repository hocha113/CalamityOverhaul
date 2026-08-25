using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //未完成者·巨像装配湾(WAVE2-BUILDINGS §3.4):折与折之间的竖向装配湾,
    //正中立一尊没造完的银灰战争巨像。一臂举起融进顶部工字梁,另一臂断在地上
    //摔成三截;肩髋膝关节是裸露齿轮块,锈橙自关节缝流下。脚手架平台绕像身
    //之字而上,两处歇脚台各有工作台与告示;后颈检修口通颅腔,里面一只锁金箱
    //和两个空眼窝。与L5巨兽上下呼应:一具长出来的骸骨、一具造出来的躯壳,都缺心。
    //
    //集成:L6Content.PlanAndBuild在RightWing之后、墙变体收尾/RustWash之前一行接线,
    //湾体自动吃到全带锈橙层染(Cog关节被PaintTilesOfType刷亮橙,银砖不在层染
    //白名单里保持毛坯银)。每世界至多1座。
    //
    //跨编队裁决(实施任务书§1.4):巨像只取非末折折间(候选=末折间隙之前的
    //中下段间隙),末折地板下让给IMPL-B渣汽疏泄带;窄间隙种子降档44h再试,
    //仍败Warn缺席。
    //
    //纪律:选址先CanReserve+宿主扫描双过关再TryReserve(零搁浅预留);
    //入口/底口连接条一律CanReserve+MarkUnchecked落账后才刻画
    //(镜像IntersticePlanner);链禁令合规:全程零锁链,悬吊感由臂融工字梁表达。
    internal static class L6Colossus
    {
        //告示(工头体,镜像L6Rooms文案voice;缺的是心,与L5巨兽的生命水晶互文)
        private const string SignCast = "第七次浇铸失败。心室尺寸不合。";
        private const string SignHeart = "吊臂就位。等一颗合尺寸的心。";

        //湾体内膛档(34~38w x 44~52h,cap=间隙-30;降档一轮=44h)
        private const int BayWidthMin = 34, BayWidthMax = 38;
        private const int BayHeightMin = 44, BayHeightMax = 52;
        //湾顶贴上折地板的悬距:上折房Bounds.Bottom(+2)+padding(+2)再留2行岩
        private const int HangBelowFold = 6;

        /// <summary>
        /// 主入口(L6Content层流末端调用):候选折间x候选窗逐个试,
        /// 全败Warn缺席(该种子无巨像,世界合法)。
        /// </summary>
        internal static void TryBuild(LayerBuildContext ctx, int[] floors,
            int xLeft, int xRight, UnifiedRandom rand) {
            int foldCount = floors.Length;
            if (foldCount < 5) {
                CWRMod.Instance.Logger.Warn($"[L6Colossus] 折数{foldCount}异常,缺席");
                return;
            }
            //非末折折间,中下段优先:末折间隙(foldCount-2)整体让给渣汽疏泄带
            int[] gaps = [foldCount - 3, foldCount - 4, foldCount - 5];
            foreach (int g in gaps) {
                if (g < 1) {
                    continue;
                }
                int gapRows = floors[g + 1] - floors[g];
                int iw = rand.Next(BayWidthMin, BayWidthMax + 1);
                int ihRoll = rand.Next(BayHeightMin, BayHeightMax + 1);
                //降档序列:先掷高,窗全败后压44h再扫一轮(窄间隙裁决)
                foreach (int pass in new[] { ihRoll, BayHeightMin }) {
                    int ih = System.Math.Min(pass, gapRows - 30);
                    if (ih < 40) {
                        continue;
                    }
                    if (TryPlaceInGap(ctx, floors, g, iw, ih, xLeft, xRight, rand)) {
                        return;
                    }
                    if (pass == BayHeightMin) {
                        break;
                    }
                }
            }
            CWRMod.Instance.Logger.Warn("[L6Colossus] 候选折间/候选窗全败,巨像缺席(种子拥挤,非硬错误)");
        }

        private static bool TryPlaceInGap(LayerBuildContext ctx, int[] floors, int g,
            int iw, int ih, int xLeft, int xRight, UnifiedRandom rand) {
            int totalW = iw + DungeonworldMetrics.RoomShellThick * 2;
            int totalH = ih + DungeonworldMetrics.RoomShellThick * 2;
            int usableL = xLeft + 6;
            int usableR = xRight - 6 - totalW;
            if (usableR <= usableL) {
                return false;
            }
            //主竖井双保险禁带(±40,栅格预留之外再挡一手)
            int keepL = DungeonworldMetrics.ShaftLeft - 40;
            int keepR = DungeonworldMetrics.ShaftLeft + DungeonworldMetrics.ShaftWidth + 40;

            foreach (double frac in new[] { 0.14, 0.38, 0.62, 0.86 }) {
                int wx = System.Math.Min(usableR,
                    usableL + (int)((usableR - usableL) * frac) + rand.Next(0, 9));
                if (wx + totalW > keepL && wx < keepR) {
                    continue;
                }
                var rect = new Rectangle(wx, floors[g] + HangBelowFold, totalW, totalH);
                if (!ctx.Grid.CanReserve(rect, DungeonworldMetrics.RoomPadding)) {
                    continue;
                }
                //先扫入口宿主(先扫后留,零搁浅预留);右侧脚手架优先,失败镜像
                if (!FindEntry(ctx, rect, floors[g], out RoomNode host, out int hostIdx,
                    out int stairX, out bool mirrored)) {
                    continue;
                }
                //刚CanReserve过且期间零写入,必成
                ctx.Grid.TryReserve(rect, DungeonworldMetrics.RoomPadding);
                Build(ctx, rect, g, floors, mirrored, host, hostIdx, stairX, rand);
                return true;
            }
            return false;
        }

        //==================== 入口宿主扫描 ====================

        //空列扫描:落口3列地板必须是素蓝砖(拒裂砖/齿轮/尖刺/传送带等机关面),
        //上方4行无任何tile(家具/压板/渣山/平台全挡)。机关廊的龛带回填与
        //B型中层地板天然通不过本检,危险房自动出局
        private static bool FloorColumnsClear(RoomNode room, int c) {
            for (int dx = 0; dx < 3; dx++) {
                int x = c + dx;
                Tile f = Main.tile[x, room.FloorTop];
                if (!f.HasTile || f.TileType != L6Palette.Brick) {
                    return false;
                }
                for (int dy = 1; dy <= 4; dy++) {
                    if (Main.tile[x, room.FloorTop - dy].HasTile) {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool FindEntry(LayerBuildContext ctx, Rectangle rect, int foldFloor,
            out RoomNode host, out int hostIdx, out int stairX, out bool mirrored) {
            int bx = (rect.Left + rect.Right) / 2;
            foreach (bool mir in new[] { false, true }) {
                int regionLo = mir ? rect.X + 3 : bx + 6;
                int regionHi = (mir ? bx - 6 : rect.Right - 3) - 3;
                for (int i = 0; i < ctx.Graph.Rooms.Count; i++) {
                    RoomNode r = ctx.Graph.Rooms[i];
                    if (r.FloorTop != foldFloor) {
                        continue;
                    }
                    int lo = System.Math.Max(regionLo, r.InteriorLeft + 2);
                    int hi = System.Math.Min(regionHi, r.InteriorRight - 5);
                    for (int c = lo; c <= hi; c++) {
                        if (!FloorColumnsClear(r, c)) {
                            continue;
                        }
                        //宿主底到湾顶之间2行岩的连接条可留(井体其余行在双方账里)
                        int stripTop = r.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
                        var strip = new Rectangle(c - 1, stripTop, 5, rect.Y - stripTop);
                        if (strip.Height > 0 && !ctx.Grid.CanReserve(strip, 0)) {
                            continue;
                        }
                        host = r;
                        hostIdx = i;
                        stairX = c;
                        mirrored = mir;
                        return true;
                    }
                }
            }
            host = null;
            hostIdx = -1;
            stairX = -1;
            mirrored = false;
            return false;
        }

        //==================== 落成 ====================

        private static void Build(LayerBuildContext ctx, Rectangle rect, int g, int[] floors,
            bool mirrored, RoomNode host, int hostIdx, int stairX, UnifiedRandom rand) {
            var bay = new RoomNode { Bounds = rect, Role = RoomRole.Treasure };
            L6Rooms.Tally tally = BuildInterior(bay, mirrored, rand,
                out int landingRow, out int hatchRow, out int holeLo, out int holeHi);

            int bayIdx = ctx.Graph.Rooms.Count;
            ctx.Graph.Rooms.Add(bay);

            //顶部入口:宿主地板落口→楼梯井→湾顶平台(连接条先落账)
            int stripTop = host.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
            if (rect.Y > stripTop) {
                ctx.Grid.MarkUnchecked(new Rectangle(stairX - 1, stripTop, 5, rect.Y - stripTop));
            }
            var entryGap = new DoorSocket(SocketSide.Bottom, stairX - host.Bounds.Left,
                SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
            host.Sockets.Add(entryGap);
            CorridorRouter.RouteToFloorBelow(host, entryGap, landingRow,
                L6Palette.PlatformFrameY, L6Palette.WallTiled);
            ctx.Graph.Edges.Add(new RoomEdge(hostIdx, bayIdx, SocketKind.PlatformGap, EdgeForm.StairWell));

            //底部出口:湾底探折g+1房天花之间的空柱,成则下探成环防一本道,败则单口
            bool loop = TryBottomExit(ctx, bay, bayIdx, floors[g + 1], holeLo, holeHi);

            CWRMod.Instance.Logger.Info(
                $"[L6Colossus] 落成 origin=({rect.X},{rect.Y}) 折间={g}->{g + 1}"
                + $" 湾={rect.Width - 4}x{rect.Height - 4} 镜像={mirrored}"
                + $" 入口=({stairX},{host.FloorTop}) 底口环路={(loop ? "有" : "无(单口)")}"
                + $" 家具={tally.Placed}成/{tally.Rejected}拒 肩位留位=2");
        }

        private static bool TryBottomExit(LayerBuildContext ctx, RoomNode bay, int bayIdx,
            int lowerFloor, int holeLo, int holeHi) {
            for (int i = 0; i < ctx.Graph.Rooms.Count; i++) {
                RoomNode r = ctx.Graph.Rooms[i];
                if (r.FloorTop != lowerFloor || ReferenceEquals(r, bay)) {
                    continue;
                }
                int lo = System.Math.Max(holeLo, r.InteriorLeft + 2);
                int hi = System.Math.Min(holeHi, r.InteriorRight - 5);
                for (int c = lo; c <= hi; c++) {
                    //目标房全内膛空柱(天花吊挂物/平台/家具全挡)+地板素砖
                    if (!ColumnClearThrough(r, c)) {
                        continue;
                    }
                    int stripTop = bay.Bounds.Bottom + DungeonworldMetrics.RoomPadding;
                    int stripBottom = r.Bounds.Top - DungeonworldMetrics.RoomPadding;
                    var strip = new Rectangle(c - 1, stripTop, 5, stripBottom - stripTop);
                    if (strip.Height <= 0 || !ctx.Grid.CanReserve(strip, 0)) {
                        continue;
                    }
                    ctx.Grid.MarkUnchecked(strip);
                    var gap = new DoorSocket(SocketSide.Bottom, c - bay.Bounds.Left,
                        SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                    bay.Sockets.Add(gap);
                    CorridorRouter.RouteToFloorBelow(bay, gap, r.FloorTop,
                        L6Palette.PlatformFrameY, L6Palette.WallTiled);
                    ctx.Graph.Edges.Add(new RoomEdge(bayIdx, i, SocketKind.PlatformGap, EdgeForm.StairWell));
                    return true;
                }
            }
            return false;
        }

        private static bool ColumnClearThrough(RoomNode room, int c) {
            for (int dx = 0; dx < 3; dx++) {
                int x = c + dx;
                Tile f = Main.tile[x, room.FloorTop];
                if (!f.HasTile || f.TileType != L6Palette.Brick) {
                    return false;
                }
                for (int y = room.InteriorTop; y < room.FloorTop; y++) {
                    if (Main.tile[x, y].HasTile) {
                        return false;
                    }
                }
            }
            return true;
        }

        //==================== 湾体+巨像+脚手架(看样入口共用的纯刻画核) ====================

        private static L6Rooms.Tally BuildInterior(RoomNode bay, bool mirrored, UnifiedRandom rand,
            out int landingRow, out int hatchRow, out int holeLo, out int holeHi) {
            var tally = new L6Rooms.Tally();
            int iL = bay.InteriorLeft;
            int iR = bay.InteriorRight;
            int iTop = bay.InteriorTop;
            int bf = bay.FloorTop;
            int ih = bf - iTop;
            int bx = (iL + iR) / 2;
            landingRow = iTop + 3;

            //内膛一遍成型(外壳=骨架原生蓝砖,镜像InfillRooms几何纪律)
            TileBrush.CarveRect(iL, iTop, iR, bf, L6Palette.WallTiled);

            //---巨像剪影:座1+腿lh+髋2+躯th+头7=ch,占湾高约八成---
            int ch = System.Math.Clamp(ih - 8, 36, 42);
            int th = System.Math.Min(16, 14 + (ch - 36) / 2);
            int lh = ch - 10 - th;
            int legTop = bf - 1 - lh;
            int pelvTop = legTop - 2;
            int tTop = pelvTop - th;
            int hTop = tTop - 7;
            hatchRow = hTop + 5;

            //座:通宽1行,两端斜切收边(F24)
            for (int x = bx - 6; x < bx + 6; x++) {
                TileBrush.SetSolid(x, bf - 1, TileID.SilverBrick);
            }
            TileBrush.SetSloped(bx - 6, bf - 1, TileID.SilverBrick, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 5, bf - 1, TileID.SilverBrick, SlopeType.SlopeDownLeft);
            //腿x2(3宽),腿间4宽=底层穿行拱道
            FillRect(bx - 5, legTop, 3, lh);
            FillRect(bx + 2, legTop, 3, lh);
            //髋2行+躯干10宽
            FillRect(bx - 5, pelvTop, 10, 2);
            FillRect(bx - 5, tTop, 10, th);
            //肩线斜切收角
            TileBrush.SetSloped(bx - 5, tTop, TileID.SilverBrick, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 4, tTop, TileID.SilverBrick, SlopeType.SlopeDownLeft);
            //头6宽7高,顶角收分
            FillRect(bx - 3, hTop, 6, 7);
            TileBrush.SetSloped(bx - 3, hTop, TileID.SilverBrick, SlopeType.SlopeDownRight);
            TileBrush.SetSloped(bx + 2, hTop, TileID.SilverBrick, SlopeType.SlopeDownLeft);

            //---举臂融进顶部工字梁(悬吊感的实心几何表达,链禁令合规)---
            int armL = mirrored ? bx + 5 : bx - 7;
            for (int y = iTop; y < tTop + 2; y++) {
                TileBrush.SetSolid(armL, y, TileID.SilverBrick);
                TileBrush.SetSolid(armL + 1, y, TileID.SilverBrick);
            }
            int beamL = mirrored ? bx - 2 : System.Math.Max(iL + 1, bx - 14);
            int beamR = mirrored ? System.Math.Min(iR - 1, bx + 14) : bx + 2;
            for (int x = beamL; x < beamR; x++) {
                TileBrush.SetSolid(x, iTop, TileID.SilverBrick);
            }

            //---断臂三截(梯形收边不逐格噪声),摔在脚手架侧地面---
            int chunk1 = ChunkLeft(bx, mirrored, 5, 3);
            MoundSilver(chunk1, bf, 3, 2, rand);
            MoundSilver(ChunkLeft(bx, mirrored, 9, 2), bf, 2, 1, rand);
            bool thirdChunk = iR - bx >= 18;
            if (thirdChunk) {
                MoundSilver(ChunkLeft(bx, mirrored, 12, 2), bf, 2, 2, rand);
            }

            //---关节:肩/髋/膝2x2齿轮块(RustWash统一刷亮橙)+关节缝锈橙垂痕---
            JointCog(bx - 6, tTop);            //左肩(非镜像时兼作臂躯桥接)
            JointCog(bx + 4, tTop);            //右肩(断臂侧=裸露断口)
            JointCog(bx - 5, pelvTop);
            JointCog(bx + 3, pelvTop);
            int kneeY = legTop + lh / 2 - 1;
            JointCog(bx - 5, kneeY);
            JointCog(bx + 3, kneeY);
            L6Palette.ScorchDisk(bx - 5, tTop, 3);
            L6Palette.ScorchDisk(bx + 4, tTop, 3);
            //肩位小齿轮留位x2(帧包络6x6,资产波接管;纯留位零几何)
            L6MachineSlots.Register(L6SlotKind.GearSmall,
                new Rectangle(bx - 8, tTop - 2, 6, 6), "巨像左肩位");
            L6MachineSlots.Register(L6SlotKind.GearSmall,
                new Rectangle(bx + 2, tTop - 2, 6, 6), "巨像右肩位");

            //---颅腔:4x4内腔+双眼窝(1宽2高,tile空+墙留空=从外读黑洞)+后颈检修口+锁金箱---
            TileBrush.CarveRect(bx - 2, hTop + 1, bx + 2, hTop + 5, L6Palette.WallTiled);
            for (int dy = 1; dy <= 2; dy++) {
                TileBrush.ClearCell(bx - 2, hTop + dy, 0);
                TileBrush.ClearCell(bx + 1, hTop + dy, 0);
            }
            int hatchX = mirrored ? bx - 3 : bx + 2;
            TileBrush.CarveRect(hatchX, hTop + 2, hatchX + 1, hTop + 5, L6Palette.WallTiled);
            tally.Add(WorldGen.PlaceChest(bx - 1, hTop + 4, TileID.Containers,
                notNearOtherChests: false, L6Palette.ChestLockedGoldStyle) >= 0,
                "颅腔锁金箱", bx - 1, hTop + 4);

            //---脚手架:之字平台(竖距4)+两处歇脚台+湾顶落位/检修口两条通行平台---
            int regionL = mirrored ? iL : bx + 6;
            int regionR = mirrored ? bx - 6 : iR;
            BuildScaffold(bay, mirrored, regionL, regionR, hatchRow, landingRow, rand, ref tally);
            TileBrush.PlatformRow(regionL, regionR, landingRow, L6Palette.PlatformFrameY);
            int hatchPlatL = mirrored ? regionL : bx + 3;
            int hatchPlatR = mirrored ? bx - 3 : regionR;
            TileBrush.PlatformRow(hatchPlatL, hatchPlatR, hatchRow, L6Palette.PlatformFrameY);
            //检修口平台↔湾顶落位平台之间补之字档(头顶净空8~10行,裸跳上不去)
            for (int y = hatchRow - 4; y > landingRow + 1; y -= 4) {
                TileBrush.PlatformRow(regionL, regionR, y, L6Palette.PlatformFrameY);
            }

            //---木梁:贴壁一根+贴身一根(非碰撞背景柱,只写空气格;贴身柱离地4行,
            //不占地面与平台的通行包络)---
            int wallBeamX = mirrored ? iL : iR - 1;
            BeamColumn(wallBeamX, hatchRow, bf);
            int bodyBeamX = mirrored ? bx - 6 : bx + 5;
            BeamColumn(bodyBeamX, tTop, bf - 4);

            //---地面与杂项:装配油渍/罐/墙面Slab斑---
            L6Palette.OilStreakFloor(bx - 2, bf, 4);
            int potSideL = mirrored ? bx + 6 : iL + 1;
            tally.Add(WorldGen.PlacePot(potSideL, bf - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                "罐", potSideL, bf - 1);
            tally.Add(WorldGen.PlacePot(potSideL + 3, bf - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                "罐", potSideL + 3, bf - 1);
            L6Palette.WallDisk(bx + rand.Next(-8, 9), tTop + rand.Next(2, 8),
                rand.Next(3, 6), L6Palette.WallSlab);
            L6Palette.WallDisk(bx + rand.Next(-8, 9), legTop + rand.Next(0, 6),
                rand.Next(3, 5), L6Palette.WallSlab);

            //底口候选跨(断臂之外、贴壁1列之内),交回编排器探折g+1
            int chunkEnd = thirdChunk ? 14 : 11;
            holeLo = mirrored ? iL + 1 : bx + chunkEnd + 1;
            holeHi = mirrored ? bx - chunkEnd - 4 : iR - 4;
            return tally;
        }

        private static void BuildScaffold(RoomNode bay, bool mirrored, int regionL, int regionR,
            int hatchRow, int landingRow, UnifiedRandom rand, ref L6Rooms.Tally tally) {
            int bf = bay.FloorTop;
            int len = regionR - regionL - 3;
            //歇脚台取两档高度(约1/3与2/3);档位k强制取奇数=贴壁锚定层(之字节拍的墙侧拍)
            int climb = bf - hatchRow;
            int kA = System.Math.Max(1, climb / 12 | 1);
            int kB = System.Math.Max(kA + 2, climb * 2 / 12 | 1);
            int ledgeA = bf - kA * 4;
            int ledgeB = bf - kB * 4;
            bool wallIsRight = !mirrored;
            bool firstLedge = true;

            for (int y = bf - 4; y > hatchRow + 1; y -= 4) {
                if (System.Math.Abs(y - hatchRow) <= 1 || System.Math.Abs(y - landingRow) <= 1) {
                    continue;
                }
                bool anchorWall = ((bf - y) / 4 & 1) == 1;
                if (anchorWall && (y == ledgeA || y == ledgeB)) {
                    //歇脚台:贴壁5格实心银台+工作台+台面蜡烛+告示,平台接内端
                    int ledgeL = wallIsRight ? regionR - 5 : regionL;
                    for (int x = ledgeL; x < ledgeL + 5; x++) {
                        TileBrush.SetSolid(x, y, TileID.SilverBrick);
                    }
                    int benchX = wallIsRight ? ledgeL : ledgeL + 3;
                    int signX = wallIsRight ? ledgeL + 2 : ledgeL + 1;
                    tally.Add(L6Palette.TryPlaceTile(benchX, y - 1, TileID.WorkBenches,
                        L6Palette.WorkBenchStyle), "歇脚工作台", benchX, y - 1);
                    tally.Add(L6Palette.TryPlaceTile(benchX, y - 2, TileID.Candles,
                        L6Palette.CandleStyle), "台面蜡烛", benchX, y - 2);
                    tally.Add(L6Palette.PlaceSignWithText(signX, y - 1,
                        firstLedge ? SignCast : SignHeart), "装配台账", signX, y - 1);
                    firstLedge = false;
                    int restL = wallIsRight ? regionL : ledgeL + 5;
                    int restR = wallIsRight ? ledgeL : regionR;
                    TileBrush.PlatformRow(restL, restR, y, L6Palette.PlatformFrameY);
                    continue;
                }
                int pl = anchorWall == wallIsRight ? regionR - len : regionL;
                TileBrush.PlatformRow(pl, pl + len, y, L6Palette.PlatformFrameY);
                //平台蜡烛照明(禁无光,镜像L6Rooms.BuildWell做法)
                if ((bf - y) / 4 % 3 == 2) {
                    int cx = pl + len / 2;
                    tally.Add(L6Palette.TryPlaceTile(cx, y - 1, TileID.Candles,
                        L6Palette.CandleStyle), "架上蜡烛", cx, y - 1);
                }
            }
        }

        //==================== 刻画原语 ====================

        private static void FillRect(int left, int top, int w, int h) {
            for (int x = left; x < left + w; x++) {
                for (int y = top; y < top + h; y++) {
                    TileBrush.SetSolid(x, y, TileID.SilverBrick);
                }
            }
        }

        //断臂残块:梯形银砖堆(两端收级)+锈橙点渍
        private static void MoundSilver(int left, int floorRow, int w, int rise, UnifiedRandom rand) {
            for (int i = 0; i < w; i++) {
                int h = System.Math.Min(rise, System.Math.Min(i + 1, w - i));
                for (int k = 1; k <= h; k++) {
                    TileBrush.SetSolid(left + i, floorRow - k, TileID.SilverBrick);
                    if (rand.NextBool(3)) {
                        WorldGen.paintTile(left + i, floorRow - k, L6Palette.RustPaint);
                    }
                }
            }
        }

        //镜像感知的偏移换算:非镜像=像身右offset起w宽,镜像=左侧对称
        private static int ChunkLeft(int bx, bool mirrored, int offset, int w)
            => mirrored ? bx - offset - w : bx + offset;

        //关节2x2齿轮块+关节缝锈橙垂痕(HotPaint由层染PaintTilesOfType统一刷)
        private static void JointCog(int left, int top) {
            for (int dx = 0; dx < 2; dx++) {
                TileBrush.SetSolid(left + dx, top, L6Palette.CogBlock);
                TileBrush.SetSolid(left + dx, top + 1, L6Palette.CogBlock);
                for (int dy = 2; dy <= 4; dy++) {
                    int y = top + dy;
                    Tile t = Main.tile[left + dx, y];
                    if (t.HasTile && t.TileType == TileID.SilverBrick) {
                        WorldGen.paintTile(left + dx, y, L6Palette.RustPaint);
                    }
                }
            }
        }

        //木梁柱:只写空气格(平台/台面处自动断开),非碰撞背景柱
        private static void BeamColumn(int x, int yTop, int yBottom) {
            for (int y = yTop; y < yBottom; y++) {
                if (WorldGen.InWorld(x, y, 5) && !Main.tile[x, y].HasTile) {
                    TileBrush.SetSolid(x, y, TileID.WoodenBeam);
                }
            }
        }

        //==================== 免接线看样入口(镜像L6Preview惯例,单人调试用) ====================

        /// <summary>
        /// 在(originX, floorRow)处就地盖看样:完整湾体+巨像+脚手架(定尺36x48),
        /// 湾底左壁开侧口供步入。不注册GenPass、不入图;仅单人调试。
        /// </summary>
        internal static void BuildPreview(int originX, int floorRow, int seed = 1919) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L6Colossus] 看样入口仅单人调试用,联机不发tile同步");
            }
            var rand = new UnifiedRandom(seed);
            L6MachineSlots.Reset();
            const int iw = 36, ih = 48;
            var rect = new Rectangle(originX - (iw + 4) / 2, floorRow - (ih + 2), iw + 4, ih + 4);
            var stamp = new Rectangle(rect.X - 6, rect.Y - 6, rect.Width + 12, rect.Height + 12);
            for (int x = stamp.Left; x < stamp.Right; x++) {
                for (int y = stamp.Top; y < stamp.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L6Palette.Brick);
                }
            }
            var bay = new RoomNode { Bounds = rect, Role = RoomRole.Treasure };
            L6Rooms.Tally tally = BuildInterior(bay, mirrored: false, rand,
                out _, out _, out _, out _);
            //侧口:湾底左壁开4高步入口
            TileBrush.CarveRect(stamp.Left, bay.FloorTop - 4, rect.X + 2, bay.FloorTop, L6Palette.WallTiled);
            WorldGen.RangeFrame(stamp.Left - 1, stamp.Top - 1, stamp.Right + 1, stamp.Bottom + 1);
            L6MachineSlots.LogAll();
            CWRMod.Instance.Logger.Info(
                $"[L6Colossus] 看样落成 rect={rect} 家具={tally.Placed}成/{tally.Rejected}拒");
        }
    }
}
